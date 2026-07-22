using LogaOutlookSync.Calendar;
using LogaOutlookSync.Domain;
using LogaOutlookSync.Infrastructure;
using LogaOutlookSync.Loga;
using Microsoft.Extensions.Logging;

namespace LogaOutlookSync.Application;

/// <summary>
/// Koordiniert einen vollständigen Synchronisationslauf: LOGA lesen, Kalenderplan erstellen,
/// optional als Trockenlauf anzeigen und andernfalls anwenden. Unterstützt Abbruch über
/// <see cref="CancellationToken"/> und läuft vollständig asynchron, damit die Oberfläche
/// währenddessen nicht einfriert.
/// </summary>
public sealed class SyncOrchestrator
{
    private readonly ILogaClient _logaClient;
    private readonly CalendarProviderFactory _calendarProviderFactory;
    private readonly SyncPlanner _planner;
    private readonly AppSettings _settings;
    private readonly IClock _clock;
    private readonly ILogger<SyncOrchestrator> _logger;

    public SyncOrchestrator(
        ILogaClient logaClient,
        CalendarProviderFactory calendarProviderFactory,
        SyncPlanner planner,
        AppSettings settings,
        IClock clock,
        ILogger<SyncOrchestrator> logger)
    {
        _logaClient = logaClient;
        _calendarProviderFactory = calendarProviderFactory;
        _planner = planner;
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Liest LOGA und den Ziel-Kalender und erstellt den Synchronisationsplan, ohne
    /// irgendetwas zu verändern. Wird für die Vorschau-Funktion verwendet.
    /// </summary>
    public async Task<IReadOnlyList<SyncPlanItem>> BuildPlanAsync(CancellationToken cancellationToken)
    {
        var today = _clock.TodayLocal;
        var windowStart = _settings.GetSyncWindowStart(today);
        var windowEnd = _settings.GetSyncWindowEnd(today);

        _logger.LogInformation(
            "Lese LOGA-Abwesenheiten für den Zeitraum {From:yyyy-MM-dd} bis {To:yyyy-MM-dd}.", windowStart, windowEnd);
        var absences = await _logaClient.GetAbsencesAsync(windowStart, windowEnd, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "{Count} genehmigte Abwesenheitseinträge im Zeitraum gefunden ({Vacation} Urlaub, {FlexTime} Gleitzeit).",
            absences.Count,
            absences.Count(a => a.Type == AbsenceType.Vacation),
            absences.Count(a => a.Type == AbsenceType.FlexTime));

        var timeZone = TimeZoneInfo.Local;
        var (rangeStart, _) = AllDayRangeCalculator.ComputeAllDayRange(windowStart, windowStart, timeZone);
        var (_, rangeEnd) = AllDayRangeCalculator.ComputeAllDayRange(windowEnd, windowEnd, timeZone);

        var calendarProvider = _calendarProviderFactory.Create();
        _logger.LogInformation("Lese verwaltete Outlook-Termine im Synchronisationszeitraum.");
        var managedEntries = await calendarProvider.GetManagedEntriesAsync(rangeStart, rangeEnd, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Outlook-Verbindung erfolgreich, {Count} Termine im Zeitraum gefunden.", managedEntries.Count);

        var plan = _planner.Plan(absences, managedEntries, _settings.DeletionPolicy);

        _logger.LogInformation(
            "Synchronisationsplan erstellt: {Create} neu, {Update} aktualisieren, {Delete} löschen, " +
            "{Unchanged} unverändert, {Conflict} Konflikte.",
            plan.Count(p => p.Operation == SyncOperationKind.Create),
            plan.Count(p => p.Operation == SyncOperationKind.Update),
            plan.Count(p => p.Operation == SyncOperationKind.Delete),
            plan.Count(p => p.Operation == SyncOperationKind.Unchanged),
            plan.Count(p => p.Operation == SyncOperationKind.Conflict));

        return plan;
    }

    /// <summary>
    /// Führt einen vollständigen Synchronisationslauf aus. Bei <paramref name="dryRun"/> = true
    /// wird ausschließlich der Plan erstellt, ohne Änderungen am Kalender vorzunehmen.
    /// </summary>
    /// <param name="dryRun">Trockenlauf ohne tatsächliche Kalenderänderungen.</param>
    /// <param name="confirmDeletions">
    /// Wird aufgerufen, wenn Löschungen anstehen und das Löschverhalten "Vor dem Löschen
    /// bestätigen" ist. Muss <see langword="true"/> zurückgeben, damit die Löschungen
    /// tatsächlich ausgeführt werden.
    /// </param>
    public async Task<SyncSummary> RunAsync(
        bool dryRun,
        Func<IReadOnlyList<SyncPlanItem>, CancellationToken, Task<bool>>? confirmDeletions,
        CancellationToken cancellationToken)
    {
        var startedAt = _clock.UtcNow;

        IReadOnlyList<SyncPlanItem> plan;
        try
        {
            plan = await BuildPlanAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Synchronisation fehlgeschlagen: Der Synchronisationsplan konnte nicht erstellt werden.");
            return SyncSummary.Failed(startedAt, _clock.UtcNow, dryRun, ex.Message);
        }

        var vacationFound = plan.Count(p => p.SourceAbsence?.Type == AbsenceType.Vacation);
        var flexTimeFound = plan.Count(p => p.SourceAbsence?.Type == AbsenceType.FlexTime);
        var unchanged = plan.Count(p => p.Operation == SyncOperationKind.Unchanged);
        var conflicts = plan.Count(p => p.Operation == SyncOperationKind.Conflict);

        if (dryRun)
        {
            _logger.LogInformation("Trockenlauf abgeschlossen: Es wurden keine Änderungen am Outlook-Kalender vorgenommen.");
            return new SyncSummary(
                startedAt,
                _clock.UtcNow,
                WasDryRun: true,
                vacationFound,
                flexTimeFound,
                Created: plan.Count(p => p.Operation == SyncOperationKind.Create),
                Updated: plan.Count(p => p.Operation == SyncOperationKind.Update),
                Deleted: 0,
                DeletionsPendingConfirmation: plan.Count(p => p.Operation == SyncOperationKind.Delete),
                unchanged,
                conflicts,
                ShowAsWarnings: 0,
                Succeeded: true,
                ErrorMessage: null,
                PlanItems: plan);
        }

        try
        {
            return await ApplyPlanAsync(plan, startedAt, vacationFound, flexTimeFound, unchanged, conflicts, confirmDeletions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Synchronisation fehlgeschlagen während der Anwendung des Plans.");
            return SyncSummary.Failed(startedAt, _clock.UtcNow, dryRun, ex.Message);
        }
    }

    private async Task<SyncSummary> ApplyPlanAsync(
        IReadOnlyList<SyncPlanItem> plan,
        DateTimeOffset startedAt,
        int vacationFound,
        int flexTimeFound,
        int unchanged,
        int conflicts,
        Func<IReadOnlyList<SyncPlanItem>, CancellationToken, Task<bool>>? confirmDeletions,
        CancellationToken cancellationToken)
    {
        var calendarProvider = _calendarProviderFactory.Create();

        var created = 0;
        var updated = 0;
        var deleted = 0;
        var pendingConfirmation = 0;
        var showAsWarnings = 0;

        var deleteItems = plan.Where(p => p.Operation == SyncOperationKind.Delete).ToList();
        var deletionApproved = deleteItems.Count == 0 || _settings.DeletionPolicy == DeletionPolicy.AutoDelete;

        if (!deletionApproved && confirmDeletions is not null)
        {
            deletionApproved = await confirmDeletions(deleteItems, cancellationToken).ConfigureAwait(false);
        }

        foreach (var planItem in plan)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (planItem.Operation)
            {
                case SyncOperationKind.Create:
                {
                    var result = await calendarProvider.CreateAsync(planItem.ProposedItem!, cancellationToken).ConfigureAwait(false);
                    created++;
                    LogShowAsWarningIfAny(result, planItem.ProposedItem!.Subject, ref showAsWarnings, "angelegt");
                    break;
                }

                case SyncOperationKind.Update:
                {
                    var result = await calendarProvider.UpdateAsync(planItem.ProposedItem!, cancellationToken).ConfigureAwait(false);
                    updated++;
                    LogShowAsWarningIfAny(result, planItem.ProposedItem!.Subject, ref showAsWarnings, "aktualisiert");
                    break;
                }

                case SyncOperationKind.Delete:
                    if (deletionApproved)
                    {
                        await calendarProvider.DeleteAsync(planItem.ExistingEntry!.CalendarEntryId, cancellationToken).ConfigureAwait(false);
                        deleted++;
                    }
                    else
                    {
                        pendingConfirmation++;
                    }

                    break;

                case SyncOperationKind.Unchanged:
                case SyncOperationKind.Conflict:
                    break;
            }
        }

        _logger.LogInformation(
            "Synchronisation erfolgreich beendet: {Created} angelegt, {Updated} aktualisiert, {Deleted} gelöscht, " +
            "{Pending} Löschungen zurückgestellt, {Warnings} \"Abwesend\"-Warnungen.",
            created, updated, deleted, pendingConfirmation, showAsWarnings);

        return new SyncSummary(
            startedAt,
            _clock.UtcNow,
            WasDryRun: false,
            vacationFound,
            flexTimeFound,
            created,
            updated,
            deleted,
            pendingConfirmation,
            unchanged,
            conflicts,
            showAsWarnings,
            Succeeded: true,
            ErrorMessage: null,
            PlanItems: plan);
    }

    private void LogShowAsWarningIfAny(CalendarWriteResult result, string subject, ref int showAsWarnings, string action)
    {
        if (result.ShowAsVerifiedOutOfOffice)
        {
            return;
        }

        showAsWarnings++;
        _logger.LogWarning("Warnung beim {Action} von '{Subject}': {Warning}", action, subject, result.Warning);
    }
}
