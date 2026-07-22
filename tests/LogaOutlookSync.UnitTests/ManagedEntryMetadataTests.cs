using FluentAssertions;
using LogaOutlookSync.Domain;
using Xunit;

namespace LogaOutlookSync.UnitTests;

public sealed class ManagedEntryMetadataTests
{
    [Fact]
    public void BuildTechnicalFooter_ContainsSyncIdAndManagedByMarker()
    {
        var footer = ManagedEntryMetadata.BuildTechnicalFooter("loga-42", "fingerprint-abc");

        footer.Should().Contain("LOGA-SYNC-ID: loga-42");
        footer.Should().Contain("ManagedBy: LogaOutlookSync");
    }

    [Fact]
    public void TryExtractSyncIdFromBody_RoundTripsThroughBuildTechnicalFooter()
    {
        var footer = ManagedEntryMetadata.BuildTechnicalFooter("loga-99", "fingerprint-xyz");
        var body = "Urlaub in der Karibik" + Environment.NewLine + footer;

        var extracted = ManagedEntryMetadata.TryExtractSyncIdFromBody(body);

        extracted.Should().Be("loga-99");
    }

    [Fact]
    public void TryExtractSyncIdFromBody_ReturnsNull_WhenNoFooterPresent()
    {
        ManagedEntryMetadata.TryExtractSyncIdFromBody("Ein ganz normaler, manuell erstellter Termin ohne Kennzeichnung.")
            .Should().BeNull();
    }

    [Fact]
    public void BodyContainsManagedByMarker_IsFalse_ForManuallyCreatedAppointments()
    {
        ManagedEntryMetadata.BodyContainsManagedByMarker("Privater Urlaub, manuell im Kalender eingetragen.")
            .Should().BeFalse();
    }

    [Fact]
    public void BodyContainsManagedByMarker_IsTrue_WhenFooterWasAppended()
    {
        var footer = ManagedEntryMetadata.BuildTechnicalFooter("loga-1", "fp-1");
        ManagedEntryMetadata.BodyContainsManagedByMarker(footer).Should().BeTrue();
    }
}
