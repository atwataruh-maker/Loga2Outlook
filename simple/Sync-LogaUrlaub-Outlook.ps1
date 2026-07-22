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
    Der Abschnitt "LOGA-Kalender auslesen" klickt jeden Urlaubsbalken einzeln an - das
    oeffnet ein Popup mit den exakten Feldern "Anfangsdatum"/"Endedatum" (siehe Kommentar
    bei Get-LogaUrlaubsEintraege weiter unten). Aktuell wird nur die nach dem Login
    angezeigte Woche gelesen, keine automatische Wochennavigation. Mit -WhatIf laesst
    sich das Skript gefahrlos testen, ohne Outlook-Termine zu veraendern; die erkannten
    Zeitraeume werden dabei trotzdem immer angezeigt.
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

    # Der ganztaegige Kalendereintrag (bestaetigt per "Copy outerHTML"):
    #   <div class="personalWeek-alldayEvent ..." data-cache-id="..." title="Tarifurlaub" ...>
    #     <div class="personalWeek-alldayEvent-eventTitle">Tarifurlaub</div>
    #   </div>
    GanztagEintrag = "div.personalWeek-alldayEvent"

    # Ein Klick auf den Eintrag oeffnet ein Popup mit "Anfangsdatum"/"Endedatum". Bestaetigt
    # per DevTools: <input name="vacationHalfDayServerMaskPart-startDate" ... value="23.07.2026">
    # Ueber "endet mit" (-startDate/-endDate) statt des vollen Namens, falls der Praefix bei
    # anderen Abwesenheitsarten abweicht.
    PopupAnfangsdatum = "input[name`$='-startDate']"
    PopupEndedatum    = "input[name`$='-endDate']"
}

# ============================================================================
# 2) Anmeldung bei LOGA
# ============================================================================

