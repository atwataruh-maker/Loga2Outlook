using LogaOutlookSync.Domain;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace LogaOutlookSync.Calendar.OutlookCom;

/// <summary>
/// Bildet den providerunabhängigen <see cref="CalendarShowAs"/>-Status auf die Outlook-COM-
/// spezifische <see cref="Outlook.OlBusyStatus"/>-Aufzählung ab und zurück. Als eigenständige,
/// statische Klasse unabhängig von einer laufenden Outlook-Instanz testbar.
/// </summary>
public static class OutlookBusyStatusMapper
{
    public static Outlook.OlBusyStatus ToOutlook(CalendarShowAs showAs) => showAs switch
    {
        CalendarShowAs.Free => Outlook.OlBusyStatus.olFree,
        CalendarShowAs.Tentative => Outlook.OlBusyStatus.olTentative,
        CalendarShowAs.Busy => Outlook.OlBusyStatus.olBusy,
        CalendarShowAs.OutOfOffice => Outlook.OlBusyStatus.olOutOfOffice,
        CalendarShowAs.WorkingElsewhere => Outlook.OlBusyStatus.olWorkingElsewhere,
        _ => Outlook.OlBusyStatus.olBusy,
    };

    public static CalendarShowAs FromOutlook(Outlook.OlBusyStatus status) => status switch
    {
        Outlook.OlBusyStatus.olFree => CalendarShowAs.Free,
        Outlook.OlBusyStatus.olTentative => CalendarShowAs.Tentative,
        Outlook.OlBusyStatus.olBusy => CalendarShowAs.Busy,
        Outlook.OlBusyStatus.olOutOfOffice => CalendarShowAs.OutOfOffice,
        Outlook.OlBusyStatus.olWorkingElsewhere => CalendarShowAs.WorkingElsewhere,
        _ => CalendarShowAs.Busy,
    };

    public static Outlook.OlSensitivity ToOutlookSensitivity(CalendarSensitivity sensitivity) => sensitivity switch
    {
        CalendarSensitivity.Private => Outlook.OlSensitivity.olPrivate,
        _ => Outlook.OlSensitivity.olNormal,
    };
}
