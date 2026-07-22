using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace LogaOutlookSync.Infrastructure.Logging;

/// <summary>
/// Dekoriert einen Serilog-Sink und ersetzt bekannte Muster sensibler Daten in der
/// gerenderten Log-Nachricht, bevor sie an den eigentlichen Sink weitergereicht wird.
/// </summary>
public sealed class RedactingSink : ILogEventSink
{
    private static readonly MessageTemplateParser TemplateParser = new();

    private readonly ILogEventSink _inner;

    public RedactingSink(ILogEventSink inner)
    {
        _inner = inner;
    }

    public void Emit(LogEvent logEvent)
    {
        var rendered = logEvent.RenderMessage();
        var redacted = SensitiveDataScrubber.Redact(rendered);

        if (string.Equals(rendered, redacted, StringComparison.Ordinal))
        {
            _inner.Emit(logEvent);
            return;
        }

        var escapedTemplateText = redacted.Replace("{", "{{").Replace("}", "}}");
        var safeTemplate = TemplateParser.Parse(escapedTemplateText);
        var safeEvent = new LogEvent(
            logEvent.Timestamp,
            logEvent.Level,
            logEvent.Exception,
            safeTemplate,
            Array.Empty<LogEventProperty>());

        _inner.Emit(safeEvent);
    }
}
