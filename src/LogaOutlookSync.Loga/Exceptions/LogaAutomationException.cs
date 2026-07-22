namespace LogaOutlookSync.Loga.Exceptions;

/// <summary>Basisklasse aller Fehler, die während der LOGA-Browserautomatisierung auftreten können.</summary>
public abstract class LogaAutomationException : Exception
{
    protected LogaAutomationException(string message, string? failedStep, string? selector, Exception? innerException)
        : base(message, innerException)
    {
        FailedStep = failedStep;
        Selector = selector;
    }

    /// <summary>Name des Navigations- bzw. Verarbeitungsschritts, der fehlgeschlagen ist, sofern zutreffend.</summary>
    public string? FailedStep { get; }

    /// <summary>Der Selektor, der nicht (mehr) funktioniert hat, sofern zutreffend.</summary>
    public string? Selector { get; }
}

/// <summary>Die LOGA-Selektor- oder Navigationskonfiguration fehlt oder ist ungültig.</summary>
public sealed class LogaConfigurationException : LogaAutomationException
{
    public LogaConfigurationException(string message, Exception? innerException = null)
        : base(message, failedStep: null, selector: null, innerException)
    {
    }
}

/// <summary>LOGA war nicht erreichbar (Netzwerkfehler, DNS, Timeout beim initialen Seitenaufruf).</summary>
public sealed class LogaConnectivityException : LogaAutomationException
{
    public LogaConnectivityException(string message, Exception? innerException = null)
        : base(message, failedStep: "Verbindungsaufbau", selector: null, innerException)
    {
    }
}

/// <summary>Die Anmeldung bei LOGA ist fehlgeschlagen (falsche Zugangsdaten, gesperrtes Konto, MFA, ...).</summary>
public sealed class LogaLoginException : LogaAutomationException
{
    public LogaLoginException(string message, string? failedStep = null, string? selector = null, Exception? innerException = null)
        : base(message, failedStep, selector, innerException)
    {
    }
}

/// <summary>Ein konfigurierter Navigationsschritt konnte nicht ausgeführt werden.</summary>
public sealed class LogaNavigationException : LogaAutomationException
{
    public LogaNavigationException(string message, string? failedStep, string? selector, Exception? innerException = null)
        : base(message, failedStep, selector, innerException)
    {
    }
}

/// <summary>Der HTML-Snapshot des Kalenders konnte nicht eindeutig ausgewertet werden.</summary>
public sealed class LogaParsingException : LogaAutomationException
{
    public LogaParsingException(string message, string? failedStep = null, string? selector = null, Exception? innerException = null)
        : base(message, failedStep, selector, innerException)
    {
    }
}

/// <summary>Der Browser (Microsoft Edge) oder Playwright konnte nicht gestartet bzw. initialisiert werden.</summary>
public sealed class LogaBrowserException : LogaAutomationException
{
    public LogaBrowserException(string message, Exception? innerException = null)
        : base(message, failedStep: "Browserstart", selector: null, innerException)
    {
    }
}
