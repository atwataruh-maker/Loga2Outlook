#Requires -Version 5.1
<#
    .SYNOPSIS
    Veroeffentlicht LOGA Outlook Sync als portablen, self-contained Windows-x64-Programmordner.

    .DESCRIPTION
    Eine Single-File-Veroeffentlichung wurde bewusst NICHT gewaehlt: Microsoft.Playwright
    benoetigt zur Laufzeit einen separaten Treiberprozess (Node-basiert) samt Begleitdateien
    unterhalb eines ".playwright"-Unterordners im Ausgabeverzeichnis. Eine Single-File-EXE
    wuerde diese Dateien nicht zuverlaessig mitbuendeln bzw. zur Laufzeit nicht wiederfinden.
    Stabilitaet hat hier Vorrang vor einer einzelnen Datei - das Ergebnis ist ein vollstaendiger,
    portabler Ordner, der ohne Installation und ohne Administratorrechte gestartet werden kann.
#>
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = (Join-Path $PSScriptRoot "..\artifacts\portable")
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$appProject = Join-Path $root "src\LogaOutlookSync.App\LogaOutlookSync.App.csproj"

& (Join-Path $PSScriptRoot "build-portable.ps1") -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build/Tests vor der Veroeffentlichung sind fehlgeschlagen." }

if (Test-Path $OutputDir) {
    Write-Host "==> Entferne vorhandenen Ausgabeordner $OutputDir..." -ForegroundColor Cyan
    Remove-Item $OutputDir -Recurse -Force
}

Write-Host "==> Veroeffentliche portablen, self-contained Ordner fuer $Runtime..." -ForegroundColor Cyan
dotnet publish $appProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=false `
    -o $OutputDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish ist fehlgeschlagen." }

Write-Host "==> Pruefe Vollstaendigkeit der veroeffentlichten Dateien..." -ForegroundColor Cyan

$requiredFiles = @(
    "LogaOutlookSync.exe",
    "LogaOutlookSync.dll",
    "Microsoft.Playwright.dll"
)

$missingFiles = @()
foreach ($file in $requiredFiles) {
    $path = Join-Path $OutputDir $file
    if (-not (Test-Path $path)) {
        $missingFiles += $file
    }
}

if ($missingFiles.Count -gt 0) {
    throw "Folgende erwartete Dateien fehlen im veroeffentlichten Ordner: $($missingFiles -join ', ')"
}

$playwrightDriverDir = Join-Path $OutputDir ".playwright"
if (-not (Test-Path $playwrightDriverDir)) {
    Write-Warning ("Der '.playwright'-Ordner mit dem Playwright-Treiber wurde nicht gefunden. " + `
        "Ohne diesen Ordner kann die Browserautomatisierung nicht starten. " + `
        "Bitte pruefen Sie, ob das Microsoft.Playwright-NuGet-Paket korrekt wiederhergestellt wurde.")
}

Write-Host "==> Kopiere Beispielkonfiguration..." -ForegroundColor Cyan
$configDest = Join-Path $OutputDir "config"
New-Item -ItemType Directory -Path $configDest -Force | Out-Null
Copy-Item (Join-Path $root "config\*.example.json") $configDest -Force

Write-Host "==> Pruefe Startfaehigkeit der portablen Ausgabe (--version)..." -ForegroundColor Cyan
$exePath = Join-Path $OutputDir "LogaOutlookSync.exe"
try {
    & $exePath --version 2>$null | Out-Null
}
catch {
    Write-Warning "Die Startfaehigkeitspruefung konnte nicht vollstaendig durchgefuehrt werden (WPF-Anwendungen unterstuetzen ggf. keine --version-Option). Bitte die Anwendung manuell einmal starten."
}

Write-Host ""
Write-Host "==> Portable Anwendung veroeffentlicht unter: $OutputDir" -ForegroundColor Green
Write-Host "    Starten Sie 'LogaOutlookSync.exe' in diesem Ordner - keine Installation erforderlich." -ForegroundColor Green
