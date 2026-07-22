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
    LOGA bietet ein Widget "Urlaubsuebersicht" (per Drag-and-Drop aus dem Werkzeug-Menue
    in den Kalenderbereich gezogen), das nach Auswahl von "Urlaub" im dortigen Dropdown
    eine einfache Tabelle mit ALLEN Urlaubszeitraeumen des Jahres zeigt (Datum, Status
    "Genommen"/"Genehmigt", Tage) - viel zuverlaessiger als einzelne Kalenderbalken
    anzuklicken oder Positionen zu berechnen. Da dieses Widget nicht dauerhaft im
    Kalender verbleibt (jedes Mal neu per Drag-and-Drop noetig) und Drag-and-Drop sich
    per Selenium nicht zuverlaessig automatisieren laesst, pausiert das Skript kurz und
    bittet um diesen einen manuellen Schritt (siehe Abschnitt 5) - danach liest es die
    Tabelle vollautomatisch aus. Erfordert daher IMMER einen sichtbaren Browser
    (-Headless kann hier nicht verwendet werden). Mit -WhatIf laesst sich das Skript
    gefahrlos testen, ohne Outlook-Termine zu veraendern; die erkannten Zeitraeume
    werden dabei trotzdem immer angezeigt.
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
    [string]$EdgeDriverPath = (Join-Path $PSScriptRoot "msedgedriver.exe")
)

$ErrorActionPreference = "Stop"

