namespace LogaOutlookSync.Domain;

/// <summary>Beschreibt einen für den Benutzer auswählbaren Kalender (Graph-Kalender bzw. Outlook-COM-Ordner).</summary>
/// <param name="Id">Providerspezifische ID bzw. Ordnername, zur Speicherung in <c>AppSettings.TargetCalendarId</c>.</param>
/// <param name="DisplayName">Für Menschen lesbarer Name, zur Anzeige im Einrichtungsassistenten.</param>
/// <param name="IsDefault">Ob es sich um den Standardkalender des Kontos handelt.</param>
public sealed record CalendarDescriptor(string Id, string DisplayName, bool IsDefault);
