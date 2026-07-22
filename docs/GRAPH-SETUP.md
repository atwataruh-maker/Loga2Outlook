# Microsoft Graph einrichten (bevorzugte Kalenderanbindung)

Microsoft Graph ist die bevorzugte Kalenderanbindung. Sie erfordert eine eigene
Entra-ID-App-Registrierung (Azure AD), da die Anwendung ohne Installation und ohne
Administratorrechte auf dem Zielrechner läuft und daher keine vorinstallierte, firmenweite
App-Registrierung voraussetzen kann.

## 1. App-Registrierung erstellen

1. Im [Microsoft Entra Admin Center](https://entra.microsoft.com) anmelden (Rolle: mindestens
   Anwendungsadministrator oder vom Administrator durchführen lassen).
2. **Entra ID > App-Registrierungen > Neue Registrierung**.
3. Name: z. B. `LOGA Outlook Sync`.
4. Unterstützte Kontotypen: "Konten in einem beliebigen Organisationsverzeichnis" (oder
   passend zur eigenen Mandantenrichtlinie).
5. Redirect-URI: Plattform **"Mobile and desktop applications"**, URI `http://localhost`.
6. Registrieren.

## 2. Berechtigungen konfigurieren

1. **API-Berechtigungen > Berechtigung hinzufügen > Microsoft Graph > Delegierte
   Berechtigungen**.
2. `Calendars.ReadWrite` und `User.Read` hinzufügen.
3. Falls der Mandant Administratorzustimmung erfordert: **Administratorzustimmung erteilen**
   anklicken (durch einen Administrator).

## 3. Public-Client-Flow aktivieren

1. **Authentifizierung** öffnen.
2. Unter "Erweiterte Einstellungen" die Option **"Öffentliche Clientflows zulassen"** auf
   **Ja** setzen und speichern.
   (Erforderlich, da die Anwendung als Public Client ohne Client Secret arbeitet - es werden
   keine Geheimnisse im portablen Anwendungsordner abgelegt.)

## 4. Client-ID in der Anwendung eintragen

1. Auf der Übersichtsseite der App-Registrierung die **Anwendungs-ID (Client)** kopieren.
2. In der Anwendung unter Einstellungen (bzw. im Einrichtungsassistenten) bei
   "Entra-ID-App-Registrierung (Client-ID)" einfügen.
3. Bei "Entra-ID-Mandant" den Wert `organizations` (Standard, jeder Geschäfts-/Schulmandant)
   oder die konkrete Tenant-ID/-Domäne eintragen.
4. "Outlook-Verbindung testen" ausführen. Beim ersten Mal öffnet sich ein Anmeldefenster im
   System-Browser (MSAL Public-Client-Flow); nach erfolgreicher Anmeldung wird das Token
   verschlüsselt zwischengespeichert, sodass künftige Synchronisationen ohne erneute
   interaktive Anmeldung auskommen (siehe SECURITY.md).

## Wenn keine App-Registrierung möglich ist

Ist in der Organisation keine eigene App-Registrierung möglich oder werden die benötigten
Berechtigungen nicht freigegeben, wechseln Sie in den Einstellungen auf **Outlook COM** als
Kalenderanbindung (siehe [OUTLOOK-COM-FALLBACK.md](OUTLOOK-COM-FALLBACK.md)) oder belassen Sie
die Einstellung auf **Automatisch** - die Anwendung fällt dann automatisch auf Outlook COM
zurück, solange keine Client-ID konfiguriert ist.
