using Serilog;
using Serilog.Core;

namespace LogaOutlookSync.Infrastructure.Logging;

/// <summary>
/// Konfiguriert das anwendungsweite Serilog-Logging: Rollierende Tagesdateien unterhalb
/// von <see cref="AppPaths.LogsDirectory"/> mit begrenzter Aufbewahrungsdauer sowie eine
/// Redaktionsstufe, die bekannte Muster sensibler Daten aus jeder Nachricht entfernt.
/// </summary>
public static class LoggingSetup
{
    /// <summary>Erstellt den konfigurierten Root-Logger. Muss genau einmal beim Anwendungsstart aufgerufen werden.</summary>
    /// <param name="logRetentionDays">Maximale Anzahl aufzubewahrender Tagesdateien.</param>
    public static Serilog.ILogger CreateLogger(int logRetentionDays)
    {
        AppPaths.EnsureDirectoriesExist();

        var logFilePath = Path.Combine(AppPaths.LogsDirectory, "logaoutlooksync-.log");

        // Innerer Logger schreibt tatsächlich in Datei und Debug-Ausgabe. Er wird komplett
        // hinter der Redaktionsstufe versteckt, damit kein Code versehentlich direkt an ihn
        // vorbei protokollieren kann.
        Logger innerLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: Math.Max(1, logRetentionDays),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Debug()
            .CreateLogger();

        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Sink(new RedactingSink(innerLogger))
            .CreateLogger();
    }
}
