namespace LogaOutlookSync.Application;

/// <summary>Abstraktion über die aktuelle Zeit, damit Zeitpunkt-abhängige Logik testbar ist.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>Das heutige Datum in der lokalen Windows-Zeitzone des Benutzers.</summary>
    DateOnly TodayLocal { get; }
}

/// <summary>Produktive <see cref="IClock"/>-Implementierung auf Basis der Systemzeit.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly TodayLocal => DateOnly.FromDateTime(DateTime.Now);
}
