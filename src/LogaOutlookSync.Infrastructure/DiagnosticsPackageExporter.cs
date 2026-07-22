using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace LogaOutlookSync.Infrastructure;

/// <summary>
/// Erstellt ein Diagnosepaket (ZIP) aus Protokolldateien und nicht sensiblen Einstellungen,
/// das der Benutzer zur Fehleranalyse weitergeben kann. Enthält ausdrücklich niemals
/// Zugangsdaten, Session-Cookies oder Authentifizierungstoken.
/// </summary>
public sealed class DiagnosticsPackageExporter
{
    private readonly ILogger<DiagnosticsPackageExporter> _logger;

    public DiagnosticsPackageExporter(ILogger<DiagnosticsPackageExporter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Erstellt das Diagnosepaket und gibt den Pfad der erzeugten ZIP-Datei zurück.
    /// </summary>
    public string Export()
    {
        AppPaths.EnsureDirectoriesExist();

        var fileName = $"LogaOutlookSync-Diagnostics-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.zip";
        var zipPath = Path.Combine(AppPaths.DiagnosticsDirectory, fileName);

        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);

        if (Directory.Exists(AppPaths.LogsDirectory))
        {
            foreach (var logFile in Directory.EnumerateFiles(AppPaths.LogsDirectory, "*.log"))
            {
                archive.CreateEntryFromFile(logFile, Path.Combine("Logs", Path.GetFileName(logFile)));
            }
        }

        if (File.Exists(AppPaths.SettingsFilePath))
        {
            archive.CreateEntryFromFile(AppPaths.SettingsFilePath, "settings.json");
        }

        if (Directory.Exists(AppPaths.DebugArtifactsDirectory))
        {
            foreach (var artifact in Directory.EnumerateFiles(AppPaths.DebugArtifactsDirectory))
            {
                archive.CreateEntryFromFile(artifact, Path.Combine("DebugArtifacts", Path.GetFileName(artifact)));
            }
        }

        _logger.LogInformation("Diagnosepaket erstellt: {Path}", zipPath);
        return zipPath;
    }
}
