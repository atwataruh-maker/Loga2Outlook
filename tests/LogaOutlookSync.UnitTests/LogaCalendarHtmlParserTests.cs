using FluentAssertions;
using LogaOutlookSync.Domain;
using LogaOutlookSync.Loga;
using LogaOutlookSync.Loga.Configuration;
using Xunit;

namespace LogaOutlookSync.UnitTests;

public sealed class LogaCalendarHtmlParserTests
{
    private static CalendarParsingSelectors CreateSelectors() => new()
    {
        EntryContainer = ".entry",
        SourceIdAttribute = "data-entry-id",
        DateRange = ".entry-date",
        TimeRange = ".entry-time",
        Type = ".entry-type",
        Status = ".entry-status",
        DisplayText = ".entry-text",
        DateFormat = "dd.MM.yyyy",
        TimeFormat = "HH:mm",
        RangeSeparator = " - ",
        CultureName = "de-DE",
        TypeTextMapping = new Dictionary<string, string>
        {
            ["Urlaub"] = nameof(AbsenceType.Vacation),
            ["Gleitzeit"] = nameof(AbsenceType.FlexTime),
            ["Krankheit"] = nameof(AbsenceType.Sickness),
        },
        StatusTextMapping = new Dictionary<string, string>
        {
            ["genehmigt"] = nameof(AbsenceApprovalStatus.Approved),
            ["beantragt"] = nameof(AbsenceApprovalStatus.Pending),
            ["abgelehnt"] = nameof(AbsenceApprovalStatus.Rejected),
            ["storniert"] = nameof(AbsenceApprovalStatus.Cancelled),
        },
    };

    private static string LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "sample-calendar.html");
        return File.ReadAllText(path);
    }

    [Fact]
    public void Parse_RecognizesMultiDayVacationEntry()
    {
        var entries = LogaCalendarHtmlParser.Parse(LoadFixture(), CreateSelectors());

        var vacation = entries.Single(e => e.SourceId == "loga-1001");

        vacation.Type.Should().Be(AbsenceType.Vacation);
        vacation.StartDate.Should().Be(new DateOnly(2026, 8, 10));
        vacation.EndDate.Should().Be(new DateOnly(2026, 8, 12));
        vacation.IsAllDay.Should().BeTrue();
        vacation.ApprovalStatus.Should().Be(AbsenceApprovalStatus.Approved);
        vacation.DisplayText.Should().Be("Sommerurlaub");
    }

    [Fact]
    public void Parse_RecognizesHourlyFlexTimeEntryWithExplicitTimes()
    {
        var entries = LogaCalendarHtmlParser.Parse(LoadFixture(), CreateSelectors());

        var flexTime = entries.Single(e => e.SourceId == "loga-1002");

        flexTime.Type.Should().Be(AbsenceType.FlexTime);
        flexTime.IsAllDay.Should().BeFalse();
        flexTime.StartDate.Should().Be(new DateOnly(2026, 8, 15));
        flexTime.EndDate.Should().Be(new DateOnly(2026, 8, 15));
        flexTime.StartTime.Should().Be(new TimeOnly(8, 0));
        flexTime.EndTime.Should().Be(new TimeOnly(12, 0));
    }

    [Fact]
    public void Parse_TreatsFlexTimeWithoutTimeRangeAsAllDay()
    {
        var entries = LogaCalendarHtmlParser.Parse(LoadFixture(), CreateSelectors());

        var pendingFlexTime = entries.Single(e => e.SourceId == "loga-1003");

        pendingFlexTime.IsAllDay.Should().BeTrue();
        pendingFlexTime.StartTime.Should().BeNull();
        pendingFlexTime.EndTime.Should().BeNull();
        pendingFlexTime.ApprovalStatus.Should().Be(AbsenceApprovalStatus.Pending);
    }

    [Fact]
    public void Parse_GeneratesDeterministicFallbackSourceId_WhenNoIdAttributePresent()
    {
        var entries = LogaCalendarHtmlParser.Parse(LoadFixture(), CreateSelectors());

        var sicknessEntry = entries.Single(e => e.Type == AbsenceType.Sickness);

        sicknessEntry.SourceId.Should().StartWith("loga-fallback-");

        // Parsing again must yield the exact same fallback ID (stable identity across syncs).
        var reparsed = LogaCalendarHtmlParser.Parse(LoadFixture(), CreateSelectors());
        var reparsedSicknessEntry = reparsed.Single(e => e.Type == AbsenceType.Sickness);
        reparsedSicknessEntry.SourceId.Should().Be(sicknessEntry.SourceId);
    }

    [Fact]
    public void Parse_ReturnsAllFourEntriesRegardlessOfApprovalStatus()
    {
        var entries = LogaCalendarHtmlParser.Parse(LoadFixture(), CreateSelectors());

        entries.Should().HaveCount(4);
    }
}
