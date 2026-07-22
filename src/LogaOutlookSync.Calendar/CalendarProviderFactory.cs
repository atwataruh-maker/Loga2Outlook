using LogaOutlookSync.Calendar.Graph;
using LogaOutlookSync.Calendar.OutlookCom;
using LogaOutlookSync.Domain;
using LogaOutlookSync.Infrastructure;
using Microsoft.Extensions.Logging;

namespace LogaOutlookSync.Calendar;

/// <summary>
/// Wählt anhand der Einstellungen die zu verwendende <see cref="ICalendarProvider"/>-Implementierung.
/// Im Modus <see cref="CalendarProviderKind.Automatic"/> wird Microsoft Graph bevorzugt und nur
/// auf Outlook COM zurückgefallen, wenn keine Graph-App-Registrierung konfiguriert ist.
/// </summary>
public sealed class CalendarProviderFactory
{
    private readonly Func<MicrosoftGraphCalendarProvider> _graphProviderFactory;
    private readonly Func<OutlookComCalendarProvider> _comProviderFactory;
    private readonly AppSettings _settings;
    private readonly ILogger<CalendarProviderFactory> _logger;

    public CalendarProviderFactory(
        Func<MicrosoftGraphCalendarProvider> graphProviderFactory,
        Func<OutlookComCalendarProvider> comProviderFactory,
        AppSettings settings,
        ILogger<CalendarProviderFactory> logger)
    {
        _graphProviderFactory = graphProviderFactory;
        _comProviderFactory = comProviderFactory;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>Erstellt die gemäß Einstellungen zu verwendende Kalenderanbindung.</summary>
    public ICalendarProvider Create()
    {
        return _settings.CalendarProvider switch
        {
            CalendarProviderKind.MicrosoftGraph => _graphProviderFactory(),
            CalendarProviderKind.OutlookCom => _comProviderFactory(),
            CalendarProviderKind.Automatic => CreateAutomatic(),
            _ => throw new InvalidOperationException($"Unbekannte Kalenderanbindung '{_settings.CalendarProvider}'."),
        };
    }

    private ICalendarProvider CreateAutomatic()
    {
        if (!string.IsNullOrWhiteSpace(_settings.GraphClientId))
        {
            _logger.LogInformation("Automatische Auswahl der Kalenderanbindung: Microsoft Graph.");
            return _graphProviderFactory();
        }

        _logger.LogInformation(
            "Automatische Auswahl der Kalenderanbindung: Outlook COM (keine Microsoft-Graph-App-Registrierung konfiguriert).");
        return _comProviderFactory();
    }
}
