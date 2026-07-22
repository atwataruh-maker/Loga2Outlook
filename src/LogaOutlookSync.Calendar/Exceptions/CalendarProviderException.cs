namespace LogaOutlookSync.Calendar.Exceptions;

/// <summary>Basisklasse für Fehler einer <see cref="ICalendarProvider"/>-Implementierung.</summary>
public abstract class CalendarProviderException : Exception
{
    protected CalendarProviderException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Authentifizierung bei Microsoft Graph fehlgeschlagen oder nicht konfiguriert.</summary>
public sealed class GraphAuthenticationException : CalendarProviderException
{
    public GraphAuthenticationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>Ein Microsoft-Graph-Aufruf zur Kalenderverwaltung ist fehlgeschlagen.</summary>
public sealed class GraphCalendarException : CalendarProviderException
{
    public GraphCalendarException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>Ein Zugriff auf Outlook über COM Interop ist fehlgeschlagen.</summary>
public sealed class OutlookComException : CalendarProviderException
{
    public OutlookComException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
