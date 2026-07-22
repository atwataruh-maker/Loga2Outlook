using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogaOutlookSync.Infrastructure;

namespace LogaOutlookSync.App.ViewModels;

/// <summary>
/// Zeigt die aktuellste Protokolldatei gefiltert nach Schweregrad an und bietet Zugriff auf
/// den Logordner sowie den Export eines Diagnosepakets ohne Zugangsdaten.
/// </summary>
public sealed partial class LogViewModel : ObservableObject
{
    private const int MaxDisplayedLines = 1000;

    private readonly DiagnosticsPackageExporter _diagnosticsExporter;

    [ObservableProperty]
    private bool showInfo = true;

    [ObservableProperty]
    private bool showWarning = true;

    [ObservableProperty]
    private bool showError = true;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public ObservableCollection<string> LogLines { get; } = new();

    public LogViewModel(DiagnosticsPackageExporter diagnosticsExporter)
    {
        _diagnosticsExporter = diagnosticsExporter;
    }

    [RelayCommand]
    private void Refresh()
    {
        LogLines.Clear();

        var latestLogFile = Directory.Exists(AppPaths.LogsDirectory)
            ? Directory.EnumerateFiles(AppPaths.LogsDirectory, "*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;

        if (latestLogFile is null)
        {
            StatusMessage = "Keine Protokolldatei gefunden.";
            return;
        }

        using var stream = new FileStream(latestLogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        var allLines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            allLines.Add(line);
        }

        foreach (var entry in allLines.TakeLast(MaxDisplayedLines).Where(MatchesFilter))
        {
            LogLines.Add(entry);
        }

        StatusMessage = $"{LogLines.Count} Zeilen aus '{Path.GetFileName(latestLogFile)}' geladen.";
    }

    private bool MatchesFilter(string logLine)
    {
        if (logLine.Contains("[ERR]", StringComparison.Ordinal))
        {
            return ShowError;
        }

        if (logLine.Contains("[WRN]", StringComparison.Ordinal))
        {
            return ShowWarning;
        }

        return ShowInfo;
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        AppPaths.EnsureDirectoriesExist();
        Process.Start(new ProcessStartInfo(AppPaths.LogsDirectory) { UseShellExecute = true });
    }

    [RelayCommand]
    private void ExportDiagnostics()
    {
        try
        {
            var path = _diagnosticsExporter.Export();
            StatusMessage = $"Diagnosepaket erstellt: {path}";
            Process.Start(new ProcessStartInfo(AppPaths.DiagnosticsDirectory) { UseShellExecute = true });
        }
        catch (IOException ex)
        {
            StatusMessage = $"Diagnosepaket konnte nicht erstellt werden: {ex.Message}";
        }
    }
}
