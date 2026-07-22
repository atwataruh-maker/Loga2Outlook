# Einfachste Variante: Bookmarklet (reines JavaScript, kein Server)

Diese Variante ist bewusst **unabhängig** vom großen .NET/WPF-Projekt in `src/` und
löst genau eine Aufgabe: **genehmigten und genommenen Urlaub aus LOGA als ICS-Datei
exportieren, zum Import in Outlook.**

Sie ersetzt die vorherige PowerShell-/Selenium-Lösung vollständig. Kein Build, kein
NuGet, kein Edge WebDriver, kein Selenium, kein PowerShell-Modul, kein PHP, kein
Server. Das Skript läuft ausschließlich als **Lesezeichen ("Bookmarklet")** direkt im
Browser, auf der bereits angemeldeten LOGA-Seite:

- Es fragt **keine Zugangsdaten** ab und speichert keine (es nutzt die ohnehin schon
  angemeldete Browsersitzung).
- Es **überträgt keine Daten nach außen** - alles passiert lokal im Browser.
- Es liest die Urlaubsdaten direkt aus dem bereits im Browser angezeigten DOM.

## Dateien

- [`loga-urlaub-export.js`](loga-urlaub-export.js) - gut lesbarer Quellcode zur
  Entwicklung und Wartung.
- [`loga-urlaub-export.bookmarklet.txt`](loga-urlaub-export.bookmarklet.txt) - dieselbe
  Logik als fertige, kompakte `javascript:`-URI zum Anlegen des Lesezeichens.
- [`build-bookmarklet.js`](build-bookmarklet.js) - erzeugt die Bookmarklet-Datei aus
  der Quelldatei neu (nur für Weiterentwicklung nötig, `node build-bookmarklet.js`).
- [`tests/parser.test.js`](tests/parser.test.js) - abhängigkeitsfreie Tests der
  Parser-/ICS-Logik (`node tests/parser.test.js`).

## Einrichtung des Lesezeichens (einmalig, ca. 2 Minuten)

1. Datei [`loga-urlaub-export.bookmarklet.txt`](loga-urlaub-export.bookmarklet.txt)
   öffnen und den gesamten Inhalt (beginnt mit `javascript:`) kopieren.
2. In Microsoft Edge: Rechtsklick auf die Lesezeichenleiste → **"Seite hinzufügen"**
   (bzw. über `Strg+Shift+O` den Lesezeichen-Manager öffnen → **"Neues Lesezeichen"**).
3. Als **Name** z. B. "LOGA Urlaub exportieren" eintragen.
4. Als **URL** den kopierten `javascript:...`-Text einfügen und speichern.

   **Hinweis:** Manche Browser kürzen sehr lange Adressen, wenn man sie direkt in die
   Adressleiste tippt/einfügt und dort Enter drückt. Der zuverlässige Weg ist daher,
   die URL im **Bearbeiten-Dialog** des Lesezeichens einzufügen (wie oben beschrieben),
   nicht über die Adressleiste zu navigieren.

## Verwendung

1. Bei LOGA anmelden (ganz normal, wie gewohnt).
2. Im Kalenderbereich das Werkzeug-Symbol anklicken und **"Urlaubsübersicht"** per
   Drag-and-Drop in den Wochenbereich ziehen.
3. Im sich öffnenden Popup im Dropdown **"Urlaub"** auswählen, sodass die Tabelle mit
   allen Urlaubszeiträumen erscheint.
4. Auf das Lesezeichen **"LOGA Urlaub exportieren"** klicken.
5. Es erscheint eine Vorschau aller gefundenen Urlaubszeiträume. Mit "OK" bestätigen,
   um die Datei `LOGA-Urlaub.ics` herunterzuladen.
6. Die heruntergeladene `LOGA-Urlaub.ics` doppelklicken (oder in Outlook über
   **Datei → Öffnen → Kalender importieren** auswählen) - Outlook übernimmt die
   Termine automatisch in den Kalender.

