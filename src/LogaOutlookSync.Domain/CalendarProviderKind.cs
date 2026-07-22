namespace LogaOutlookSync.Domain;

/// <summary>
/// Auswahl der Outlook-Kalenderanbindung.
/// </summary>
public enum CalendarProviderKind
{
    /// <summary>
    /// Automatische Auswahl: bevorzugt Microsoft Graph, fällt bei fehlender
    /// Berechtigung oder App-Registrierung auf Outlook COM zurück.
    /// </summary>
    Automatic = 0,

    /// <summary>Microsoft Graph (bevorzugte Lösung).</summary>
    MicrosoftGraph,

    /// <summary>Outlook COM Interop für Outlook Classic (Fallback-Lösung).</summary>
    OutlookCom,
}
