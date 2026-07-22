using System.Text.Json;
using System.Text.Json.Serialization;
using LogaOutlookSync.Infrastructure;
using LogaOutlookSync.Loga.Exceptions;
using Microsoft.Extensions.Logging;

namespace LogaOutlookSync.Loga.Configuration;

/// <summary>
/// Lädt die zentrale LOGA-Selektor- und Navigationskonfiguration aus
/// <c>%LOCALAPPDATA%\LogaOutlookSync\Config\loga-selectors.json</c> bzw. <c>loga-navigation.json</c>.
/// Erfindet keine Standardwerte für LOGA-spezifische Selektoren - fehlt die Konfiguration,
/// wird ein aussagekräftiger Fehler geworfen, der auf den Einrichtungs-/Analysemodus verweist.
/// </summary>
public sealed class LogaSelectorsProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ILogger<LogaSelectorsProvider> _logger;
    private readonly string _selectorsFilePath;
    private readonly string _navigationFilePath;

    public LogaSelectorsProvider(
        ILogger<LogaSelectorsProvider> logger,
        string? selectorsFilePath = null,
        string? navigationFilePath = null)
    {
        _logger = logger;
        _selectorsFilePath = selectorsFilePath ?? AppPaths.SelectorsFilePath;
        _navigationFilePath = navigationFilePath ?? AppPaths.NavigationFilePath;
    }

    public LogaSelectorsConfig LoadSelectors()
    {
        var config = LoadJson<LogaSelectorsConfig>(_selectorsFilePath, "Selektor");

        if (string.IsNullOrWhiteSpace(config.Login.UsernameField)
            || string.IsNullOrWhiteSpace(config.Login.PasswordField)
            || string.IsNullOrWhiteSpace(config.Login.SubmitButton))
        {
            throw new LogaConfigurationException(
                $"Die Login-Selektoren in '{_selectorsFilePath}' sind unvollständig. " +
                "Bitte UsernameField, PasswordField und SubmitButton im Analysemodus ermitteln und eintragen.");
        }

        if (string.IsNullOrWhiteSpace(config.CalendarParsing.EntryContainer)
            || string.IsNullOrWhiteSpace(config.CalendarParsing.DateRange)
            || string.IsNullOrWhiteSpace(config.CalendarParsing.Type))
        {
            throw new LogaConfigurationException(
                $"Die Kalender-Selektoren in '{_selectorsFilePath}' sind unvollständig. " +
                "Bitte EntryContainer, DateRange und Type im Analysemodus ermitteln und eintragen.");
        }

        return config;
    }

    public LogaNavigationConfig LoadNavigation()
    {
        var config = LoadJson<LogaNavigationConfig>(_navigationFilePath, "Navigations");

        if (string.IsNullOrWhiteSpace(config.LoginPageUrl))
        {
            throw new LogaConfigurationException(
                $"Die Navigationskonfiguration in '{_navigationFilePath}' enthält keine LoginPageUrl.");
        }

        return config;
    }

    private T LoadJson<T>(string filePath, string configLabel) where T : new()
    {
        if (!File.Exists(filePath))
        {
            throw new LogaConfigurationException(
                $"Es wurde keine {configLabel}konfiguration unter '{filePath}' gefunden. " +
                "Bitte kopieren Sie die mitgelieferte Beispieldatei aus dem 'config'-Ordner, " +
                "ermitteln Sie die tatsächlichen Werte im sichtbaren Debug-/Analysemodus und tragen Sie diese ein.");
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var config = JsonSerializer.Deserialize<T>(json, SerializerOptions);
            if (config is null)
            {
                throw new LogaConfigurationException($"Die {configLabel}konfiguration unter '{filePath}' ist leer oder ungültig.");
            }

            return config;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Fehler beim Lesen der {ConfigLabel}konfiguration {Path}.", configLabel, filePath);
            throw new LogaConfigurationException(
                $"Die {configLabel}konfiguration unter '{filePath}' konnte nicht als JSON gelesen werden: {ex.Message}",
                ex);
        }
    }
}
