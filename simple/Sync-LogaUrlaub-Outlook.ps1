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
    WICHTIG: Der Abschnitt "LOGA-Kalender auslesen" berechnet das Datum eines Eintrags aus
    seiner Pixel-Position in der Wochenansicht (siehe Kommentar bei
    Get-LogaUrlaubsEintraegeDerAngezeigtenWoche weiter unten) - LOGA liefert das Datum nicht
    als Attribut. Aktuell wird nur die nach dem Login angezeigte Woche gelesen, keine
    automatische Wochennavigation. Mit -WhatIf laesst sich das Skript gefahrlos testen,
    ohne Outlook-Termine zu veraendern; die erkannten Zeitraeume werden dabei trotzdem
    immer angezeigt, damit man sie gegen den Browser pruefen kann.
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
    #   <div class="personalWeek-alldayEvent ..." data-cache-id="..." title="Tarifurlaub" ...
    #        style="...left: 306px; right: 162px;">
    #     <div class="personalWeek-alldayEvent-eventTitle">Tarifurlaub</div>
    #   </div>
    GanztagEintrag = "div.personalWeek-alldayEvent"

    # Container, dessen Breite die volle 7-Tage-Woche (Montag-Sonntag) abbildet. Die
    # left/right-Werte der Eintraege sind relativ zu diesem Element zu verstehen (naechster
    # Vorfahre mit position:relative laut DOM-Struktur). NICHT abschliessend bestaetigt.
    WochenBreiteContainer = "div.personalWeek-scrollableAlldayEventsArea > div"
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
#    Bestaetigt per "Copy outerHTML" aus den Browser-DevTools:
#        <div class="personalWeek-alldayEvent ..." data-cache-id="1e38e3d9..." title="Tarifurlaub"
#             role="button" aria-label="Tarifurlaub"
#             style="...; top: 3px; left: 306px; right: 162px;">
#          <div class="personalWeek-alldayEvent-eventTitle">Tarifurlaub</div>
#        </div>
#
#    WICHTIGE EINSCHRAENKUNG: Dieses LOGA-Kalenderwidget (GWT-basiert, erkennbar an
#    "gwt-InlineHTML") legt das Datum eines Eintrags NICHT in einem Attribut ab, sondern
#    ausschliesslich ueber die Pixel-Position (links/rechts) relativ zur sichtbaren
#    7-Tage-Woche (Montag bis Sonntag). "data-cache-id" ist zwar eine stabile, eindeutige
#    Kennung fuer Duplikat-Erkennung, enthaelt aber selbst kein Datum.
#
#    Diese Funktion berechnet das Datum daher aus der Position: Sie liest die Breite des
#    Wochen-Containers (siehe $LogaSelectors.WochenBreiteContainer), teilt sie durch 7 und
#    ordnet jeden Eintrag anhand seiner Position dem entsprechenden Wochentag zu. Das
#    Ergebnis wird IMMER ausgegeben (auch bei -WhatIf), damit man es gegen die sichtbare
#    Kalenderwoche pruefen kann, bevor irgendetwas in Outlook geschrieben wird.
#
#    $WochenMontag muss das Datum des Montags der Woche sein, die der Browser GERADE
#    anzeigt (das Skript liest dieses Datum nicht selbst von der Seite ab, sondern
#    verwendet den Wert, den der Aufrufer via -Von/-Bis bzw. Wochennavigation vorgibt -
#    siehe Abschnitt 5).
# ============================================================================
function Get-LogaUrlaubsEintraegeDerAngezeigtenWoche {
    param(
        [Parameter(Mandatory)] $Driver,
        [Parameter(Mandatory)] [datetime]$WochenMontag
    )

    $container = $Driver.FindElement([OpenQA.Selenium.By]::CssSelector($LogaSelectors.WochenBreiteContainer))
    $containerLinks = $container.Location.X
    $containerBreite = $container.Size.Width
    $spaltenBreite = $containerBreite / 7.0

    Write-Verbose ("Wochen-Container: Breite={0}px, Spaltenbreite={1:N1}px, Montag={2:dd.MM.yyyy}" -f $containerBreite, $spaltenBreite, $WochenMontag)

    $eintraege = @()
    $elemente = $Driver.FindElements([OpenQA.Selenium.By]::CssSelector($LogaSelectors.GanztagEintrag))

    foreach ($el in $elemente) {
        $text = $el.GetAttribute("title")
        if (-not $text) { $text = $el.GetAttribute("aria-label") }
        if (-not $text) { $text = $el.Text.Trim() }

        # Nur Eintraege verarbeiten, deren Text auf Urlaub hindeutet (z. B. "Tarifurlaub",
        # "Erholungsurlaub", "Resturlaub", ...). Andere Abwesenheitsarten (Gleitzeit,
        # Krankheit, ...) werden bewusst ignoriert, wie vom Benutzer gewuenscht.
        if ($text -notmatch '(?i)urlaub') {
            continue
        }

        $relativLinks = $el.Location.X - $containerLinks
        $relativRechts = $relativLinks + $el.Size.Width

        # +/- 0.1 Spalten Toleranz gegen Rundungsfehler an den Spaltengrenzen.
        $startTagIndex = [Math]::Floor(($relativLinks / $spaltenBreite) + 0.1)
        $endTagIndex = [Math]::Ceiling(($relativRechts / $spaltenBreite) - 0.1) - 1

        $startTagIndex = [Math]::Max(0, [Math]::Min(6, $startTagIndex))
        $endTagIndex = [Math]::Max($startTagIndex, [Math]::Min(6, $endTagIndex))

        $start = $WochenMontag.Date.AddDays($startTagIndex)
        $ende = $WochenMontag.Date.AddDays($endTagIndex)

        $syncId = $el.GetAttribute("data-cache-id")
        if (-not $syncId) { $syncId = "geo_$($start.ToString('yyyyMMdd'))_$($ende.ToString('yyyyMMdd'))_$text" }

        Write-Verbose ("Gefunden: '{0}' -> links={1}px rechts={2}px -> Tag {3}-{4} -> {5:dd.MM.yyyy}-{6:dd.MM.yyyy}" -f `
            $text, $relativLinks, $relativRechts, $startTagIndex, $endTagIndex, $start, $ende)

        $eintraege += [pscustomobject]@{
            Start       = $start
            Ende        = $ende
            Anzeigetext = $text
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
    # HINWEIS ZUM AKTUELLEN STAND: Die automatische Navigation zwischen Kalenderwochen
    # (fuer -SyncPastDays/-SyncFutureDays ueber mehrere Wochen hinweg) ist noch nicht
    # eingebaut, da die Selektoren fuer "naechste/vorherige Woche" noch nicht bestaetigt
    # sind. Dieses Skript liest daher vorerst NUR die Woche, die der Kalender direkt nach
    # dem Login anzeigt (i. d. R. die aktuelle Woche). $WochenMontag wird aus dem heutigen
    # Datum berechnet - falls LOGA nach dem Login eine andere Woche zeigt, bitte melden.
    Write-Host "==> Lese Urlaubseintraege der aktuell angezeigten Woche..." -ForegroundColor Cyan

    $heute = (Get-Date).Date
    $wochenMontag = $heute.AddDays(-(([int]$heute.DayOfWeek + 6) % 7))  # Montag dieser Woche

    $urlaube = Get-LogaUrlaubsEintraegeDerAngezeigtenWoche -Driver $driver -WochenMontag $wochenMontag

    if ($urlaube.Count -eq 0) {
        Write-Warning "Keine Urlaubseintraege in der aktuell angezeigten Woche gefunden."
    }
    else {
        Write-Host ("==> {0} Urlaubszeitraum/-zeitraeume gefunden (bitte gegen das im Browser sichtbare Datum pruefen!):" -f $urlaube.Count) -ForegroundColor Cyan
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
