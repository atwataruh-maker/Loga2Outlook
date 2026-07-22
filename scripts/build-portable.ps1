#Requires -Version 5.1
<#
    .SYNOPSIS
    Restaurt, baut und testet die LOGA Outlook Sync Solution.
    Erzeugt keine Veröffentlichung - siehe publish-portable.ps1 dafür.
#>
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$solution = Join-Path $root "LogaOutlookSync.sln"

Write-Host "==> Stelle NuGet-Pakete wieder her..." -ForegroundColor Cyan
dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw "dotnet restore ist fehlgeschlagen." }

Write-Host "==> Baue Solution ($Configuration)..." -ForegroundColor Cyan
dotnet build $solution -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet build ist fehlgeschlagen." }

Write-Host "==> Fuehre Unit-Tests aus..." -ForegroundColor Cyan
dotnet test (Join-Path $root "tests\LogaOutlookSync.UnitTests\LogaOutlookSync.UnitTests.csproj") -c $Configuration --no-build
if ($LASTEXITCODE -ne 0) { throw "Unit-Tests sind fehlgeschlagen." }

Write-Host "==> Fuehre Integrationstests aus..." -ForegroundColor Cyan
dotnet test (Join-Path $root "tests\LogaOutlookSync.IntegrationTests\LogaOutlookSync.IntegrationTests.csproj") -c $Configuration --no-build
if ($LASTEXITCODE -ne 0) { throw "Integrationstests sind fehlgeschlagen." }

Write-Host "==> Build erfolgreich abgeschlossen." -ForegroundColor Green
