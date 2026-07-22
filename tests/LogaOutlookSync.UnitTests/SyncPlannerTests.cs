using FluentAssertions;
using LogaOutlookSync.Application;
using LogaOutlookSync.Domain;
using LogaOutlookSync.Infrastructure;
using Xunit;

namespace LogaOutlookSync.UnitTests;

public sealed class SyncPlannerTests
{
    private static readonly TimeZoneInfo BerlinTimeZone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    private static SyncPlanner CreatePlanner(AppSettings? settings = null)
    {
        var factory = new CalendarSyncItemFactory(settings ?? new AppSettings(), BerlinTimeZone);
        return new SyncPlanner(factory);
    }

    private static AbsenceEntry CreateVacation(string sourceId, DateOnly start, DateOnly end, string displayText = "Urlaub")
    {
        var fingerprint = AbsenceFingerprint.Compute(AbsenceType.Vacation, start, end, null, null, AbsenceApprovalStatus.Approved, displayText);
        return new AbsenceEntry(sourceId, AbsenceType.Vacation, start, end, null, null, true, AbsenceApprovalStatus.Approved, displayText, fingerprint, DateTimeOffset.UtcNow);
    }

    private static ManagedCalendarEntry CreateManagedEntryFor(AbsenceEntry absence, AppSettings? settings = null, string calendarEntryId = "evt-1")
    {
        var item = new CalendarSyncItemFactory(settings ?? new AppSettings(), BerlinTimeZone).Create(absence, null);
        return new ManagedCalendarEntry(
            calendarEntryId, absence.SourceId, IsManagedByApp: true, item.Subject, item.Start, item.End, item.IsAllDay,
            item.Category, item.ShowAs, item.SourceFingerprint, DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Plan_NewApprovedAbsenceWithoutExistingEntry_IsCreate()
    {
        var absence = CreateVacation("loga-1", new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12));
        var plan = CreatePlanner().Plan(new[] { absence }, Array.Empty<ManagedCalendarEntry>(), DeletionPolicy.ConfirmBeforeDelete);

        plan.Should().ContainSingle(p => p.Operation == SyncOperationKind.Create && p.SyncId == "loga-1");
    }

    [Fact]
    public void Plan_UnchangedAbsenceMatchingExistingManagedEntry_IsUnchanged()
    {
        var absence = CreateVacation("loga-2", new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12));
        var managedEntry = CreateManagedEntryFor(absence);

        var plan = CreatePlanner().Plan(new[] { absence }, new[] { managedEntry }, DeletionPolicy.ConfirmBeforeDelete);

        plan.Should().ContainSingle(p => p.Operation == SyncOperationKind.Unchanged && p.SyncId == "loga-2");
    }

    [Fact]
    public void Plan_ChangedDateRange_IsUpdate()
    {
        var originalAbsence = CreateVacation("loga-3", new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12));
        var managedEntry = CreateManagedEntryFor(originalAbsence);

        var changedAbsence = CreateVacation("loga-3", new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 14));

        var plan = CreatePlanner().Plan(new[] { changedAbsence }, new[] { managedEntry }, DeletionPolicy.ConfirmBeforeDelete);

        plan.Should().ContainSingle(p => p.Operation == SyncOperationKind.Update && p.SyncId == "loga-3");
    }

    [Fact]
    public void Plan_ManagedEntryWithoutMatchingAbsence_IsDeleteByDefault()
    {
        var goneAbsence = CreateVacation("loga-4", new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12));
        var managedEntry = CreateManagedEntryFor(goneAbsence);

        var plan = CreatePlanner().Plan(Array.Empty<AbsenceEntry>(), new[] { managedEntry }, DeletionPolicy.ConfirmBeforeDelete);

        plan.Should().ContainSingle(p => p.Operation == SyncOperationKind.Delete && p.SyncId == "loga-4");
    }

    [Fact]
    public void Plan_ManagedEntryWithoutMatchingAbsence_IsConflict_WhenDeletionPolicyIsReportOnly()
    {
        var goneAbsence = CreateVacation("loga-5", new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12));
        var managedEntry = CreateManagedEntryFor(goneAbsence);

        var plan = CreatePlanner().Plan(Array.Empty<AbsenceEntry>(), new[] { managedEntry }, DeletionPolicy.ReportOnly);

        plan.Should().ContainSingle(p => p.Operation == SyncOperationKind.Conflict && p.SyncId == "loga-5");
    }

    [Fact]
    public void Plan_DuplicateSyncIdAmongManagedEntries_IsFlaggedAsConflict()
    {
        var absence = CreateVacation("loga-6", new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12));
        var firstManagedEntry = CreateManagedEntryFor(absence, calendarEntryId: "evt-a");
        var duplicateManagedEntry = CreateManagedEntryFor(absence, calendarEntryId: "evt-b");

        var plan = CreatePlanner().Plan(new[] { absence }, new[] { firstManagedEntry, duplicateManagedEntry }, DeletionPolicy.ConfirmBeforeDelete);

        plan.Should().Contain(p => p.Operation == SyncOperationKind.Conflict && p.SyncId == "loga-6");
    }

    [Fact]
    public void Plan_NeverTouchesEntriesNotManagedByThisApplication()
    {
        var absence = CreateVacation("loga-7", new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12));
        var unmanagedEntry = new ManagedCalendarEntry(
            "evt-manual", SyncId: null, IsManagedByApp: false, "Privater Urlaub (manuell erstellt)",
            new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.FromHours(2)),
            true, null, CalendarShowAs.OutOfOffice, SourceFingerprint: null, LastModifiedUtc: null);

        var plan = CreatePlanner().Plan(new[] { absence }, new[] { unmanagedEntry }, DeletionPolicy.ConfirmBeforeDelete);

        // The unmanaged entry must never appear as a Delete/Update target; the new absence is simply created.
        plan.Should().ContainSingle(p => p.Operation == SyncOperationKind.Create && p.SyncId == "loga-7");
        plan.Should().NotContain(p => p.ExistingEntry == unmanagedEntry);
    }

    [Fact]
    public void Plan_IgnoresAbsenceTypesOutsideTheSyncPolicy()
    {
        var sickness = new AbsenceEntry(
            "loga-8", AbsenceType.Sickness, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 10), null, null, true,
            AbsenceApprovalStatus.Approved, "Krank", "irrelevant-fingerprint", DateTimeOffset.UtcNow);

        var plan = CreatePlanner().Plan(new[] { sickness }, Array.Empty<ManagedCalendarEntry>(), DeletionPolicy.ConfirmBeforeDelete);

        plan.Should().BeEmpty();
    }
}
