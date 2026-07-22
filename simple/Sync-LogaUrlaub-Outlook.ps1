<#
    .SYNOPSIS
    Liest genehmigte Urlaubstage aus LOGA3 und traegt sie als ganztaegige,
    "Abwesend" markierte Termine in den lokalen Outlook-Kalender (Outlook Classic) ein.

    .DESCRIPTION
    Bewusst einfach gehalten: EIN Skript, keine Installation, kein Build, kein NuGet,
    kein GitHub Actions. Voraussetzungen (beide einmalig, ohne Administratorrechte):

      1. PowerShell-Modul "Selenium" installieren:
           Install-Module Selenium -Scope CurrentUser

      2. Microsoft Edge WebDriver (msedgedriver.exe) in der zur installierten
         Edge-Version passenden Fassung herunterladen von:
           https://developer.microsoft.com/microsoft-edge/tools/webdriver/
         und in denselben Ordner wie dieses Skript legen (oder Pfad unten anpassen).

    Start: Rechtsklick auf diese Datei -> "Mit PowerShell ausfuehren"
           oder in einem PowerShell-Fenster: .\Sync-LogaUrlaub-Outlook.ps1

    Diese Datei ist bewusst vom grossen .NET/WPF-Projekt in src/ getrennt und hat
    keinerlei Abhaengigkeit dazu.

    .NOTES
    WICHTIG: Der Abschnitt "LOGA-Kalender auslesen" ist aktuell ein bestmoeglicher
    Entwurf. Die konkreten HTML-Selektoren des Kalenders (welches Element zu welchem
    Datum gehoert) sind noch nicht abschliessend bestaetigt - siehe Kommentar bei
    Get-LogaUrlaubsEintraege weiter unten. Mit -WhatIf laesst sich das Skript
    gefahrlos testen, ohne Outlook-Termine zu veraendern.
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    # LOGA-Startseite. Bitte im Browser pruefen, welche Adresse VOR dem Login
    # tatsaechlich das Anmeldeformular zeigt (siehe README.md, Abschnitt 1).
    [string]$LogaLoginUrl = "https://dedalus.pi-asp.de/loga3/public/login",

    # Wie viele Tage rueckwirkend bzw. wie viele Tage in die Zukunft synchronisiert werden.
    [int]$SyncPastDays = 14,
    [int]$SyncFutureDays = 400,

    # Betreff/Kategorie des angelegten Outlook-Termins.
    [string]$Betreff = "Urlaub",
    [string]$Kategorie = "LOGA Urlaub",

    # Pfad zu msedgedriver.exe, falls nicht im selben Ordner wie dieses Skript.
    [string]$EdgeDriverPath = (Join-Path $PSScriptRoot "msedgedriver.exe"),

    # Browserfenster sichtbar lassen (empfohlen fuer die ersten Laeufe / zur Kontrolle).
    [switch]$Headless
)

$ErrorActionPreference = "Stop"

# ============================================================================
# 1) LOGA-Selektoren (zentral an einer Stelle, keine Verteilung im Code)
#    Basierend auf den bereitgestellten Screenshots des Anmeldeformulars.
# ============================================================================
$LogaSelectors = @{
    # Relative XPath-Ausdruecke (kein absoluter Pfad), gestuetzt auf die sichtbaren
    # Feldbeschriftungen "Kennung" / "Kennwort" - robuster als geratene CSS-Klassen.
    BenutzernameFeld = "//label[normalize-space(.)='Kennung']/following::input[1]"
    PasswortFeld     = "//label[normalize-space(.)='Kennwort']/following::input[1]"
    AnmeldenButton   = "//button[contains(., 'ANMELDEN') or contains(., 'Anmelden')]"

    # Element, das nach erfolgreichem Login sicher sichtbar ist (zur Erfolgspruefung).
    EingeloggtIndikator = "//*[contains(text(),'Kalendarium')]"

    # Der ganztaegige Kalendereintrag laut DevTools-Screenshot.
    GanztagEintrag = "div.personalWeek-alldayEvent-eventTitle"
}

# ============================================================================
# 2) Anmeldung bei LOGA
# ============================================================================

