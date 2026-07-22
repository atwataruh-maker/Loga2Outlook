using FluentAssertions;
using LogaOutlookSync.Application;
using Xunit;

namespace LogaOutlookSync.UnitTests;

public sealed class AllDayRangeCalculatorTests
{
    private static TimeZoneInfo BerlinTimeZone => TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    [Fact]
    public void ComputeAllDayRange_EndIsExclusive_OneDayAfterLastInclusiveDay()
    {
        var (start, end) = AllDayRangeCalculator.ComputeAllDayRange(
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12), BerlinTimeZone);

        start.Date.Should().Be(new DateTime(2026, 8, 10));
        end.Date.Should().Be(new DateTime(2026, 8, 13));
    }

    [Fact]
    public void ComputeAllDayRange_SingleDayVacation_EndIsNextDay()
    {
        var (start, end) = AllDayRangeCalculator.ComputeAllDayRange(
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 10), BerlinTimeZone);

        start.Date.Should().Be(new DateTime(2026, 8, 10));
        end.Date.Should().Be(new DateTime(2026, 8, 11));
    }

    [Fact]
    public void ComputeAllDayRange_AcrossDaylightSavingTransition_UsesCorrectOffsetOnEachSide()
    {
        // In Germany, DST ends on the last Sunday of October (2026-10-25): clocks move from
        // UTC+2 back to UTC+1. A vacation spanning this date must use +02:00 for the start
        // and +01:00 for the (already shifted) exclusive end.
        var (start, end) = AllDayRangeCalculator.ComputeAllDayRange(
            new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 26), BerlinTimeZone);

        start.Offset.Should().Be(TimeSpan.FromHours(2));
        end.Offset.Should().Be(TimeSpan.FromHours(1));
        end.Date.Should().Be(new DateTime(2026, 10, 27));
    }

    [Fact]
    public void ComputeTimedRange_UsesGivenTimesOnSingleDay()
    {
        var (start, end) = AllDayRangeCalculator.ComputeTimedRange(
            new DateOnly(2026, 8, 15), new TimeOnly(8, 0), new TimeOnly(12, 0), BerlinTimeZone);

        start.Should().Be(new DateTimeOffset(2026, 8, 15, 8, 0, 0, TimeSpan.FromHours(2)));
        end.Should().Be(new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.FromHours(2)));
    }
}
