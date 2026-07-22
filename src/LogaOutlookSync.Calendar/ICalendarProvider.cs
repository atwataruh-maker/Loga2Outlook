using LogaOutlookSync.Domain;

namespace LogaOutlookSync.Calendar;

/// <summary>
/// Providerunabhängige Abstraktion über die Outlook-Kalenderanbindung. Implementierungen:
/// <see cref="Graph.MicrosoftGraphCalendarProvider"/> (bevorzugt) und
/// <see cref="OutlookCom.OutlookComCalendarProvider"/> (Fallback für Outlook Classic).
/// </summary>
public interface ICalendarProvider
{
    /// <summary>
    /// Liest alle Kalendertermine im Zeitraum [<paramref name="from"/>, <paramref name="to"/>],
    /// die eine erkennbare Kennzeichnung dieser Anwendung tragen könnten (siehe
    /// <see cref="ManagedCalendarEntry.IsManagedByApp"/>).
    /// </summary>
    Task<IReadOnlyList<ManagedCalendarEntry>> GetManagedEntriesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);

    /// <summary>
    /// Legt einen neuen Termin an. Prüft anschließend, ob der Termin tatsächlich als
    /// "Abwesend" gespeichert wurde (siehe <see cref="CalendarWriteResult.ShowAsVerifiedOutOfOffice"/>).
    /// </summary>
    Task<CalendarWriteResult> CreateAsync(CalendarSyncItem item, CancellationToken cancellationToken);

    /// <summary>
    /// Aktualisiert einen bestehenden, von dieser Anwendung verwalteten Termin
    /// (<see cref="CalendarSyncItem.ExistingCalendarEntryId"/> muss gesetzt sein).
    /// </summary>
    Task<CalendarWriteResult> UpdateAsync(CalendarSyncItem item, CancellationToken cancellationToken);

    /// <summary>Löscht einen Termin anhand seiner providerinternen ID.</summary>
    Task DeleteAsync(string calendarEntryId, CancellationToken cancellationToken);
}
