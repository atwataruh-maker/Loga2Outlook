namespace LogaOutlookSync.Application;

/// <summary>
/// Berechnet die tatsächlich zu speichernden Start-/Endzeitpunkte für Outlook-/Graph-Termine
/// unter Berücksichtigung des exklusiven Enddatums bei Ganztagsterminen sowie der Zeitzone
/// und Sommerzeit des Benutzers. Ein Urlaub vom 10. bis einschließlich 12. August wird
/// beispielsweise als Start 10.08. 00:00 Uhr, Ende 13.08. 00:00 Uhr gespeichert, damit der
/// letzte Urlaubstag im Kalender nicht fehlt.
/// </summary>
public static class AllDayRangeCalculator
{
    /// <summary>
    /// Berechnet den Zeitraum für einen ganztägigen Termin. <paramref name="endDateInclusive"/>
    /// ist der letzte tatsächliche Tag der Abwesenheit; das zurückgegebene Ende liegt einen Tag
    /// später um 00:00 Uhr (exklusiv), wie von Outlook/Graph für Ganztagstermine erwartet.
    /// </summary>
    public static (DateTimeOffset Start, DateTimeOffset End) ComputeAllDayRange(
        DateOnly startDate,
        DateOnly endDateInclusive,
        TimeZoneInfo timeZone)
    {
        var startLocal = startDate.ToDateTime(TimeOnly.MinValue);
        var endExclusiveLocal = endDateInclusive.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var start = new DateTimeOffset(startLocal, timeZone.GetUtcOffset(startLocal));
        var end = new DateTimeOffset(endExclusiveLocal, timeZone.GetUtcOffset(endExclusiveLocal));

        return (start, end);
    }

    /// <summary>Berechnet den Zeitraum für einen stundenweisen Termin an einem einzelnen Tag.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) ComputeTimedRange(
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        TimeZoneInfo timeZone)
    {
        var startLocal = date.ToDateTime(startTime);
        var endLocal = date.ToDateTime(endTime);

        var start = new DateTimeOffset(startLocal, timeZone.GetUtcOffset(startLocal));
        var end = new DateTimeOffset(endLocal, timeZone.GetUtcOffset(endLocal));

        return (start, end);
    }
}
