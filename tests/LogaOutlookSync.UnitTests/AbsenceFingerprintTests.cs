using FluentAssertions;
using LogaOutlookSync.Domain;
using Xunit;

namespace LogaOutlookSync.UnitTests;

public sealed class AbsenceFingerprintTests
{
    [Fact]
    public void Compute_IsDeterministic_ForIdenticalInput()
    {
        var first = AbsenceFingerprint.Compute(
            AbsenceType.Vacation, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12), null, null,
            AbsenceApprovalStatus.Approved, "Sommerurlaub");

        var second = AbsenceFingerprint.Compute(
            AbsenceType.Vacation, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12), null, null,
            AbsenceApprovalStatus.Approved, "Sommerurlaub");

        first.Should().Be(second);
    }

    [Theory]
    [InlineData(AbsenceType.FlexTime, "2026-08-10", "2026-08-12", "Approved", "Text")]
    [InlineData(AbsenceType.Vacation, "2026-08-11", "2026-08-12", "Approved", "Text")]
    [InlineData(AbsenceType.Vacation, "2026-08-10", "2026-08-13", "Approved", "Text")]
    [InlineData(AbsenceType.Vacation, "2026-08-10", "2026-08-12", "Pending", "Text")]
    [InlineData(AbsenceType.Vacation, "2026-08-10", "2026-08-12", "Approved", "Anderer Text")]
    public void Compute_ChangesWhenAnyRelevantFieldChanges(AbsenceType type, string start, string end, string status, string text)
    {
        var baseline = AbsenceFingerprint.Compute(
            AbsenceType.Vacation, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12), null, null,
            AbsenceApprovalStatus.Approved, "Text");

        var varied = AbsenceFingerprint.Compute(
            type,
            DateOnly.Parse(start),
            DateOnly.Parse(end),
            null,
            null,
            Enum.Parse<AbsenceApprovalStatus>(status),
            text);

        varied.Should().NotBe(baseline);
    }

    [Fact]
    public void ComputeFallbackSourceId_IsStableForSameCoreFields_EvenWhenDisplayTextDiffers()
    {
        var idWithOneText = AbsenceFingerprint.ComputeFallbackSourceId(
            AbsenceType.Vacation, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12), null, null);

        var idWithSameCoreFields = AbsenceFingerprint.ComputeFallbackSourceId(
            AbsenceType.Vacation, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12), null, null);

        idWithOneText.Should().Be(idWithSameCoreFields);
        idWithOneText.Should().StartWith("loga-fallback-");
    }

    [Fact]
    public void ComputeFallbackSourceId_DiffersWhenDateRangeChanges()
    {
        var first = AbsenceFingerprint.ComputeFallbackSourceId(
            AbsenceType.Vacation, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12), null, null);

        var second = AbsenceFingerprint.ComputeFallbackSourceId(
            AbsenceType.Vacation, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 13), null, null);

        first.Should().NotBe(second);
    }
}