# Einfache Warteschleife statt WebDriverWait.Until(...): vermeidet die in PowerShell
# manchmal unzuverlaessige automatische Umwandlung von Scriptblocks in .NET-Delegaten
# und ist so leichter nachvollziehbar/debuggbar.
function Wait-SeElementByXPath {
    param(
        [Parameter(Mandatory)] $Driver,
        [Parameter(Mandatory)] [string]$XPath,
        [int]$TimeoutSeconds = 15
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        try {
            return $Driver.FindElement([OpenQA.Selenium.By]::XPath($XPath))
        }
        catch [OpenQA.Selenium.NoSuchElementException] {
            Start-Sleep -Milliseconds 500
        }
    } while ((Get-Date) -lt $deadline)

    throw "Element mit XPath '$XPath' wurde nach $TimeoutSeconds Sekunden nicht gefunden. " +
        "Die LOGA-Seitenstruktur hat sich moeglicherweise geaendert."
}

function Connect-Loga {
    param(
        [Parameter(Mandatory)] [System.Management.Automation.PSCredential]$Credential
    )

    Import-Module Selenium -ErrorAction Stop

    $edgeOptions = New-Object OpenQA.Selenium.Edge.EdgeOptions
    if ($Headless) {
        $edgeOptions.AddArgument("--headless=new")
    }

    if (-not (Test-Path $EdgeDriverPath)) {
        throw "msedgedriver.exe wurde unter '$EdgeDriverPath' nicht gefunden. " +
            "Bitte gemaess README.md herunterladen und dorthin legen (oder -EdgeDriverPath angeben)."
    }

    $driverDir = Split-Path $EdgeDriverPath -Parent
    $driver = New-Object OpenQA.Selenium.Edge.EdgeDriver($driverDir, $edgeOptions)

    try {
        $driver.Navigate().GoToUrl($LogaLoginUrl)

        $usernameField = Wait-SeElementByXPath -Driver $driver -XPath $LogaSelectors.BenutzernameFeld
        $passwordField = $driver.FindElement([OpenQA.Selenium.By]::XPath($LogaSelectors.PasswortFeld))

        $usernameField.SendKeys($Credential.UserName)
        $passwordField.SendKeys($Credential.GetNetworkCredential().Password)

        $driver.FindElement([OpenQA.Selenium.By]::XPath($LogaSelectors.AnmeldenButton)).Click()

        Wait-SeElementByXPath -Driver $driver -XPath $LogaSelectors.EingeloggtIndikator | Out-Null
    }
    catch {
        $driver.Quit()
        throw "LOGA-Anmeldung fehlgeschlagen: $($_.Exception.Message). Bitte Benutzername/Kennwort pruefen."
    }

    return $driver
}

