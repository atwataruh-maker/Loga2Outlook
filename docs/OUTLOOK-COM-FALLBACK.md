# Outlook COM als Fallback einrichten

Wenn Microsoft Graph aufgrund fehlender Berechtigungen oder einer nicht möglichen
Entra-ID-App-Registrierung nicht eingesetzt werden kann, verwendet die Anwendung Outlook COM
Interop gegen die lokal installierte Outlook-Classic-Anwendung (Desktop).

## Voraussetzungen

- Microsoft Outlook Desktop (Outlook Classic, nicht "Neues Outlook") ist installiert.
- Outlook wurde mindestens einmal gestartet und ein Postfach/Profil ist eingerichtet.
- Outlook darf während der Synchronisation geöffnet sein oder wird bei Bedarf automatisch von
  der Anwendung im Hintergrund gestartet.

## Einrichtung

1. In den Einstellungen bzw. im Einrichtungsassistenten bei "Bevorzugte Anbindung"
   **Outlook COM** auswählen (oder **Automatisch** belassen, solange keine Microsoft-Graph-
   Client-ID konfiguriert ist - dann wird automatisch Outlook COM verwendet).
2. **"Verfügbare Kalender anzeigen"** ausführen. Es werden der Standardkalender sowie alle
   direkten Unterordner des Standardkalenders aufgelistet.
3. Den gewünschten Kalender auswählen und **"Ausgewählten Kalender testen"** ausführen.

## Funktionsweise

- Jeder COM-Aufruf läuft auf einem dedizierten Thread im STA-Apartment
  (`LogaOutlookSync.Calendar.OutlookCom.StaThread`), da Outlook-COM-Objekte zwingend ein
  STA-Apartment erfordern.
- Die LOGA-Sync-Kennung, der "ManagedBy"-Marker und der Quell-Fingerprint werden als
  benutzerdefinierte `UserProperties` am jeweiligen `AppointmentItem` gespeichert.
- "Abwesend" entspricht `BusyStatus = OlBusyStatus.olOutOfOffice`, "Privat" entspricht
  `Sensitivity = OlSensitivity.olPrivate`.
- Alle COM-Objekte werden zuverlässig über `Marshal.ReleaseComObject` freigegeben, auch im
  Fehlerfall.

## Einschränkungen gegenüber Microsoft Graph

- Outlook muss lokal installiert und eingerichtet sein; die Anbindung funktioniert nicht,
  wenn der Benutzer ausschließlich Outlook im Web nutzt.
- Die Synchronisation ist an die lokal laufende Outlook-Instanz gebunden und kann nicht ohne
  ein gestartetes bzw. startbares Outlook-Profil erfolgen.

## Wechsel der Kalenderanbindung

Die LOGA-Sync-Kennung wird bei beiden Anbindungen als providerspezifische erweiterte
Eigenschaft gespeichert (Microsoft-Graph-Termine: "Single Value Extended Property"; Outlook-
COM-Termine: benutzerdefinierte `UserProperties`). Diese beiden Speicherorte sind **nicht**
providerübergreifend lesbar. Ein Wechsel von Outlook COM zu Microsoft Graph (oder umgekehrt)
erkennt zuvor mit dem jeweils anderen Provider angelegte Termine daher **nicht** automatisch
als verwaltet wieder und würde bei der nächsten Synchronisation neue Termine anlegen.

Empfehlung beim Wechsel der Anbindung: vor dem Umschalten eine Vorschau ausführen und die
zuvor vom alten Provider angelegten Termine (erkennbar am Betreff bzw. am technischen
Fußabschnitt "ManagedBy: LogaOutlookSync" im Termintext) manuell löschen, damit nach dem
Wechsel keine Duplikate entstehen.
