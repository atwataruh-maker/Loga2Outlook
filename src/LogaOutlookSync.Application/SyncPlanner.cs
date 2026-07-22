using LogaOutlookSync.Domain;

namespace LogaOutlookSync.Application;

/// <summary>
/// Vergleicht die aktuellen, genehmigten LOGA-Abwesenheitseinträge mit den bereits von dieser
/// Anwendung verwalteten Outlook-Terminen und erstellt daraus einen Synchronisationsplan
/// (Vorschau/Trockenlauf-Ergebnis). Nicht von dieser Anwendung verwaltete Termine werden
/// niemals in den Plan aufgenommen und damit auch nie verändert oder gelöscht.
/// </summary>
public sealed class SyncPlanner
{
    private readonly CalendarSyncItemFactory _itemFactory;
    private readonly AbsenceSyncPolicy _policy;

    public SyncPlanner(CalendarSyncItemFactory itemFactory, AbsenceSyncPolicy? policy = null)
    {
        _itemFactory = itemFactory;
        _policy = policy ?? AbsenceSyncPolicy.Default;
    }

    /// <summary>Erstellt den Synchronisationsplan.</summary>
    /// <param name="absences">Alle vom LOGA-Client gelieferten, bereits auf "genehmigt" gefilterten Abwesenheitseinträge.</param>
    /// <param name="existingManagedEntries">Alle im Synchronisationszeitraum gelesenen Outlook-Termine (verwaltet und fremd).</param>
    /// <param name="deletionPolicy">Verhalten bei nicht mehr vorhandenen LOGA-Einträgen.</param>
    public IReadOnlyList<SyncPlanItem> Plan(
        IReadOnlyList<AbsenceEntry> absences,
        IReadOnlyList<ManagedCalendarEntry> existingManagedEntries,
        DeletionPolicy deletionPolicy)
    {
        var relevantAbsences = absences.Where(a => _policy.IsSynced(a.Type)).ToList();

        var managedBySyncId = new Dictionary<string, ManagedCalendarEntry>(StringComparer.Ordinal);
        var planItems = new List<SyncPlanItem>();

        foreach (var managedEntry in existingManagedEntries.Where(e => e.IsManagedByApp && !string.IsNullOrEmpty(e.SyncId)))
        {
            if (!managedBySyncId.TryAdd(managedEntry.SyncId!, managedEntry))
            {
                planItems.Add(new SyncPlanItem(
                    SyncOperationKind.Conflict,
                    managedEntry.SyncId,
                    SourceAbsence: null,
                    ExistingEntry: managedEntry,
                    ProposedItem: null,
                    Reason: $"Mehrere Outlook-Termine tragen dieselbe LOGA-Sync-Kennung '{managedEntry.SyncId}'. " +
                        "Bitte manuell prüfen, welcher Termin korrekt ist."));
            }
        }

        foreach (var absence in relevantAbsences)
        {
            if (managedBySyncId.Remove(absence.SourceId, out var existing))
            {
                var proposed = _itemFactory.Create(absence, existing.CalendarEntryId);

                planItems.Add(IsUnchanged(existing, proposed)
                    ? new SyncPlanItem(SyncOperationKind.Unchanged, absence.SourceId, absence, existing, proposed, "Termin ist bereits aktuell.")
                    : new SyncPlanItem(
                        SyncOperationKind.Update,
                        absence.SourceId,
                        absence,
                        existing,
                        proposed,
                        "LOGA-Eintrag hat sich geändert, oder der verwaltete Termin wurde manuell abweichend verändert " +
                        "und wird auf den LOGA-Stand zurückgesetzt."));
            }
            else
            {
                var proposed = _itemFactory.Create(absence, existingCalendarEntryId: null);
                planItems.Add(new SyncPlanItem(
                    SyncOperationKind.Create,
                    absence.SourceId,
                    absence,
                    ExistingEntry: null,
                    proposed,
                    "Neuer genehmigter LOGA-Eintrag ohne passenden Outlook-Termin."));
            }
        }

        foreach (var (syncId, orphaned) in managedBySyncId)
        {
            var isReportOnly = deletionPolicy == DeletionPolicy.ReportOnly;
            planItems.Add(new SyncPlanItem(
                isReportOnly ? SyncOperationKind.Conflict : SyncOperationKind.Delete,
                syncId,
                SourceAbsence: null,
                ExistingEntry: orphaned,
                ProposedItem: null,
                Reason: isReportOnly
                    ? "LOGA-Eintrag nicht mehr vorhanden oder storniert. Löschverhalten ist auf \"Nur als Konflikt melden\" eingestellt."
                    : "LOGA-Eintrag nicht mehr vorhanden oder storniert."));
        }

        return planItems;
    }

    private static bool IsUnchanged(ManagedCalendarEntry existing, CalendarSyncItem proposed)
    {
        return string.Equals(existing.SourceFingerprint, proposed.SourceFingerprint, StringComparison.Ordinal)
            && existing.Start == proposed.Start
            && existing.End == proposed.End
            && existing.IsAllDay == proposed.IsAllDay
            && existing.ShowAs == proposed.ShowAs
            && string.Equals(existing.Subject, proposed.Subject, StringComparison.Ordinal)
            && string.Equals(existing.Category, proposed.Category, StringComparison.Ordinal);
    }
}
