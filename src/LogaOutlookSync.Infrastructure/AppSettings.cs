using LogaOutlookSync.Domain;

namespace LogaOutlookSync.Infrastructure;

/// <summary>
/// Nicht sensible, benutzerspezifische Einstellungen. Wird als JSON unterhalb von
/// <see cref="AppPaths.SettingsFilePath"/> gespeichert. Enthält ausdrücklich keine
/// Zugangsdaten - diese werden ausschließlich über <c>ICredentialStore</c> verwaltet.
/// </summary>
public sealed record AppSettings
{
    /// <summary>LOGA-Webadresse, z. B. die Adresse des persönlichen Kalenders nach dem Login.</summary>
    public string LogaBaseUrl { get; init; } = "https://dedalus.pi-asp.de/loga3/private/layout?action=afterlogin";

    /// <summary>LOGA-Benutzername (nicht sensibel im Sinne der Speicherung, dient nur der Anzeige/Vorbelegung).</summary>
    public string LogaUserName { get; init; } = string.Empty;

    /// <summary>Bevorzugte Kalenderanbindung.</summary>
    public CalendarProviderKind CalendarProvider { get; init; } = CalendarProviderKind.Automatic;

    /// <summary>Anzeigename des gewählten Ziel-Kalenders (zur Anzeige in der Oberfläche).</summary>
    public string? TargetCalendarDisplayName { get; init; }

    /// <summary>
    /// Providerspezifische ID des Ziel-Kalenders (Graph Calendar-ID bzw. Name des Outlook-COM-Ordners).
    /// </summary>
    public string? TargetCalendarId { get; init; }

    /// <summary>Browser sichtbar (Debug-Modus) oder unsichtbar (Headless) betreiben.</summary>
    public bool BrowserVisible { get; init; } = true;

    /// <summary>Anzahl Tage in der Vergangenheit, die synchronisiert werden.</summary>
    public int SyncPastDays { get; init; } = 30;

    /// <summary>Anzahl Monate in der Zukunft, die synchronisiert werden.</summary>
    public int SyncFutureMonths { get; init; } = 18;

    /// <summary>Optionaler Präfix für Outlook-Terminbetreffs, z. B. "[LOGA] ".</summary>
    public string? SubjectPrefix { get; init; }

    /// <summary>Ob dem Betreff der Zusatz "(LOGA)" angehängt werden soll, z. B. "Urlaub (LOGA)".</summary>
    public bool AppendLogaSuffixToSubject { get; init; }

    /// <summary>Terminbetreff für Urlaub, ohne Präfix/Suffix.</summary>
    public string VacationSubject { get; init; } = "Urlaub";

    /// <summary>Terminbetreff für Gleitzeit, ohne Präfix/Suffix.</summary>
    public string FlexTimeSubject { get; init; } = "Gleitzeit";

    /// <summary>Outlook-Kategorie für Urlaubstermine.</summary>
    public string VacationCategory { get; init; } = "LOGA Urlaub";

    /// <summary>Outlook-Kategorie für Gleitzeittermine.</summary>
    public string FlexTimeCategory { get; init; } = "LOGA Gleitzeit";

    /// <summary>Verhalten bei nicht mehr vorhandenen bzw. stornierten LOGA-Einträgen.</summary>
    public DeletionPolicy DeletionPolicy { get; init; } = DeletionPolicy.ConfirmBeforeDelete;

    /// <summary>Aufbewahrungsdauer der Protokolldateien in Tagen.</summary>
    public int LogRetentionDays { get; init; } = 30;

    /// <summary>Ob der Einrichtungsassistent bereits erfolgreich abgeschlossen wurde.</summary>
    public bool FirstRunCompleted { get; init; }

    /// <summary>Berechnet den Startzeitpunkt des zu synchronisierenden Fensters relativ zu <paramref name="today"/>.</summary>
    public DateOnly GetSyncWindowStart(DateOnly today) => today.AddDays(-Math.Abs(SyncPastDays));

    /// <summary>Berechnet den Endzeitpunkt des zu synchronisierenden Fensters relativ zu <paramref name="today"/>.</summary>
    public DateOnly GetSyncWindowEnd(DateOnly today) => today.AddMonths(Math.Abs(SyncFutureMonths));
}
