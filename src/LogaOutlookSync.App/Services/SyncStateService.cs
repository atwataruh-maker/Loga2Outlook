using LogaOutlookSync.Domain;

namespace LogaOutlookSync.App.Services;

/// <summary>
/// Hält den zuletzt bekannten Synchronisationsstatus im Speicher, damit das Dashboard
/// unabhängig davon, ob die Synchronisation über die Dashboard- oder die Synchronisations-
/// Ansicht angestoßen wurde, den aktuellen Stand anzeigen kann.
/// </summary>
public sealed class SyncStateService
{
    /// <summary>Wird ausgelöst, sobald ein neuer Synchronisationslauf (auch Vorschau/Trockenlauf) abgeschlossen ist.</summary>
    public event EventHandler? SummaryChanged;

    public SyncSummary? LastSummary { get; private set; }

    public bool IsSyncRunning { get; private set; }

    public void ReportSummary(SyncSummary summary)
    {
        LastSummary = summary;
        SummaryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReportRunningStateChanged(bool isRunning)
    {
        IsSyncRunning = isRunning;
        SummaryChanged?.Invoke(this, EventArgs.Empty);
    }
}