# Einfache Warteschleife statt WebDriverWait.Until(...): vermeidet die in PowerShell
# manchmal unzuverlaessige automatische Umwandlung von Scriptblocks in .NET-Delegaten
# und ist so leichter nachvollziehbar/debuggbar. $By ist z. B.
# [OpenQA.Selenium.By]::XPath("...") oder [OpenQA.Selenium.By]::CssSelector("...").
function Wait-SeElement {
    param(
        [Parameter(Mandatory)] $Driver,
        [Parameter(Mandatory)] $By,
        [int]$TimeoutSeconds = 15
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        try {
            return $Driver.FindElement($By)
        }
        catch [OpenQA.Selenium.NoSuchElementException] {
            Start-Sleep -Milliseconds 300
        }
    } while ((Get-Date) -lt $deadline)

    throw "Element '$By' wurde nach $TimeoutSeconds Sekunden nicht gefunden. " +
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

        $usernameField = Wait-SeElement -Driver $driver -By ([OpenQA.Selenium.By]::XPath($LogaSelectors.BenutzernameFeld))
        $passwordField = $driver.FindElement([OpenQA.Selenium.By]::XPath($LogaSelectors.PasswortFeld))

        $usernameField.SendKeys($Credential.UserName)
        $passwordField.SendKeys($Credential.GetNetworkCredential().Password)

        $driver.FindElement([OpenQA.Selenium.By]::XPath($LogaSelectors.AnmeldenButton)).Click()

        Wait-SeElement -Driver $driver -By ([OpenQA.Selenium.By]::XPath($LogaSelectors.EingeloggtIndikator)) | Out-Null
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
#    Bestaetigt per "Copy outerHTML" aus den Browser-DevTools:
#        <div class="personalWeek-alldayEvent ..." data-cache-id="1e38e3d9..." title="Tarifurlaub"
#             role="button" aria-label="Tarifurlaub" ...>
#          <div class="personalWeek-alldayEvent-eventTitle">Tarifurlaub</div>
#        </div>
#
#    Ein Klick auf diesen Balken oeffnet ein Popup mit exaktem "Anfangsdatum"/"Endedatum"
#    (bestaetigt per Screenshot: Eingabefeld mit
#    name="vacationHalfDayServerMaskPart-startDate", Wert "23.07.2026"). Das ist die
#    zuverlaessigste Datenquelle, die wir bisher kennen - kein Rechnen mit Pixel-Positionen
#    noetig. Ablauf pro Eintrag: anklicken, Popup abwarten, beide Datumsfelder auslesen,
#    Popup mit ESC wieder schliessen, weiter zum naechsten Eintrag.
#
#    Alle Attribute des Balkens selbst (Text, data-cache-id) werden VOR dem Klick
#    ausgelesen, weil GWT nach dem Klick Teile der Seite neu rendern kann und das
#    urspruengliche Element-Objekt dann ungueltig (stale) werden koennte.
# ============================================================================
function Get-LogaUrlaubsEintraege {
    param(
        [Parameter(Mandatory)] $Driver
    )

    $balken = $Driver.FindElements([OpenQA.Selenium.By]::CssSelector($LogaSelectors.GanztagEintrag))

    $kandidaten = @()
    foreach ($el in $balken) {
        $text = $el.GetAttribute("title")
        if (-not $text) { $text = $el.GetAttribute("aria-label") }
        if (-not $text) { $text = $el.Text.Trim() }

        # Nur Eintraege verarbeiten, deren Text auf Urlaub hindeutet (z. B. "Tarifurlaub",
        # "Erholungsurlaub", "Resturlaub", ...). Andere Abwesenheitsarten (Gleitzeit,
        # Krankheit, ...) werden bewusst ignoriert, wie vom Benutzer gewuenscht.
        if ($text -notmatch '(?i)urlaub') {
            continue
        }

        $kandidaten += [pscustomobject]@{
            Element = $el
            Text    = $text
            SyncId  = $el.GetAttribute("data-cache-id")
        }
    }

    Write-Verbose ("{0} Urlaubsbalken in der aktuell sichtbaren Ansicht gefunden." -f $kandidaten.Count)

    $eintraege = @()
    foreach ($kandidat in $kandidaten) {
        try {
            $kandidat.Element.Click()

            $startFeld = Wait-SeElement -Driver $Driver -By ([OpenQA.Selenium.By]::CssSelector($LogaSelectors.PopupAnfangsdatum)) -TimeoutSeconds 10
            $endeFeld = $Driver.FindElement([OpenQA.Selenium.By]::CssSelector($LogaSelectors.PopupEndedatum))

            $startText = $startFeld.GetAttribute("value")
            $endeText = $endeFeld.GetAttribute("value")

            $start = [datetime]::ParseExact($startText, "dd.MM.yyyy", [System.Globalization.CultureInfo]::InvariantCulture)
            $ende = [datetime]::ParseExact($endeText, "dd.MM.yyyy", [System.Globalization.CultureInfo]::InvariantCulture)

            $syncId = $kandidat.SyncId
            if (-not $syncId) { $syncId = "fallback_$($start.ToString('yyyyMMdd'))_$($ende.ToString('yyyyMMdd'))_$($kandidat.Text)" }

            Write-Verbose ("Gefunden: '{0}' -> {1:dd.MM.yyyy} - {2:dd.MM.yyyy} (SyncId: {3})" -f $kandidat.Text, $start, $ende, $syncId)

            $eintraege += [pscustomobject]@{
                Start       = $start
                Ende        = $ende
                Anzeigetext = $kandidat.Text
                SyncId      = $syncId
            }
        }
        catch {
            Write-Warning ("Eintrag '{0}' konnte nicht ausgelesen werden: {1}" -f $kandidat.Text, $_.Exception.Message)
        }
        finally {
            # Popup wieder schliessen, bevor der naechste Eintrag angeklickt wird.
            try {
                $Driver.FindElement([OpenQA.Selenium.By]::TagName("body")).SendKeys([OpenQA.Selenium.Keys]::Escape)
                Start-Sleep -Milliseconds 300
            }
            catch {
                # Popup war vermutlich schon geschlossen - ignorieren.
            }
        }
    }

    return $eintraege
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
        # Stabile Kennung fuer diesen Eintrag (data-cache-id aus LOGA, oder ein Ersatzwert),
        # um bei erneutem Lauf keine Duplikate anzulegen.
        $syncMarker = "LOGA-SYNC-ID: $($zeitraum.SyncId)"

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
    # HINWEIS ZUM AKTUELLEN STAND: Die automatische Navigation zwischen Kalenderwochen
    # (fuer -SyncPastDays/-SyncFutureDays ueber mehrere Wochen hinweg) ist noch nicht
    # eingebaut, da die Selektoren fuer "naechste/vorherige Woche" noch nicht bestaetigt
    # sind. Dieses Skript liest daher vorerst NUR die Woche, die der Kalender direkt nach
    # dem Login anzeigt (i. d. R. die aktuelle Woche).
    Write-Host "==> Lese Urlaubseintraege der aktuell angezeigten Woche..." -ForegroundColor Cyan

    $urlaube = Get-LogaUrlaubsEintraege -Driver $driver

    if ($urlaube.Count -eq 0) {
        Write-Warning "Keine Urlaubseintraege in der aktuell angezeigten Woche gefunden."
    }
    else {
        Write-Host ("==> {0} Urlaubszeitraum/-zeitraeume gefunden:" -f $urlaube.Count) -ForegroundColor Cyan
        $urlaube | ForEach-Object { Write-Host ("    {0:dd.MM.yyyy} - {1:dd.MM.yyyy}  ({2})  [SyncId: {3}]" -f $_.Start, $_.Ende, $_.Anzeigetext, $_.SyncId) }
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