# ============================================================================
# 3) LOGA-Kalender auslesen
#
#    HINWEIS: Dieser Teil ist noch NICHT vollstaendig bestaetigt. Aus dem Screenshot
#    wissen wir, dass ganztaegige Eintraege als
#        <div class="personalWeek-alldayEvent-eventTitle">Tarifurlaub</div>
#    dargestellt werden - aber nicht, welches Attribut (z. B. title="...", data-date="...")
#    das zugehoerige Datum enthaelt. Damit hier keine erfundenen Annahmen ins Skript
#    einfliessen, versucht die Funktion mehrere plausible Quellen der Reihe nach und
#    bricht mit einer klaren Fehlermeldung ab, wenn keine davon passt - anstatt still
#    falsche Daten zu liefern.
#
#    Wenn dieser Schritt fehlschlaegt: Bitte im Browser (F12) auf einen Urlaubs-Balken
#    klicken, im Elements-Tab das Element UND sein direktes Elternelement per
#    Rechtsklick -> "Copy" -> "Copy outerHTML" kopieren und mir schicken - dann passe
#    ich exakt diese Funktion an.
# ============================================================================
function Get-LogaUrlaubsEintraege {
    param(
        [Parameter(Mandatory)] $Driver,
        [Parameter(Mandatory)] [datetime]$Von,
        [Parameter(Mandatory)] [datetime]$Bis
    )

    $eintraege = @()
    $elemente = $Driver.FindElements([OpenQA.Selenium.By]::CssSelector($LogaSelectors.GanztagEintrag))

    foreach ($el in $elemente) {
        $text = $el.Text.Trim()

        # Nur Eintraege verarbeiten, deren Text auf Urlaub hindeutet (z. B. "Tarifurlaub",
        # "Erholungsurlaub", "Resturlaub", ...). Andere Abwesenheitsarten (Gleitzeit,
        # Krankheit, ...) werden bewusst ignoriert, wie vom Benutzer gewuenscht.
        if ($text -notmatch '(?i)urlaub') {
            continue
        }

        # Versuch 1: title-Attribut (haeufig bei Tooltips, enthaelt oft das volle Datum).
        $titleAttr = $el.GetAttribute("title")

        # Versuch 2: data-date/data-start-Attribute am Element selbst oder am Elternelement.
        $dataDate = $el.GetAttribute("data-date")
        if (-not $dataDate) { $dataDate = $el.GetAttribute("data-start") }
        if (-not $dataDate) {
            try {
                $parent = $el.FindElement([OpenQA.Selenium.By]::XPath(".."))
                $dataDate = $parent.GetAttribute("data-date")
                if (-not $dataDate) { $dataDate = $parent.GetAttribute("data-start") }
                if (-not $titleAttr) { $titleAttr = $parent.GetAttribute("title") }
            }
            catch {
                # Kein Elternelement gefunden oder kein Attribut vorhanden - ignorieren,
                # der naechste Versuch (Fehlermeldung unten) greift dann.
            }
        }

        $gefundenesDatum = $null
        foreach ($kandidat in @($dataDate, $titleAttr)) {
            if ($kandidat -match '(\d{1,2})\.(\d{1,2})\.(\d{4})') {
                $gefundenesDatum = [datetime]::new([int]$Matches[3], [int]$Matches[2], [int]$Matches[1])
                break
            }
            if ($kandidat -match '(\d{4})-(\d{2})-(\d{2})') {
                $gefundenesDatum = [datetime]::new([int]$Matches[1], [int]$Matches[2], [int]$Matches[3])
                break
            }
        }

        if (-not $gefundenesDatum) {
            Write-Warning ("Konnte fuer den Eintrag '{0}' kein Datum aus title/data-Attributen " +
                "ermitteln. Dieser Eintrag wird uebersprungen. Bitte HTML-Ausschnitt an den " +
                "Entwickler schicken, damit die Datumserkennung ergaenzt werden kann." -f $text)
            continue
        }

        if ($gefundenesDatum -lt $Von -or $gefundenesDatum -gt $Bis) {
            continue
        }

        $eintraege += [pscustomobject]@{
            Datum       = $gefundenesDatum
            Anzeigetext = $text
        }
    }

    # Aufeinanderfolgende Tage mit demselben Anzeigetext zu einem Zeitraum zusammenfassen.
    $eintraege = $eintraege | Sort-Object Datum
    $zeitraeume = @()
    $aktuell = $null

    foreach ($eintrag in $eintraege) {
        if ($aktuell -and $eintrag.Datum -eq $aktuell.Ende.AddDays(1) -and $eintrag.Anzeigetext -eq $aktuell.Anzeigetext) {
            $aktuell.Ende = $eintrag.Datum
        }
        else {
            if ($aktuell) { $zeitraeume += $aktuell }
            $aktuell = [pscustomobject]@{ Start = $eintrag.Datum; Ende = $eintrag.Datum; Anzeigetext = $eintrag.Anzeigetext }
        }
    }
    if ($aktuell) { $zeitraeume += $aktuell }

    return $zeitraeume
}

