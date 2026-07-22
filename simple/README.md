# Einfache Variante: Ein PowerShell-Skript

Diese Variante ist bewusst **unabhängig** vom großen .NET/WPF-Projekt in `src/` und
löst genau eine Aufgabe: **genehmigten Urlaub aus LOGA in den Outlook-Kalender
übernehmen.** Kein Build, kein NuGet, keine GitHub Actions, kein Visual Studio.

## Einmalige Einrichtung (ca. 5 Minuten)

1. **PowerShell-Modul installieren** (einmalig, keine Adminrechte nötig):
   ```powershell
   Install-Module Selenium -Scope CurrentUser
   ```
   Bei Rückfrage "Untrusted repository" mit `Y` (Ja) bestätigen, bzw. vorher
   `Set-PSRepository -Name PSGallery -InstallationPolicy Trusted` ausführen.

2. **Edge WebDriver herunterladen.** Zuerst die eigene Edge-Version prüfen: Edge
   öffnen → `edge://settings/help`. Dann die passende `msedgedriver.exe` laden von:
   https://developer.microsoft.com/microsoft-edge/tools/webdriver/
   Die Datei `msedgedriver.exe` in denselben Ordner wie `Sync-LogaUrlaub-Outlook.ps1`
   legen.

3. Outlook Classic (Desktop) muss installiert und einmal eingerichtet sein.

## Start

Rechtsklick auf `Sync-LogaUrlaub-Outlook.ps1` → **"Mit PowerShell ausführen"**,
oder in einem PowerShell-Fenster in diesem Ordner:

```powershell
.\Sync-LogaUrlaub-Outlook.ps1
```

Zum gefahrlosen Testen (es werden keine Outlook-Termine angelegt/verändert, die
erkannten Zeiträume werden aber trotzdem angezeigt):

```powershell
.\Sync-LogaUrlaub-Outlook.ps1 -WhatIf
```

Für ausführliche Diagnoseausgaben (jeder gefundene Eintrag mit Rohwerten):

```powershell
.\Sync-LogaUrlaub-Outlook.ps1 -WhatIf -Verbose
```

Das Skript fragt bei jedem Start nach Benutzername/Kennwort (über den normalen
Windows-Anmeldedialog) - es werden **keine Zugangsdaten im Skript oder auf der
Festplatte gespeichert.**

**Wichtig:** Während des Laufs pausiert das Skript einmal kurz und bittet um einen
manuellen Schritt im Browser (siehe unten) - `-Headless` kann daher nicht verwendet
werden, es wird immer ein sichtbares Browserfenster benötigt.

## Ablauf beim Ausführen

1. Skript fragt nach LOGA-Zugangsdaten und meldet sich an.
2. Das Skript pausiert und bittet um folgenden manuellen Schritt im Browserfenster:
   1. Werkzeug-Symbol unten rechts im Kalender anklicken.
   2. "Urlaubsübersicht" per Drag-and-Drop in den Wochenbereich ziehen.
   3. Im sich öffnenden Popup im Dropdown "Urlaub" auswählen, sodass die Tabelle mit
      allen Urlaubszeiträumen des Jahres erscheint.
   4. Zurück im PowerShell-Fenster Enter drücken.
3. Das Skript liest die komplette Tabelle automatisch aus und trägt die im
   Synchronisationszeitraum liegenden Zeiträume in Outlook ein.

Dieser manuelle Zwischenschritt ist bewusst so gelöst: Das Widget "Urlaubsübersicht"
bleibt nicht dauerhaft im Kalender (muss bei jedem Login neu hineingezogen werden),
und Drag-and-Drop lässt sich per Selenium nicht zuverlässig automatisieren. Der
eigentliche Vorteil überwiegt aber deutlich: Die Tabelle zeigt **alle** Urlaubszeiträume
des Jahres auf einen Blick (mit Status "Genommen"/"Genehmigt"), statt einzelne
Kalenderwochen durchklicken zu müssen.

## Aktueller Stand

- **Login funktioniert** (bestätigt anhand der LOGA-Anmeldeseite: Felder
  "Kennung"/"Kennwort", Button "ANMELDEN").
- **Urlaubsübersicht auslesen ist zuverlässig gelöst**: Die Tabelle enthält Zeilen wie
  `23.07. - 24.07.  Genehmigt   2.00`. Das Skript liest den gesamten sichtbaren Text
  des Popups und parst ihn zeilenübergreifend per regulärem Ausdruck (robuster als
  einzelne Tabellenzellen anzusteuern), inklusive Zuordnung des richtigen Jahres pro
  Zeitraum.
- **Der komplette Synchronisationszeitraum wird abgedeckt** (`-SyncPastDays`/
  `-SyncFutureDays`), da die Tabelle ohnehin alle Zeiträume des Jahres zeigt - keine
  Wochennavigation mehr nötig.
- **Outlook-Eintrag ist vollständig**: ganztägig, "Abwesend" (`olOutOfOffice`),
  privat, keine Erinnerung, Kategorie "LOGA Urlaub", keine Duplikate bei erneutem
  Ausführen (über eine aus Start-/Enddatum abgeleitete Kennung im Termintext).
