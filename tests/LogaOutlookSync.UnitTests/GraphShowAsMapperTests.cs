using FluentAssertions;
using LogaOutlookSync.Calendar.Graph;
using LogaOutlookSync.Domain;
using Microsoft.Graph.Models;
using Xunit;

namespace LogaOutlookSync.UnitTests;

public sealed class GraphShowAsMapperTests
{
    [Fact]
    public void ToGraph_MapsOutOfOffice_ToOof()
    {
        GraphShowAsMapper.ToGraph(CalendarShowAs.OutOfOffice).Should().Be(FreeBusyStatus.Oof);
    }

    [Fact]
    public void FromGraph_MapsOof_ToOutOfOffice()
    {
        GraphShowAsMapper.FromGraph(FreeBusyStatus.Oof).Should().Be(CalendarShowAs.OutOfOffice);
    }

    [Theory]
    [InlineData(CalendarShowAs.Free, FreeBusyStatus.Free)]
    [InlineData(CalendarShowAs.Tentative, FreeBusyStatus.Tentative)]
    [InlineData(CalendarShowAs.Busy, FreeBusyStatus.Busy)]
    [InlineData(CalendarShowAs.OutOfOffice, FreeBusyStatus.Oof)]
    [InlineData(CalendarShowAs.WorkingElsewhere, FreeBusyStatus.WorkingElsewhere)]
    public void ToGraph_RoundTripsThroughFromGraph(CalendarShowAs domainValue, FreeBusyStatus expectedGraphValue)
    {
        var graphValue = GraphShowAsMapper.ToGraph(domainValue);
        graphValue.Should().Be(expectedGraphValue);
        GraphShowAsMapper.FromGraph(graphValue).Should().Be(domainValue);
    }

    [Fact]
    public void ToGraphSensitivity_MapsPrivate_ToGraphPrivate()
    {
        GraphShowAsMapper.ToGraphSensitivity(CalendarSensitivity.Private).Should().Be(Sensitivity.Private);
    }
}
