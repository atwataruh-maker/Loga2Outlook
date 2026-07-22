# Fehlerdiagnose

## Allgemeines Vorgehen

1. Öffnen Sie den Protokoll-Tab in der Anwendung und filtern Sie nach "Fehler".
2. Öffnen Sie bei Bedarf den Logordner (Schaltfläche "Logordner öffnen") und prüfen Sie die
   aktuelle Tagesdatei unter `%LOCALAPPDATA%\LogaOutlookSync\Logs\`.
3. Exportieren Sie bei Bedarf ein Diagnosepaket (Schaltfläche "Diagnosepaket exportieren").
   Dieses enthält Protokolle, Einstellungen und Debug-Screenshots/-HTML-Snapshots -
   **niemals** Passwörter, Session-Cookies oder Authentifizierungstoken.

## Bekannte Fehlerfälle und Lösungshinweise

| Fehler | Ursache | Lösung |
|---|---|---|
| "LOGA-Adresse konnte nicht geöffnet werden" | Keine Internetverbindung oder LOGA nicht erreichbar | Internetverbindung und LOGA-Webadresse in den Einstellungen prüfen |
| "LOGA-Anmeldung fehlgeschlagen" | Falsche Zugangsdaten, gesperrtes Konto, abgelaufenes Passwort | Zugangsdaten im Einrichtungsassistenten/Einstellungen neu eingeben und mit "LOGA-Anmeldung testen" prüfen |
| "Multi-Faktor-Authentifizierung erkannt" | MFA wurde für das LOGA-Konto aktiviert | Die Anwendung führt aus Sicherheitsgründen keine automatisierte MFA durch; mit der IT klären, ob ein alternativer, MFA-freier technischer Zugang möglich ist |
| "Navigationsschritt '...' ist fehlgeschlagen" | LOGA-Seitenstruktur wurde geändert | Den in der Fehlermeldung genannten Selektor in `loga-navigation.json`/`loga-selectors.json` im sichtbaren Debug-Modus neu ermitteln (siehe ADMIN-GUIDE.md) |
| "Ein Kalendereintrag enthält keinen auswertbaren Datumsbereichstext" | Kalender-Selektor passt nicht mehr zur Seitenstruktur | Selektor `calendarParsing.dateRange` neu ermitteln |
| "Microsoft Edge konnte nicht gestartet werden" | Edge nicht installiert oder Playwright kann den Browser nicht starten | Microsoft Edge installieren; prüfen, ob der portable Ordner vollständig kopiert wurde |
| "Playwright konnte nicht initialisiert werden" | Der `.playwright`-Treiberordner fehlt im portablen Anwendungsordner | Anwendungsordner vollständig neu aus der Veröffentlichung kopieren, siehe `scripts/publish-portable.ps1` |
| "Für Microsoft Graph ist keine Entra-ID-App-Registrierung konfiguriert" | `GraphClientId` nicht gesetzt | Siehe [GRAPH-SETUP.md](GRAPH-SETUP.md), oder auf Outlook COM wechseln |
| "Graph-Berechtigung fehlt" (Fehlermeldung von Microsoft Graph, z. B. `Forbidden`) | Der App-Registrierung fehlt die Freigabe von `Calendars.ReadWrite` durch einen Administrator | Administrator um Freigabe der Berechtigung bitten (siehe GRAPH-SETUP.md) |
| "Outlook konnte nicht gestartet werden" | Outlook Classic nicht installiert oder kein Profil eingerichtet | Outlook installieren und mit einem Postfach einrichten, oder auf Microsoft Graph wechseln |
| "Der konfigurierte Outlook-Kalenderordner wurde nicht gefunden" | Zielkalender wurde umbenannt oder gelöscht | In den Einstellungen erneut "Verfügbare Kalender anzeigen" und den Kalender neu auswählen |
| "Termin trägt keine gültige Kennzeichnung dieser Anwendung" | Ein Termin wurde manuell erstellt oder verändert | Gewolltes Verhalten: die Anwendung schützt manuell erstellte Termine und ändert/löscht sie nie automatisch |
| Synchronisation bricht mit "OperationCanceledException" ab | Benutzer hat auf "Abbrechen" geklickt, oder die Netzwerkverbindung ist während der Synchronisation abgebrochen | Erneut synchronisieren; bei wiederholtem Abbruch die Netzwerkverbindung prüfen |
| Warnung: "'Abwesend'-Status konnte nicht bestätigt werden" | Der Kalenderanbieter hat den Termin gespeichert, aber die anschließende Prüfung ist fehlgeschlagen | Termin in Outlook manuell prüfen; bei wiederholtem Auftreten Diagnosepaket exportieren und Protokoll prüfen |

## Firmenrichtlinien / Sicherheitssoftware

Blockieren Firmenrichtlinien die Ausführung, die Browserautomatisierung oder die
Kalenderberechtigung, meldet die Anwendung dies transparent über eine Fehlermeldung. Die
Anwendung umgeht solche Schutzmaßnahmen grundsätzlich nicht - wenden Sie sich in diesem Fall
an Ihre IT-Abteilung.
