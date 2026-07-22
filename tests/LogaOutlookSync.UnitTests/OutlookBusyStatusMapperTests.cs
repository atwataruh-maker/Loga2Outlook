using FluentAssertions;
using LogaOutlookSync.Calendar.OutlookCom;
using LogaOutlookSync.Domain;
using Outlook = Microsoft.Office.Interop.Outlook;
using Xunit;

namespace LogaOutlookSync.UnitTests;

public sealed class OutlookBusyStatusMapperTests
{
    [Fact]
    public void ToOutlook_MapsOutOfOffice_ToOlOutOfOffice()
    {
        OutlookBusyStatusMapper.ToOutlook(CalendarShowAs.OutOfOffice).Should().Be(Outlook.OlBusyStatus.olOutOfOffice);
    }

    [Fact]
    public void FromOutlook_MapsOlOutOfOffice_ToOutOfOffice()
    {
        OutlookBusyStatusMapper.FromOutlook(Outlook.OlBusyStatus.olOutOfOffice).Should().Be(CalendarShowAs.OutOfOffice);
    }

    [Theory]
    [InlineData(CalendarShowAs.Free, Outlook.OlBusyStatus.olFree)]
    [InlineData(CalendarShowAs.Tentative, Outlook.OlBusyStatus.olTentative)]
    [InlineData(CalendarShowAs.Busy, Outlook.OlBusyStatus.olBusy)]
    [InlineData(CalendarShowAs.OutOfOffice, Outlook.OlBusyStatus.olOutOfOffice)]
    [InlineData(CalendarShowAs.WorkingElsewhere, Outlook.OlBusyStatus.olWorkingElsewhere)]
    public void ToOutlook_RoundTripsThroughFromOutlook(CalendarShowAs domainValue, Outlook.OlBusyStatus expectedOutlookValue)
    {
        var outlookValue = OutlookBusyStatusMapper.ToOutlook(domainValue);
        outlookValue.Should().Be(expectedOutlookValue);
        OutlookBusyStatusMapper.FromOutlook(outlookValue).Should().Be(domainValue);
    }

    [Fact]
    public void ToOutlookSensitivity_MapsPrivate_ToOlPrivate()
    {
        OutlookBusyStatusMapper.ToOutlookSensitivity(CalendarSensitivity.Private).Should().Be(Outlook.OlSensitivity.olPrivate);
    }
}
