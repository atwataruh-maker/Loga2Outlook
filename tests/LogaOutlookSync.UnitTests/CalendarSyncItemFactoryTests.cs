using FluentAssertions;
using LogaOutlookSync.Application;
using LogaOutlookSync.Domain;
using LogaOutlookSync.Infrastructure;
using Xunit;

namespace LogaOutlookSync.UnitTests;

public sealed class CalendarSyncItemFactoryTests
{
    private static readonly TimeZoneInfo BerlinTimeZone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    private static AbsenceEntry CreateVacation(DateOnly start, DateOnly end) => new(
        "loga-1", AbsenceType.Vacation, start, end, null, null, true,
        AbsenceApprovalStatus.Approved, "Sommerurlaub",
        AbsenceFingerprint.Compute(AbsenceType.Vacation, start, end, null, null, AbsenceApprovalStatus.Approved, "Sommerurlaub"),
        DateTimeOffset.UtcNow);

    [Fact]
    public void Create_Vacation_IsAlwaysOutOfOfficePrivateWithNoReminder()
    {
        var factory = new CalendarSyncItemFactory(new AppSettings(), BerlinTimeZone);
        var absence = CreateVacation(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12));

        var item = factory.Create(absence, existingCalendarEntryId: null);

        item.ShowAs.Should().Be(CalendarShowAs.OutOfOffice);
        item.Sensitivity.Should().Be(CalendarSensitivity.Private);
        item.ReminderEnabled.Should().BeFalse();
        item.IsAllDay.Should().BeTrue();
    }

    [Fact]
    public void Create_UsesExclusiveEndDate_ForAllDayVacation()
    {
        var factory = new CalendarSyncItemFactory(new AppSettings(), BerlinTimeZone);
        var absence = CreateVacation(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12));

        var item = factory.Create(absence, existingCalendarEntryId: null);

        item.Start.Date.Should().Be(new DateTime(2026, 8, 10));
        item.End.Date.Should().Be(new DateTime(2026, 8, 13));
    }

    [Fact]
    public void Create_AppendsLogaSuffix_WhenConfigured()
    {
        var settings = new AppSettings { AppendLogaSuffixToSubject = true };
        var factory = new CalendarSyncItemFactory(settings, BerlinTimeZone);
        var absence = CreateVacation(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12));

        var item = factory.Create(absence, existingCalendarEntryId: null);

        item.Subject.Should().Be("Urlaub (LOGA)");
    }

    [Fact]
    public void Create_AppliesSubjectPrefix_WhenConfigured()
    {
        var settings = new AppSettings { SubjectPrefix = "[Firma] " };
        var factory = new CalendarSyncItemFactory(settings, BerlinTimeZone);
        var absence = CreateVacation(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12));

        var item = factory.Create(absence, existingCalendarEntryId: null);

        item.Subject.Should().Be("[Firma] Urlaub");
    }

    [Fact]
    public void Create_EmbedsSyncIdAndManagedByMarkerInBody()
    {
        var factory = new CalendarSyncItemFactory(new AppSettings(), BerlinTimeZone);
        var absence = CreateVacation(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12));

        var item = factory.Create(absence, existingCalendarEntryId: null);

        item.Body.Should().Contain("LOGA-SYNC-ID: loga-1");
        item.Body.Should().Contain("ManagedBy: LogaOutlookSync");
    }
}
