namespace LogaOutlookSync.Domain;

/// <summary>
/// Ein aus dem Outlook-/Graph-Kalender gelesener Termin, providerunabhängig dargestellt.
/// </summary>
/// <param name="CalendarEntryId">
/// Providerinterne, stabile Kennung des Termins (Graph Event-ID bzw. Outlook COM EntryID).
/// </param>
/// <param name="SyncId">
/// LOGA-Sync-ID, sofern der Termin eine erkennbare Kennzeichnung dieser Anwendung trägt.
/// <see langword="null"/>, wenn der Termin nicht von dieser Anwendung erzeugt wurde.
/// </param>
/// <param name="IsManagedByApp">
/// <see langword="true"/>, wenn der Termin eindeutig als von dieser Anwendung verwaltet
/// erkannt wurde (Marker + Sync-ID vorhanden). Nur solche Termine dürfen automatisiert
/// aktualisiert oder gelöscht werden.
/// </param>
/// <param name="Subject">Betreff des Termins.</param>
/// <param name="Start">Startzeitpunkt (inkl. Zeitzone).</param>
/// <param name="End">
/// Endzeitpunkt (inkl. Zeitzone). Bei Ganztagsterminen exklusiv, siehe
/// <c>AllDayRangeCalculator</c> in der Application-Schicht.
/// </param>
/// <param name="IsAllDay">Ganztägig ja/nein.</param>
/// <param name="Category">Outlook-Kategorie, sofern gesetzt.</param>
/// <param name="ShowAs">Aktueller "Anzeigen als"-Status des Termins.</param>
/// <param name="SourceFingerprint">
/// Fingerprint der LOGA-Quelldaten zum Zeitpunkt der letzten Synchronisation, sofern
/// als erweiterte Eigenschaft gespeichert. Dient der Änderungserkennung ohne erneuten
/// Feldvergleich.
/// </param>
/// <param name="LastModifiedUtc">Letzter Änderungszeitpunkt laut Kalenderanbieter, sofern bekannt.</param>
public sealed record ManagedCalendarEntry(
    string CalendarEntryId,
    string? SyncId,
    bool IsManagedByApp,
    string Subject,
    DateTimeOffset Start,
    DateTimeOffset End,
    bool IsAllDay,
    string? Category,
    CalendarShowAs ShowAs,
    string? SourceFingerprint,
    DateTimeOffset? LastModifiedUtc);
