namespace LogaOutlookSync.Domain;

/// <summary>
/// Ein einzelner Eintrag im Synchronisationsplan (Vorschau/Trockenlauf-Ergebnis),
/// der eine geplante oder bereits ausgeführte Änderung beschreibt.
/// </summary>
/// <param name="Operation">Art der geplanten Änderung.</param>
/// <param name="SyncId">LOGA-Sync-Kennung, sofern zugeordnet.</param>
/// <param name="SourceAbsence">Zugrunde liegender LOGA-Eintrag, sofern vorhanden (nicht bei reinem Delete-Konflikt).</param>
/// <param name="ExistingEntry">Bestehender Outlook-Termin, sofern vorhanden.</param>
/// <param name="ProposedItem">Zu schreibender Termin, sofern es sich um Create/Update handelt.</param>
/// <param name="Reason">Für Menschen lesbare Begründung der Einstufung.</param>
public sealed record SyncPlanItem(
    SyncOperationKind Operation,
    string? SyncId,
    AbsenceEntry? SourceAbsence,
    ManagedCalendarEntry? ExistingEntry,
    CalendarSyncItem? ProposedItem,
    string Reason);
