using LogaOutlookSync.Domain;
using Microsoft.Graph.Models;

namespace LogaOutlookSync.Calendar.Graph;

/// <summary>
/// Bildet den providerunabhängigen <see cref="CalendarShowAs"/>-Status auf den Microsoft-Graph-
/// spezifischen <see cref="FreeBusyStatus"/> ab und zurück. Als eigenständige, statische Klasse
/// unabhängig von einer Graph-Verbindung testbar.
/// </summary>
public static class GraphShowAsMapper
{
    public static FreeBusyStatus ToGraph(CalendarShowAs showAs) => showAs switch
    {
        CalendarShowAs.Free => FreeBusyStatus.Free,
        CalendarShowAs.Tentative => FreeBusyStatus.Tentative,
        CalendarShowAs.Busy => FreeBusyStatus.Busy,
        CalendarShowAs.OutOfOffice => FreeBusyStatus.Oof,
        CalendarShowAs.WorkingElsewhere => FreeBusyStatus.WorkingElsewhere,
        _ => FreeBusyStatus.Unknown,
    };

    public static CalendarShowAs FromGraph(FreeBusyStatus? showAs) => showAs switch
    {
        FreeBusyStatus.Free => CalendarShowAs.Free,
        FreeBusyStatus.Tentative => CalendarShowAs.Tentative,
        FreeBusyStatus.Busy => CalendarShowAs.Busy,
        FreeBusyStatus.Oof => CalendarShowAs.OutOfOffice,
        FreeBusyStatus.WorkingElsewhere => CalendarShowAs.WorkingElsewhere,
        _ => CalendarShowAs.Busy,
    };

    public static Sensitivity ToGraphSensitivity(CalendarSensitivity sensitivity) => sensitivity switch
    {
        CalendarSensitivity.Private => Sensitivity.Private,
        _ => Sensitivity.Normal,
    };
}
