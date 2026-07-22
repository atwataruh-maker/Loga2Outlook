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

Zum gefahrlosen Testen (es werden keine Outlook-Termine angelegt/verändert):

```powershell
.\Sync-LogaUrlaub-Outlook.ps1 -WhatIf
```

Das Skript fragt bei jedem Start nach Benutzername/Kennwort (über den normalen
Windows-Anmeldedialog) - es werden **keine Zugangsdaten im Skript oder auf der
Festplatte gespeichert.**

## Aktueller Stand

- **Login funktioniert** (basierend auf den bereitgestellten Screenshots der
  LOGA-Anmeldeseite: Felder "Kennung"/"Kennwort", Button "ANMELDEN").
- **Kalender auslesen ist ein bestmöglicher erster Entwurf.** Woher das Skript das
  genaue Datum eines Urlaubsbalkens nimmt (`title`- oder `data-date`-Attribut), ist
  noch nicht abschließend bestätigt. Schlägt dieser Schritt fehl, gibt das Skript
  eine klare Warnung aus statt falsche Daten zu erzeugen.
- **Outlook-Eintrag ist vollständig**: ganztägig, "Abwesend" (`olOutOfOffice`),
  privat, keine Erinnerung, Kategorie "LOGA Urlaub", keine Duplikate bei erneutem
  Ausführen (über eine versteckte Kennung im Termintext).

Falls der Kalender-Lese-Schritt bei dir nicht funktioniert: im Browser F12 öffnen,
auf einen Urlaubsbalken klicken, im Elements-Tab das Element und sein Elternelement
per Rechtsklick → "Copy" → "Copy outerHTML" kopieren und mir schicken.