## Was die ICS-Datei enthält

Für jeden gefundenen Urlaubszeitraum (Status "Genehmigt" oder "Genommen") wird ein
Termin erzeugt:

- ganztägig,
- Betreff **"Urlaub"**,
- **CLASS:PRIVATE** (privat),
- Outlook-Status **"Abwesend"** (`X-MICROSOFT-CDO-BUSYSTATUS:OOF`, das von Outlook
  ausgewertete Gegenstück zu `TRANSP:OPAQUE`),
- **keine Erinnerung** (kein `VALARM`-Block in der Datei),
- **exklusives Enddatum** (letzter Urlaubstag + 1 Tag), damit der letzte Urlaubstag in
  Outlook nicht fehlt,
- eine **stabile, deterministische UID** aus Start- und Enddatum
  (`loga-urlaub-JJJJMMTT-JJJJMMTT@loga-urlaub-export`) - ein erneuter Export
  desselben Zeitraums erzeugt exakt dieselbe UID.

**Hinweis zu Duplikaten:** Ob Outlook einen erneuten Import mit gleicher UID als
Aktualisierung statt als neuen Termin behandelt, hängt von der gewählten Import-Methode
ab (einfaches Doppelklicken/Öffnen legt in der Regel unabhängig von der UID einen neuen
Termin an). Vor einem erneuten Import empfiehlt es sich daher, zuvor importierte
LOGA-Urlaubstermine im Kalender zu prüfen bzw. zu entfernen.

## Wie die Erkennung funktioniert

Das Skript sucht im DOM ausschließlich nach den bestätigten, stabilen CSS-Klassen:

- `.LG-InputLabel.Cell.Von` - Startdatum einer Zeile (z. B. "05.02.")
- `.LG-InputLabel.Cell.Bis` - Enddatum einer Zeile
- `.LG-InputLabel.Cell.Text` - Status ("Genommen"/"Genehmigt"/...)

Die von LOGA bei jedem Rendern neu vergebenen `id="LGLabel203"`-Attribute werden
bewusst **nicht** verwendet, da sie sich ändern können.

Da das zugehörige Jahr nicht in jeder Datenzeile steht, sondern einmal pro
Jahresabschnitt der Tabelle (z. B. "2026 Resturlaub Vorjahr ..."), durchläuft das
Skript den sichtbaren Bereich in Dokumentreihenfolge und merkt sich die zuletzt
gesehene vierstellige Jahreszahl; jede folgende Datenzeile wird diesem Jahr
zugeordnet. Liegt das Enddatum eines Zeitraums kalendarisch vor dem Startdatum (z. B.
"29.12. - 02.01."), wird das Enddatum automatisch dem Folgejahr zugeordnet.

Wird kein Eintrag oder kein zugehöriges Jahr gefunden, zeigt das Skript eine
verständliche Fehlermeldung statt falscher Daten.

## Sicherheit

- Das Skript prüft beim Start, ob es auf der konfigurierten LOGA-Domain
  (`dedalus.pi-asp.de`) ausgeführt wird, und bricht andernfalls mit einer klaren
  Meldung ab.
- Es werden keine Zugangsdaten abgefragt, gespeichert oder verarbeitet.
- Es findet keine Netzwerkkommunikation statt - die ICS-Datei wird ausschließlich
  lokal im Browser erzeugt und heruntergeladen.

## Tests

```powershell
node tests/parser.test.js
```

Deckt u. a. ab: eintägiger und mehrtägiger Urlaub, mehrere Einträge, Status
"Genommen"/"Genehmigt" (andere Status werden ignoriert), Einträge in zwei
verschiedenen Jahren, Urlaub über einen Jahreswechsel hinweg, leere Tabelle, fehlendes
Jahr, nicht parsebares Datum, das UID-Schema sowie den erzeugten ICS-Inhalt
(exklusives Enddatum, `CLASS:PRIVATE`, `OOF`, keine `VALARM`-Erinnerung).
