namespace LogaOutlookSync.Domain;

/// <summary>
/// Verständliche Zusammenfassung eines abgeschlossenen (oder simulierten) Synchronisationslaufs,
/// wie sie im Dashboard und am Ende der Synchronisation angezeigt wird.
/// </summary>
public sealed record SyncSummary(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    bool WasDryRun,
    int VacationEntriesFound,
    int FlexTimeEntriesFound,
    int Created,
    int Updated,
    int Deleted,
    int DeletionsPendingConfirmation,
    int Unchanged,
    int Conflicts,
    int ShowAsWarnings,
    bool Succeeded,
    string? ErrorMessage,
    IReadOnlyList<SyncPlanItem> PlanItems)
{
    /// <summary>Erstellt eine leere, fehlgeschlagene Zusammenfassung mit Fehlermeldung (z. B. bei Abbruch vor Beginn der Verarbeitung).</summary>
    public static SyncSummary Failed(DateTimeOffset startedAtUtc, DateTimeOffset finishedAtUtc, bool wasDryRun, string errorMessage)
    {
        return new SyncSummary(
            startedAtUtc,
            finishedAtUtc,
            wasDryRun,
            VacationEntriesFound: 0,
            FlexTimeEntriesFound: 0,
            Created: 0,
            Updated: 0,
            Deleted: 0,
            DeletionsPendingConfirmation: 0,
            Unchanged: 0,
            Conflicts: 0,
            ShowAsWarnings: 0,
            Succeeded: false,
            ErrorMessage: errorMessage,
            PlanItems: Array.Empty<SyncPlanItem>());
    }
}
