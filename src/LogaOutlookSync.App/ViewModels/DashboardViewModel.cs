using CommunityToolkit.Mvvm.ComponentModel;
using LogaOutlookSync.App.Services;

namespace LogaOutlookSync.App.ViewModels;

/// <summary>Zeigt den aktuellen Verbindungs- und Synchronisationsstatus im Überblick an.</summary>
public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly SyncStateService _syncStateService;

    [ObservableProperty]
    private string connectionStatus = "Noch nicht synchronisiert";

    [ObservableProperty]
    private string lastSyncTimeText = "-";

    [ObservableProperty]
    private int vacationEntriesFound;

    [ObservableProperty]
    private int flexTimeEntriesFound;

    [ObservableProperty]
    private int created;

    [ObservableProperty]
    private int updated;

    [ObservableProperty]
    private int deleted;

    [ObservableProperty]
    private string lastSyncStatusText = "-";

    public DashboardViewModel(SyncStateService syncStateService)
    {
        _syncStateService = syncStateService;
        _syncStateService.SummaryChanged += OnSummaryChanged;
        Refresh();
    }

    private void OnSummaryChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        if (_syncStateService.IsSyncRunning)
        {
            ConnectionStatus = "Synchronisation läuft...";
        }

        var summary = _syncStateService.LastSummary;
        if (summary is null)
        {
            return;
        }

        ConnectionStatus = summary.Succeeded ? "Verbunden" : "Fehler bei letzter Synchronisation";
        LastSyncTimeText = summary.FinishedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");
        VacationEntriesFound = summary.VacationEntriesFound;
        FlexTimeEntriesFound = summary.FlexTimeEntriesFound;
        Created = summary.Created;
        Updated = summary.Updated;
        Deleted = summary.Deleted;

        LastSyncStatusText = summary.Succeeded
            ? summary.WasDryRun
                ? "Trockenlauf erfolgreich abgeschlossen (keine Änderungen vorgenommen)."
                : "Erfolgreich abgeschlossen."
            : $"Fehlgeschlagen: {summary.ErrorMessage}";
    }

    public void Dispose()
    {
        _syncStateService.SummaryChanged -= OnSummaryChanged;
    }
}
