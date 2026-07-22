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

## Aktueller Stand

- **Login funktioniert** (bestätigt anhand der LOGA-Anmeldeseite: Felder
  "Kennung"/"Kennwort", Button "ANMELDEN").
- **Kalender auslesen ist zuverlässig gelöst**: LOGA legt das Datum eines
  Urlaubsbalkens nicht als Attribut ab, aber ein Klick auf den Balken öffnet ein
  Popup mit den exakten Feldern "Anfangsdatum"/"Endedatum" (bestätigt per Screenshot:
  Eingabefeld `name="vacationHalfDayServerMaskPart-startDate"`). Das Skript klickt
  daher jeden gefundenen Urlaubsbalken einzeln an, liest die beiden Datumsfelder aus
  und schließt das Popup wieder (ESC), statt Positionen zu erraten.
- **Aktuell wird nur die nach dem Login angezeigte Woche gelesen** (in der Regel die
  aktuelle Woche) - eine automatische Navigation zu anderen Wochen (für länger
  zurückliegenden oder weiter in der Zukunft liegenden Urlaub) ist noch nicht
  eingebaut, da die Selektoren für "nächste/vorherige Woche" noch nicht bekannt sind.
- **Outlook-Eintrag ist vollständig**: ganztägig, "Abwesend" (`olOutOfOffice`),
  privat, keine Erinnerung, Kategorie "LOGA Urlaub", keine Duplikate bei erneutem
  Ausführen (über LOGAs eigene `data-cache-id` als Kennung im Termintext).

## Bekannte offene Punkte

- **Mehrwöchige Synchronisation**: Damit auch Urlaub in anderen Wochen gefunden wird,
  braucht es noch die Selektoren für die Wochen-Navigation (Pfeile "vorherige/nächste
  Woche" bzw. die Datumsauswahl im Kalender-Miniaturbild links). Screenshot/HTML davon
  hilft weiter.
