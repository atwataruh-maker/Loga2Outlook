# Sicherheitsdokumentation

## Zugangsdaten

- Das LOGA-Passwort wird ausschließlich über den Windows Credential Manager gespeichert
  (Eintrag `LOGA-Outlook-Sync`, generischer Credential-Typ), verwaltet über
  `LogaOutlookSync.Security.WindowsCredentialManagerStore` (P/Invoke gegen `advapi32.dll`).
- Kann der Credential Manager technisch nicht verwendet werden (z. B. durch Gruppenrichtlinien
  eingeschränkt), weicht `FallbackCredentialStore` auf eine mit Windows DPAPI verschlüsselte
  Datei aus (`DataProtectionScope.CurrentUser`), die untrennbar an das aktuelle
  Windows-Benutzerkonto gebunden ist.
- Das Passwort wird zu keinem Zeitpunkt: im Klartext auf der Festplatte gespeichert, in einer
  JSON-Datei abgelegt, protokolliert, in einer Fehlermeldung angezeigt oder im Quellcode
  hinterlegt.
- Microsoft-Graph-Zugriffstoken werden nie im Klartext gespeichert: MSAL.NET verwaltet den
  Token-Cache über `Microsoft.Identity.Client.Extensions.Msal`, der die Cache-Datei unter
  Windows automatisch DPAPI-verschlüsselt.

## Protokollierung

- Logs werden unterhalb von `%LOCALAPPDATA%\LogaOutlookSync\Logs\` als rollierende
  Tagesdateien gespeichert, mit konfigurierbarer, begrenzter Aufbewahrungsdauer.
- Eine zusätzliche Redaktionsstufe (`LogaOutlookSync.Infrastructure.Logging.RedactingSink`)
  entfernt bekannte Muster sensibler Daten (Passwort-Zuweisungen, Bearer-Token,
  Cookie-/Set-Cookie-Header) aus jeder protokollierten Nachricht, als zusätzliche
  Verteidigungslinie zur im Code eingehaltenen Sorgfaltspflicht, niemals Passwörter oder
  Token an den Logger zu übergeben.
- Debug-Screenshots und HTML-Snapshots, die bei Fehlern in der Browserautomatisierung
  automatisch erstellt werden, entfernen zuvor die Werte von Passwortfeldern und den Inhalt
  von `<script>`-Tags (siehe `DebugArtifactCollector.RedactSensitiveHtml`).
- Das exportierbare Diagnosepaket (ZIP) enthält ausschließlich Protokolle, nicht sensible
  Einstellungen und Debug-Artefakte - keine Zugangsdaten.

## Berechtigungen

- Minimalprinzip: Für Microsoft Graph wird ausschließlich `Calendars.ReadWrite` (delegiert,
  Public-Client) sowie `User.Read` angefragt.
- Die Anwendung ändert ausschließlich Termine im ausgewählten Ziel-Kalender und ausschließlich
  solche, die eine eindeutige, von dieser Anwendung selbst gesetzte Kennzeichnung tragen
  (`ManagedBy: LogaOutlookSync` plus `LOGA-SYNC-ID`, siehe `ManagedEntryMetadata`). Manuell
  erstellte oder fremde Termine werden nie automatisch verändert oder gelöscht.

## Browserautomatisierung

- Es werden keine Browser- oder Zertifikatssicherheitsmechanismen deaktiviert.
- Es wird keine automatisierte Umgehung von Multi-Faktor-Authentifizierung oder Captchas
  vorgenommen; wird eine MFA-Abfrage erkannt, bricht die Anmeldung kontrolliert mit einer
  klaren Fehlermeldung ab.
- Es werden keine Zugangsdaten als Kommandozeilenparameter übergeben.
- Playwright startet ausschließlich die bereits auf dem Rechner installierte Microsoft-Edge-
  Instanz (`Channel = "msedge"`); es wird kein zusätzlicher Browser heruntergeladen oder
  systemweit installiert.

## Keine Datenübertragung an Dritte

Die Anwendung überträgt keine Daten an externe Dienste außer den vom Benutzer konfigurierten
Endpunkten (LOGA-Webportal, Microsoft Graph bzw. das lokal installierte Outlook). Es findet
keine Telemetrie und keine Übertragung an sonstige Drittanbieter statt.

## Portabilität ohne Systemänderungen

Die Anwendung installiert keine Software systemweit, verändert keine Systemeinstellungen und
benötigt keine Administratorrechte. Alle Zustandsdaten liegen unterhalb von
`%LOCALAPPDATA%\LogaOutlookSync\`.