# ============================================================================
# 1) LOGA-Selektoren (zentral an einer Stelle, keine Verteilung im Code)
#    Basierend auf den bereitgestellten Screenshots des Anmeldeformulars und der
#    Urlaubsuebersicht-Tabelle.
# ============================================================================
$LogaSelectors = @{
    # Relative XPath-Ausdruecke (kein absoluter Pfad), gestuetzt auf die sichtbaren
    # Feldbeschriftungen "Kennung" / "Kennwort" - robuster als geratene CSS-Klassen.
    BenutzernameFeld = "//label[normalize-space(.)='Kennung']/following::input[1]"
    PasswortFeld     = "//label[normalize-space(.)='Kennwort']/following::input[1]"
    AnmeldenButton   = "//button[contains(., 'ANMELDEN') or contains(., 'Anmelden')]"

    # Element, das nach erfolgreichem Login sicher sichtbar ist (zur Erfolgspruefung).
    EingeloggtIndikator = "//*[contains(text(),'Kalendarium')]"

    # Ankertext im Titel des manuell geoeffneten Urlaubsuebersicht-Popups.
    UrlaubsuebersichtTitel = "//*[contains(text(),'Urlaubsübersicht')]"

    # Umschliessender Popup-Container (dieselbe Wrapper-Klasse wie beim Datums-Popup
    # eines einzelnen Kalendereintrags - NICHT fuer dieses Popup einzeln bestaetigt).
    PopupContainerKlasse = "popupContent"
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

    # Immer sichtbar: der Benutzer muss die Urlaubsuebersicht manuell per
    # Drag-and-Drop oeffnen (siehe Abschnitt 5), das erfordert ein sichtbares Fenster.
    $edgeOptions = New-Object OpenQA.Selenium.Edge.EdgeOptions

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
# 3) Urlaubsuebersicht auslesen
#
#    Setzt voraus, dass der Benutzer die Urlaubsuebersicht-Tabelle bereits manuell
#    geoeffnet hat (Werkzeug-Symbol -> "Urlaubsuebersicht" in den Kalenderbereich
#    ziehen -> im Popup "Urlaub" im Dropdown auswaehlen). Siehe Abschnitt 5.
#
#    Bestaetigt per DevTools-Screenshot enthaelt jede Datenzeile Zellen mit den
#    stabilen Klassen "LG-InputLabel Cell Von" (Startdatum, z. B. "05.02."),
#    "LG-InputLabel Cell Bis" (Enddatum) und "LG-InputLabel Cell Text" (Status,
#    z. B. "Genommen"/"Genehmigt"). Die zusaetzlich sichtbaren id="LGLabel203" o. Ae.
#    sind vermutlich bei jedem Rendern neu vergeben und werden bewusst NICHT verwendet.
#
#    Statt jede Zelle einzeln per Selektor abzufragen (fehleranfaellig, falls sich die
#    genaue Tabellenverschachtelung unterscheidet), wird der gesamte sichtbare Text des
#    Popups gelesen und zeilenuebergreifend per regulaerem Ausdruck geparst - das ist
#    robuster gegenueber kleineren Strukturunterschieden. Muster pro Datenzeile:
#    "05.02. - 06.02.  Genommen   2.00". Das Jahr steht nicht in jeder Zeile, sondern
#    einmal pro Jahresblock (z. B. "2026 Resturlaub Vorjahr ..."); jedem Datumseintrag
#    wird daher das zuletzt zuvor im Text vorkommende 4-stellige Jahr zugeordnet.
# ============================================================================
function Get-LogaUrlaubsuebersicht {
    param(
        [Parameter(Mandatory)] $Driver
    )

    $titelElement = Wait-SeElement -Driver $Driver -By ([OpenQA.Selenium.By]::XPath($LogaSelectors.UrlaubsuebersichtTitel)) -TimeoutSeconds 120
    $popup = $titelElement.FindElement([OpenQA.Selenium.By]::XPath("ancestor::div[contains(@class,'$($LogaSelectors.PopupContainerKlasse)')][1]"))

    $text = $popup.Text
    Write-Verbose "----- Text der Urlaubsuebersicht (zur Kontrolle) -----"
    Write-Verbose $text
    Write-Verbose "-------------------------------------------------------"

    $jahrTreffer = [regex]::Matches($text, '\b(20\d{2})\b')
    $zeilenTreffer = [regex]::Matches(
        $text,
        '(?<start>\d{2}\.\d{2})\.\s*-\s*(?<ende>\d{2}\.\d{2})\.\s*(?<status>Genommen|Genehmigt)\s*(?<tage>[\d.,]+)'
    )

    if ($zeilenTreffer.Count -eq 0) {
        Write-Warning ("Es konnten keine Urlaubszeilen im Popup-Text erkannt werden. Bitte pruefen, " +
            "ob im Dropdown des Popups tatsaechlich 'Urlaub' ausgewaehlt ist. Mit -Verbose wird der " +
            "gelesene Text oben angezeigt.")
    }

    $eintraege = @()
    foreach ($treffer in $zeilenTreffer) {
        $jahrMatch = $jahrTreffer | Where-Object { $_.Index -le $treffer.Index } | Select-Object -Last 1
        if (-not $jahrMatch) {
            Write-Warning ("Kein Jahr fuer den Eintrag '{0}' gefunden - wird uebersprungen." -f $treffer.Value)
            continue
        }
        $jahr = [int]$jahrMatch.Value

        $startTeile = $treffer.Groups['start'].Value -split '\.'
        $endeTeile = $treffer.Groups['ende'].Value -split '\.'

        $start = [datetime]::new($jahr, [int]$startTeile[1], [int]$startTeile[0])
        $ende = [datetime]::new($jahr, [int]$endeTeile[1], [int]$endeTeile[0])
        if ($ende -lt $start) {
            # Zeitraum ueber den Jahreswechsel hinweg (z. B. 29.12. - 02.01.).
            $ende = $ende.AddYears(1)
        }

        $syncId = "urlaubsuebersicht_$($start.ToString('yyyyMMdd'))_$($ende.ToString('yyyyMMdd'))"

        Write-Verbose ("Gefunden: {0:dd.MM.yyyy} - {1:dd.MM.yyyy}  {2}  ({3} Tage)  [SyncId: {4}]" -f `
            $start, $ende, $treffer.Groups['status'].Value, $treffer.Groups['tage'].Value, $syncId)

        $eintraege += [pscustomobject]@{
            Start       = $start
            Ende        = $ende
            Anzeigetext = "Urlaub ($($treffer.Groups['status'].Value))"
            SyncId      = $syncId
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
    Write-Host ""
    Write-Host "==> Manueller Zwischenschritt erforderlich:" -ForegroundColor Yellow
    Write-Host "    1. Im Browser das Werkzeug-Symbol unten rechts anklicken." -ForegroundColor Yellow
    Write-Host "    2. 'Urlaubsuebersicht' in den Kalender-Wochenbereich ziehen." -ForegroundColor Yellow
    Write-Host "    3. Im sich oeffnenden Popup im Dropdown 'Urlaub' auswaehlen." -ForegroundColor Yellow
    Write-Host ""
    Read-Host "    Wenn die Tabelle mit den Urlaubszeitraeumen sichtbar ist, hier Enter druecken" | Out-Null

    Write-Host "==> Lese Urlaubsuebersicht..." -ForegroundColor Cyan
    # @(...) erzwingt ein Array, auch wenn 0 oder 1 Eintraege gefunden werden - PowerShell
    # wandelt eine leere Ergebnismenge sonst stillschweigend in $null um, was .Count
    # unzuverlaessig machen wuerde.
    $alleUrlaube = @(Get-LogaUrlaubsuebersicht -Driver $driver)

    $von = (Get-Date).Date.AddDays(-$SyncPastDays)
    $bis = (Get-Date).Date.AddDays($SyncFutureDays)
    $urlaube = @($alleUrlaube | Where-Object { $_.Ende -ge $von -and $_.Start -le $bis })

    if ($urlaube.Count -eq 0) {
        Write-Warning "Keine Urlaubszeitraeume im konfigurierten Synchronisationszeitraum gefunden."
    }
    else {
        Write-Host ("==> {0} von {1} gefundenen Urlaubszeitraeumen liegen im Synchronisationszeitraum ({2:dd.MM.yyyy} - {3:dd.MM.yyyy}):" -f `
            $urlaube.Count, $alleUrlaube.Count, $von, $bis) -ForegroundColor Cyan
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
