# Administrationsanleitung

## 1. Voraussetzungen auf dem Zielrechner

- Windows 11
- Microsoft Edge (wird von Playwright über `Channel = "msedge"` angesteuert, es wird kein
  eigener Chromium-Browser heruntergeladen)
- Microsoft Outlook Desktop **oder** Zugriff auf einen Microsoft-365-Kalender über Microsoft Graph

## 2. Erstinbetriebnahme

Beim ersten Start (`FirstRunCompleted = false` in den Einstellungen) öffnet sich der
Einrichtungsassistent. Er fragt ab:

| Feld | Beschreibung |
|---|---|
| LOGA-Webadresse | Adresse des LOGA-Portals, vorbelegt mit der bekannten Adresse |
| Benutzername / Passwort | LOGA-Zugangsdaten. Das Passwort wird ausschließlich über den Windows Credential Manager (Eintrag `LOGA-Outlook-Sync`) bzw. DPAPI gespeichert, niemals in einer Konfigurationsdatei |
| Kalenderanbindung | Automatisch / Microsoft Graph / Outlook COM |
| Entra-ID-App-Registrierung | Client-ID für Microsoft Graph, siehe [GRAPH-SETUP.md](GRAPH-SETUP.md) |
| Browser sichtbar/unsichtbar | Debug-Modus (sichtbar) oder Headless-Betrieb |
| Synchronisationszeitraum | Tage rückwirkend / Monate voraus (Standard: 30 Tage / 18 Monate) |
| Terminbezeichnungen | Betreff/Kategorie für Urlaub und Gleitzeit, optionaler Präfix/Suffix |
| Löschverhalten | Automatisch löschen / Vor dem Löschen bestätigen (Standard) / Nur als Konflikt melden |

Über die Schaltflächen können LOGA-Anmeldung, Outlook-Verbindung und der ausgewählte Kalender
sofort getestet werden, bevor der Assistent abgeschlossen wird.

Alle nicht sensiblen Einstellungen werden unter `%LOCALAPPDATA%\LogaOutlookSync\settings.json`
gespeichert. **Änderungen an den Einstellungen werden erst nach einem Neustart der Anwendung
wirksam** (die laufende Sitzung verwendet die beim Start geladenen Werte).

## 3. LOGA-Selektoren ermitteln (erforderlich vor dem ersten produktiven Einsatz)

Die tatsächliche HTML-Struktur von LOGA ist nicht bekannt. Gehen Sie wie folgt vor:

1. Kopieren Sie `config/loga-selectors.example.json` und `config/loga-navigation.example.json`
   nach `%LOCALAPPDATA%\LogaOutlookSync\Config\loga-selectors.json` bzw. `loga-navigation.json`.
2. Aktivieren Sie in den Einstellungen "Browser sichtbar" (Debug-Modus).
3. Starten Sie eine Synchronisation oder den Login-Test und beobachten Sie den Browser.
4. Öffnen Sie parallel die Entwicklertools von Microsoft Edge (F12) auf der echten LOGA-Seite
   und ermitteln Sie stabile Selektoren in dieser Reihenfolge der Bevorzugung:
   1. stabile IDs (`#id`),
   2. ARIA-Rollen (`[role="..."]`),
   3. Labels (`label:has-text("...")`),
   4. sichtbare Texte (`text=...`),
   5. stabile CSS-Klassen,
   6. relativer XPath nur als letzte Möglichkeit (keine absoluten XPath-Ausdrücke).
5. Tragen Sie die ermittelten Selektoren in die JSON-Dateien ein. Jedes Feld ist in der
   Beispieldatei mit `_readme`-Kommentaren und `TODO`-Platzhaltern versehen.
6. Nutzen Sie "LOGA-Anmeldung testen" und danach eine Vorschau/Trockenlauf-Synchronisation,
   um die Kalender-Selektoren zu verifizieren, ohne Outlook-Termine zu verändern.

Schlägt ein Navigationsschritt oder ein Selektor fehl, nennt die Fehlermeldung explizit den
betroffenen Schritt bzw. Selektor, und die Anwendung legt automatisch einen Screenshot sowie
(mit redigierten Passwortfeldern) einen HTML-Snapshot unter
`%LOCALAPPDATA%\LogaOutlookSync\DebugArtifacts\` ab.

## 4. Verzeichnisstruktur zur Laufzeit

```
%LOCALAPPDATA%\LogaOutlookSync\
  settings.json              nicht sensible Einstellungen
  Config\
    loga-selectors.json      LOGA-Selektoren (siehe oben)
    loga-navigation.json     LOGA-Navigationsschritte
    graph-token-cache.bin    verschlüsselter MSAL-Token-Cache (Microsoft Graph)
  Logs\                      rollierende Tagesprotokolle, automatische Rotation
  DebugArtifacts\            Screenshots/HTML-Snapshots bei Fehlern (Passwörter entfernt)
  Diagnostics\               exportierte Diagnosepakete (ZIP, ohne Zugangsdaten)
```

## 5. Erweiterung um weitere Abwesenheitsarten

Das Domänenmodell (`AbsenceType`) enthält bereits Werte für Krankheit, Dienstreise,
Fortbildung, Homeoffice, Rufbereitschaft und Zeitausgleich. Um einen weiteren Typ zu
synchronisieren:

1. In `loga-selectors.json` unter `calendarParsing.typeTextMapping` sicherstellen, dass der
   LOGA-Text auf den gewünschten `AbsenceType`-Wert abgebildet wird (bereits vorbereitet).
2. In `LogaOutlookSync.Domain.AbsenceSyncPolicy.Default` den Typ ergänzen.
3. In `LogaOutlookSync.Application.CalendarSyncItemFactory.Create` die Zuordnung von
   Betreff/Kategorie für den neuen Typ ergänzen (aktuell nur Vacation/FlexTime abgebildet).
