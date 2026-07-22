namespace LogaOutlookSync.Domain;

/// <summary>
/// Anzeigen-als-Status eines Kalendertermins, providerunabhängig.
/// </summary>
public enum CalendarShowAs
{
    Free,
    Tentative,
    Busy,

    /// <summary>
    /// Abwesend. Verpflichtender Status für alle von dieser Anwendung verwalteten
    /// Urlaubs- und Gleitzeittermine (Microsoft Graph: <c>showAs = "oof"</c>,
    /// Outlook COM: <c>BusyStatus = olOutOfOffice</c>).
    /// </summary>
    OutOfOffice,

    WorkingElsewhere,
}
