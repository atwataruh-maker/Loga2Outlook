# LOGA Outlook Sync

Portable Windows-Anwendung, die genehmigte Urlaubs- und Gleitzeiteinträge aus dem
persönlichen Kalender des LOGA-Webportals ausliest und mit dem Outlook-/Microsoft-365-Kalender
synchronisiert. Die Synchronisation wird ausschließlich manuell gestartet.

## Wichtiger Hinweis zum aktuellen Stand

Die genaue HTML-Struktur und die konkreten Selektoren des LOGA-Webportals
(`https://dedalus.pi-asp.de/loga3/...`) sind **noch nicht bekannt** und wurden bewusst nicht
erfunden. Bevor die Anwendung produktiv genutzt werden kann, müssen die Dateien
`config/loga-selectors.example.json` und `config/loga-navigation.example.json` nach einer
Untersuchung der echten LOGA-Seite mit echten Werten ausgefüllt werden (siehe
[ADMIN-GUIDE.md](ADMIN-GUIDE.md), Abschnitt "LOGA-Selektoren ermitteln"). Alle davon
unabhängigen Komponenten (Domänenmodell, Sicherheitsschicht, Kalenderanbindung, Synchronisations-
logik, Oberfläche) sind vollständig implementiert und getestet.

## Funktionsumfang

1. Öffnet das LOGA-Webportal über Microsoft Edge (Playwright) und meldet sich an.
2. Öffnet den persönlichen Kalender und liest genehmigte Urlaubs- und Gleitzeiteinträge.
3. Gleicht diese mit dem Outlook-/Microsoft-365-Kalender ab (Microsoft Graph oder Outlook COM).
4. Erstellt, aktualisiert oder entfernt die entsprechenden Outlook-Termine, ohne Duplikate und
   ohne jemals manuell erstellte Termine zu berühren.
5. Zeigt eine verständliche Zusammenfassung der Synchronisation an.

Jeder synchronisierte Termin wird verpflichtend als **"Abwesend"** gespeichert
(Microsoft Graph: `showAs = "oof"`, Outlook COM: `BusyStatus = olOutOfOffice`) und nach dem
Schreiben verifiziert; schlägt die Verifikation fehl, wird eine Warnung angezeigt.

## Architektur

```
LogaOutlookSync.sln
src/
  LogaOutlookSync.Domain          Abwesenheits-/Kalender-Domänenmodell, keine Abhängigkeiten
  LogaOutlookSync.Application     Synchronisationsplanung und -orchestrierung
  LogaOutlookSync.Infrastructure  Einstellungen, Pfade, Logging, Diagnosepaket
  LogaOutlookSync.Loga            Playwright-Browserautomatisierung + HTML-Parser
  LogaOutlookSync.Calendar        ICalendarProvider: Microsoft Graph + Outlook COM
  LogaOutlookSync.Security        Windows Credential Manager + DPAPI-Fallback
  LogaOutlookSync.App             WPF-Oberfläche (Dashboard, Sync, Einstellungen, Protokoll)
tests/
  LogaOutlookSync.UnitTests
  LogaOutlookSync.IntegrationTests
config/
  loga-selectors.example.json
  loga-navigation.example.json
scripts/
  build-portable.ps1
  publish-portable.ps1
docs/
.github/
  workflows/
    build-portable.yml        baut die portable Version automatisiert über GitHub Actions
```

Details zur Architektur und den Design-Entscheidungen siehe [ADMIN-GUIDE.md](ADMIN-GUIDE.md).

## Build

Voraussetzung: .NET 8 SDK unter Windows.

```powershell
scripts\build-portable.ps1
```

Baut die Solution, führt Unit- und Integrationstests aus.

## Portable Veröffentlichung

```powershell
scripts\publish-portable.ps1
```

Erzeugt einen vollständigen, self-contained Programmordner unter `artifacts\portable\`
(Standardmäßig für `win-x64`). Eine Single-File-Veröffentlichung wurde bewusst **nicht**
gewählt, da Microsoft.Playwright zur Laufzeit einen separaten Treiberprozess samt
Begleitdateien benötigt (siehe Kommentar im Skript). Die Anwendung erfordert:

- keine Installation (portabler Ordner, einfach kopieren und `LogaOutlookSync.exe` starten),
- keine Administratorrechte,
- keine lokal installierte .NET-Laufzeit (self-contained),
- weder Visual Studio noch ein .NET SDK auf dem Zielrechner,
- keine Python-Installation.

## Portable Version über GitHub Actions bauen

Wer keinen Windows-Rechner mit .NET 8 SDK zur Hand hat, kann die portable Version stattdessen
automatisiert über GitHub Actions bauen lassen:

1. Das Repository auf GitHub öffnen.
2. Den Bereich **Actions** öffnen.
3. Den Workflow **"Portable Windows App bauen"** auswählen.
4. **"Run workflow"** anklicken.
5. Nach erfolgreichem Lauf das Artefakt **`Loga2Outlook-win-x64`** herunterladen.
6. Die heruntergeladene ZIP-Datei vollständig entpacken.
7. `LogaOutlookSync.exe` im entpackten Ordner starten.

Hinweise:

- Auf dem Zielrechner ist **keine .NET-Installation** nötig - der Workflow baut die Anwendung
  self-contained.
- Es sind **keine Administratorrechte** nötig, weder für den Workflow-Lauf noch für die
  anschließende Ausführung der entpackten Anwendung.
- Der **gesamte entpackte Ordner muss zusammenbleiben** (u. a. der `.playwright`-Unterordner
  mit dem Playwright-Treiber sowie alle DLLs) - nicht nur `LogaOutlookSync.exe` verschieben.
- Die **Outlook-COM-Anbindung funktioniert nur mit Outlook Classic** (Desktop), nicht mit dem
  neuen Outlook oder rein browserbasiertem Outlook im Web.
- Die konkreten **LOGA-Selektoren müssen weiterhin separat konfiguriert werden** (siehe
  [ADMIN-GUIDE.md](ADMIN-GUIDE.md), Abschnitt "LOGA-Selektoren ermitteln") - der Workflow baut
  nur die Anwendung, er kennt die LOGA-Seitenstruktur nicht.

Der Workflow (`.github/workflows/build-portable.yml`) ruft ausschließlich das vorhandene
`scripts/publish-portable.ps1` auf - es gibt keine zusätzliche, parallele Build-Logik. Er
führt dabei automatisch auch die Unit- und Integrationstests aus (über
`scripts/build-portable.ps1`, das von `publish-portable.ps1` aufgerufen wird) und schlägt
fehl, wenn Tests fehlschlagen oder `artifacts/portable/LogaOutlookSync.exe` nach dem Build
nicht existiert.

## Erstinbetriebnahme

Beim ersten Start erscheint der Einrichtungsassistent. Details siehe
[ADMIN-GUIDE.md](ADMIN-GUIDE.md).

## Weitere Dokumentation

- [ADMIN-GUIDE.md](ADMIN-GUIDE.md) - Einrichtung, Konfiguration, LOGA-Selektoren ermitteln
- [TROUBLESHOOTING.md](TROUBLESHOOTING.md) - Fehlerdiagnose
- [SECURITY.md](SECURITY.md) - Sicherheitskonzept
- [GRAPH-SETUP.md](GRAPH-SETUP.md) - Microsoft-Graph-App-Registrierung einrichten
- [OUTLOOK-COM-FALLBACK.md](OUTLOOK-COM-FALLBACK.md) - Outlook-COM-Fallback einrichten