# ============================================================================
# 4) Outlook-Termine anlegen (Outlook Classic, per COM - kein Graph, kein MSAL)
# ============================================================================
function Sync-OutlookTermine {
    param(
        [Parameter(Mandatory)] [array]$Urlaubszeitraeume
    )

    $outlook = New-Object -ComObject Outlook.Application
    $namespace = $outlook.GetNamespace("MAPI")
    $kalender = $namespace.GetDefaultFolder(9)  # 9 = olFolderCalendar

    $ergebnis = @{ Angelegt = 0; UnveraendertVorhanden = 0 }

    foreach ($zeitraum in $Urlaubszeitraeume) {
        # Eindeutige, stabile Kennung fuer diesen Zeitraum, um Duplikate zu vermeiden.
        $syncMarker = "LOGA-SYNC-ID: {0:yyyy-MM-dd}_{1:yyyy-MM-dd}" -f $zeitraum.Start, $zeitraum.Ende

        $vorhandeneTermine = $kalender.Items
        $vorhandeneTermine.IncludeRecurrences = $false
        $gefunden = $false
        foreach ($termin in $vorhandeneTermine) {
            if ($termin.Body -and $termin.Body.Contains($syncMarker)) {
                $gefunden = $true
                break
            }
        }

        if ($gefunden) {
            $ergebnis.UnveraendertVorhanden++
            continue
        }

        if ($PSCmdlet.ShouldProcess(
                "$($zeitraum.Start.ToString('dd.MM.yyyy')) - $($zeitraum.Ende.ToString('dd.MM.yyyy'))",
                "Outlook-Termin '$Betreff' anlegen")) {

            $termin = $outlook.CreateItem(1)  # 1 = olAppointmentItem
            $termin.Subject = $Betreff
            $termin.Start = $zeitraum.Start.Date
            # Exklusives Enddatum: letzter Urlaubstag + 1 Tag, damit er in Outlook nicht fehlt.
            $termin.End = $zeitraum.Ende.Date.AddDays(1)
            $termin.AllDayEvent = $true
            $termin.BusyStatus = 3       # olOutOfOffice = "Abwesend" (verpflichtend)
            $termin.Sensitivity = 2      # olPrivate
            $termin.ReminderSet = $false
            $termin.Categories = $Kategorie
            $termin.Body = "$($zeitraum.Anzeigetext) (aus LOGA übernommen)`r`n`r`n$syncMarker"
            $termin.Save()

            $ergebnis.Angelegt++
        }
    }

    return $ergebnis
}

# ============================================================================
# 5) Ablauf
# ============================================================================
Write-Host "==> LOGA-Zugangsdaten abfragen (werden nicht gespeichert)..." -ForegroundColor Cyan
$credential = Get-Credential -Message "LOGA-Zugangsdaten (Kennung / Kennwort)"

Write-Host "==> Melde mich bei LOGA an..." -ForegroundColor Cyan
$driver = Connect-Loga -Credential $credential

try {
    Write-Host "==> Lese Urlaubseintraege aus dem Kalender..." -ForegroundColor Cyan
    $von = (Get-Date).Date.AddDays(-$SyncPastDays)
    $bis = (Get-Date).Date.AddDays($SyncFutureDays)
    $urlaube = Get-LogaUrlaubsEintraege -Driver $driver -Von $von -Bis $bis

    if ($urlaube.Count -eq 0) {
        Write-Warning "Keine Urlaubseintraege gefunden. Entweder gibt es aktuell keine, oder die Datumserkennung (siehe Funktion Get-LogaUrlaubsEintraege) muss noch angepasst werden."
    }
    else {
        Write-Host ("==> {0} Urlaubszeitraum/-zeitraeume gefunden:" -f $urlaube.Count) -ForegroundColor Cyan
        $urlaube | ForEach-Object { Write-Host ("    {0:dd.MM.yyyy} - {1:dd.MM.yyyy}  ({2})" -f $_.Start, $_.Ende, $_.Anzeigetext) }
    }
}
finally {
    $driver.Quit()
}

if ($urlaube.Count -gt 0) {
    Write-Host "==> Trage Termine in Outlook ein..." -ForegroundColor Cyan
    $ergebnis = Sync-OutlookTermine -Urlaubszeitraeume $urlaube
    Write-Host ("==> Fertig: {0} neu angelegt, {1} bereits vorhanden." -f $ergebnis.Angelegt, $ergebnis.UnveraendertVorhanden) -ForegroundColor Green
}
