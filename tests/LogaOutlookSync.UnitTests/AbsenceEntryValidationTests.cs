using FluentAssertions;
using LogaOutlookSync.Domain;
using Xunit;

namespace LogaOutlookSync.UnitTests;

public sealed class AbsenceEntryValidationTests
{
    private static AbsenceEntry CreateAllDay(DateOnly start, DateOnly end) => new(
        "id", AbsenceType.Vacation, start, end, null, null, true, AbsenceApprovalStatus.Approved, null, "fp", DateTimeOffset.UtcNow);

    [Fact]
    public void Validate_Throws_WhenEndDateBeforeStartDate()
    {
        var entry = CreateAllDay(new DateOnly(2026, 8, 12), new DateOnly(2026, 8, 10));

        var act = entry.Validate;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Validate_Succeeds_ForValidMultiDayAllDayEntry()
    {
        var entry = CreateAllDay(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12));

        var act = entry.Validate;

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_Throws_WhenAllDayEntryHasTimes()
    {
        var entry = new AbsenceEntry(
            "id", AbsenceType.FlexTime, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 10),
            new TimeOnly(8, 0), new TimeOnly(12, 0), true, AbsenceApprovalStatus.Approved, null, "fp", DateTimeOffset.UtcNow);

        var act = entry.Validate;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Validate_Throws_WhenTimedEntryEndTimeNotAfterStartTime()
    {
        var entry = new AbsenceEntry(
            "id", AbsenceType.FlexTime, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 10),
            new TimeOnly(12, 0), new TimeOnly(8, 0), false, AbsenceApprovalStatus.Approved, null, "fp", DateTimeOffset.UtcNow);

        var act = entry.Validate;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Validate_Throws_WhenTimedEntrySpansMultipleDays()
    {
        var entry = new AbsenceEntry(
            "id", AbsenceType.FlexTime, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 11),
            new TimeOnly(8, 0), new TimeOnly(12, 0), false, AbsenceApprovalStatus.Approved, null, "fp", DateTimeOffset.UtcNow);

        var act = entry.Validate;

        act.Should().Throw<InvalidOperationException>();
    }
}
