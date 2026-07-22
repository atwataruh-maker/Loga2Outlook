using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogaOutlookSync.App.Services;
using LogaOutlookSync.Application;
using LogaOutlookSync.Domain;
using Microsoft.Extensions.Logging;

namespace LogaOutlookSync.App.ViewModels;

/// <summary>
/// Steuert die Synchronisation: Jetzt synchronisieren, Vorschau und Trockenlauf, jeweils
/// asynchron mit Fortschrittsanzeige und Abbruchmöglichkeit, damit die Oberfläche nicht einfriert.
/// </summary>
public sealed partial class SyncViewModel : ObservableObject
{
    private readonly SyncOrchestrator _orchestrator;
    private readonly SyncStateService _syncStateService;
    private readonly ILogger<SyncViewModel> _logger;

    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string currentStep = "Bereit.";

    [ObservableProperty]
    private string? statusMessage;

    public ObservableCollection<SyncPlanItemViewModel> PlanItems { get; } = new();

    public SyncViewModel(SyncOrchestrator orchestrator, SyncStateService syncStateService, ILogger<SyncViewModel> logger)
    {
        _orchestrator = orchestrator;
        _syncStateService = syncStateService;
        _logger = logger;
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task SyncNowAsync() => RunAsync(dryRun: false, previewOnly: false);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task PreviewAsync() => RunAsync(dryRun: true, previewOnly: true);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task DryRunAsync() => RunAsync(dryRun: true, previewOnly: false);

    private bool CanRun() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cancellationTokenSource?.Cancel();
        CurrentStep = "Abbruch angefordert...";
    }

    private bool CanCancel() => IsBusy;

    private async Task RunAsync(bool dryRun, bool previewOnly)
    {
        IsBusy = true;
        SyncNowCommand.NotifyCanExecuteChanged();
        PreviewCommand.NotifyCanExecuteChanged();
        DryRunCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        _syncStateService.ReportRunningStateChanged(true);

        _cancellationTokenSource = new CancellationTokenSource();
        StatusMessage = null;
        PlanItems.Clear();

        try
        {
            if (previewOnly)
            {
                CurrentStep = "Erstelle Vorschau...";
                var plan = await _orchestrator.BuildPlanAsync(_cancellationTokenSource.Token).ConfigureAwait(true);
                foreach (var item in plan)
                {
                    PlanItems.Add(new SyncPlanItemViewModel(item));
                }

                CurrentStep = "Vorschau abgeschlossen.";
                StatusMessage = $"{plan.Count} Einträge im Plan. Es wurden keine Änderungen vorgenommen.";
                return;
            }

            CurrentStep = dryRun ? "Trockenlauf wird ausgeführt..." : "Synchronisation wird ausgeführt...";
            var summary = await _orchestrator.RunAsync(dryRun, ConfirmDeletionsAsync, _cancellationTokenSource.Token).ConfigureAwait(true);

            foreach (var item in summary.PlanItems)
            {
                PlanItems.Add(new SyncPlanItemViewModel(item));
            }

            _syncStateService.ReportSummary(summary);

            CurrentStep = "Abgeschlossen.";
            StatusMessage = summary.Succeeded
                ? BuildSuccessMessage(summary)
                : $"Synchronisation fehlgeschlagen: {summary.ErrorMessage}";
        }
        catch (OperationCanceledException)
        {
            CurrentStep = "Abgebrochen.";
            StatusMessage = "Die Synchronisation wurde durch den Benutzer abgebrochen.";
        }
        finally
        {
            IsBusy = false;
            _syncStateService.ReportRunningStateChanged(false);
            SyncNowCommand.NotifyCanExecuteChanged();
            PreviewCommand.NotifyCanExecuteChanged();
            DryRunCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            _cancellationTokenSource = null;
        }
    }

    private static string BuildSuccessMessage(SyncSummary summary)
    {
        if (summary.WasDryRun)
        {
            return "Trockenlauf abgeschlossen: keine Änderungen am Outlook-Kalender vorgenommen. " +
                   $"{summary.Created} neu, {summary.Updated} aktualisieren, {summary.DeletionsPendingConfirmation} löschen wären fällig.";
        }

        var warningText = summary.ShowAsWarnings > 0
            ? $" Achtung: bei {summary.ShowAsWarnings} Termin(en) konnte der \"Abwesend\"-Status nicht bestätigt werden."
            : string.Empty;

        return $"Synchronisation abgeschlossen: {summary.Created} angelegt, {summary.Updated} aktualisiert, " +
               $"{summary.Deleted} gelöscht, {summary.DeletionsPendingConfirmation} zurückgestellt.{warningText}";
    }

    private Task<bool> ConfirmDeletionsAsync(IReadOnlyList<SyncPlanItem> deletions, CancellationToken cancellationToken)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var subjects = string.Join(
                Environment.NewLine,
                deletions.Select(d => $"- {d.ExistingEntry?.Subject} ({d.ExistingEntry?.Start:dd.MM.yyyy})"));

            var result = MessageBox.Show(
                $"Folgende {deletions.Count} Termin(e) sind in LOGA nicht mehr vorhanden oder storniert und " +
                $"sollen aus Outlook entfernt werden:{Environment.NewLine}{Environment.NewLine}{subjects}" +
                $"{Environment.NewLine}{Environment.NewLine}Jetzt löschen?",
                "Löschungen bestätigen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            return result == MessageBoxResult.Yes;
        }).Task;
    }
}
