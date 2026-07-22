using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace LogaOutlookSync.Infrastructure;

/// <summary>
/// Lädt und speichert <see cref="AppSettings"/> als JSON-Datei unterhalb des Benutzerprofils.
/// Schreibt atomar (temporäre Datei + Umbenennung), damit ein Absturz während des Schreibens
/// keine beschädigte Konfigurationsdatei hinterlässt.
/// </summary>
public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;
    private readonly ILogger<AppSettingsStore> _logger;

    public AppSettingsStore(ILogger<AppSettingsStore> logger, string? filePath = null)
    {
        _logger = logger;
        _filePath = filePath ?? AppPaths.SettingsFilePath;
    }

    /// <summary>Lädt die Einstellungen, oder liefert die Standardwerte, wenn noch keine Datei existiert.</summary>
    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogInformation("Keine gespeicherten Einstellungen gefunden, verwende Standardwerte.");
            return new AppSettings();
        }

        await using var stream = File.OpenRead(_filePath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        if (settings is null)
        {
            _logger.LogWarning("Einstellungsdatei {Path} konnte nicht gelesen werden, verwende Standardwerte.", _filePath);
            return new AppSettings();
        }

        return settings;
    }

    /// <summary>Speichert die Einstellungen atomar.</summary>
    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempFilePath = _filePath + ".tmp";
        await using (var stream = File.Create(tempFilePath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(tempFilePath, _filePath, overwrite: true);
        _logger.LogInformation("Einstellungen gespeichert unter {Path}.", _filePath);
    }
}
