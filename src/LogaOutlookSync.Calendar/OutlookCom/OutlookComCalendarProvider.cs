using System.Globalization;
using System.Runtime.InteropServices;
using LogaOutlookSync.Calendar.Exceptions;
using LogaOutlookSync.Domain;
using LogaOutlookSync.Infrastructure;
using Microsoft.Extensions.Logging;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace LogaOutlookSync.Calendar.OutlookCom;

/// <summary>
/// Fallback-<see cref="ICalendarProvider"/>-Implementierung über Outlook-COM-Interop für
/// Outlook Classic (Desktop). Wird verwendet, wenn Microsoft Graph aufgrund fehlender
/// Berechtigungen oder einer nicht möglichen Entra-ID-App-Registrierung nicht eingesetzt
/// werden kann. Jeder Aufruf läuft auf einem dedizierten STA-Thread (siehe <see cref="StaThread"/>)
/// und gibt alle COM-Objekte zuverlässig wieder frei.
/// </summary>
public sealed class OutlookComCalendarProvider : ICalendarProvider
{
    private readonly AppSettings _settings;
    private readonly ILogger<OutlookComCalendarProvider> _logger;

    public OutlookComCalendarProvider(AppSettings settings, ILogger<OutlookComCalendarProvider> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public Task<IReadOnlyList<ManagedCalendarEntry>> GetManagedEntriesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StaThread.RunAsync(() => GetManagedEntriesCore(from, to));
    }

    public Task<CalendarWriteResult> CreateAsync(CalendarSyncItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StaThread.RunAsync(() => CreateCore(item));
    }

    public Task<CalendarWriteResult> UpdateAsync(CalendarSyncItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StaThread.RunAsync(() => UpdateCore(item));
    }

    public Task DeleteAsync(string calendarEntryId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StaThread.RunAsync(() =>
        {
            DeleteCore(calendarEntryId);
            return true;
        });
    }

    private IReadOnlyList<ManagedCalendarEntry> GetManagedEntriesCore(DateTimeOffset from, DateTimeOffset to)
    {
        var (app, ns) = GetOrCreateApplication();
        try
        {
            var folder = FindCalendarFolder(ns, _settings.TargetCalendarId);
            try
            {
                var items = folder.Items;
                items.IncludeRecurrences = false;
                items.Sort("[Start]", Type: false);

                var filter = string.Format(
                    CultureInfo.InvariantCulture,
                    "[Start] <= '{0:g}' AND [End] >= '{1:g}'",
                    to.LocalDateTime,
                    from.LocalDateTime);

                var restricted = items.Restrict(filter);
                var result = new List<ManagedCalendarEntry>();

                foreach (var rawItem in restricted)
                {
                    if (rawItem is not Outlook.AppointmentItem appointment)
                    {
                        continue;
                    }

                    try
                    {
                        result.Add(MapToManagedEntry(appointment));
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(appointment);
                    }
                }

                Marshal.ReleaseComObject(restricted);
                return result;
            }
            finally
            {
                Marshal.ReleaseComObject(folder.Items);
                Marshal.ReleaseComObject(folder);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(ns);
            Marshal.ReleaseComObject(app);
        }
    }

    private CalendarWriteResult CreateCore(CalendarSyncItem item)
    {
        var (app, ns) = GetOrCreateApplication();
        try
        {
            var folder = FindCalendarFolder(ns, _settings.TargetCalendarId);
            try
            {
                var appointment = (Outlook.AppointmentItem)folder.Items.Add(Outlook.OlItemType.olAppointmentItem);
                try
                {
                    ApplySyncItemToAppointment(appointment, item);
                    appointment.Save();

                    var entryId = appointment.EntryID;
                    var verified = appointment.BusyStatus == Outlook.OlBusyStatus.olOutOfOffice;

                    _logger.LogInformation("Termin '{Subject}' in Outlook (COM) angelegt (SyncId {SyncId}).", item.Subject, item.SyncId);

                    return new CalendarWriteResult(
                        entryId,
                        verified,
                        verified ? null : "Der Termin wurde gespeichert, aber der verpflichtende Status \"Abwesend\" konnte nicht bestätigt werden.");
                }
                finally
                {
                    Marshal.ReleaseComObject(appointment);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(folder.Items);
                Marshal.ReleaseComObject(folder);
            }
        }
        catch (COMException ex)
        {
            throw new OutlookComException($"Termin konnte in Outlook nicht angelegt werden: {ex.Message}", ex);
        }
        finally
        {
            Marshal.ReleaseComObject(ns);
            Marshal.ReleaseComObject(app);
        }
    }

    private CalendarWriteResult UpdateCore(CalendarSyncItem item)
    {
        if (string.IsNullOrEmpty(item.ExistingCalendarEntryId))
        {
            throw new InvalidOperationException(
                $"Für die Aktualisierung des Termins mit SyncId '{item.SyncId}' fehlt die vorhandene Termin-ID.");
        }

        var (app, ns) = GetOrCreateApplication();
        try
        {
            var appointment = GetAppointmentByEntryId(ns, item.ExistingCalendarEntryId);
            try
            {
                if (!IsManagedByApp(appointment))
                {
                    throw new OutlookComException(
                        $"Der Termin '{item.ExistingCalendarEntryId}' trägt keine gültige Kennzeichnung dieser " +
                        "Anwendung und wird daher nicht automatisch verändert, um manuell erstellte Termine zu schützen.");
                }

                ApplySyncItemToAppointment(appointment, item);
                appointment.Save();

                var verified = appointment.BusyStatus == Outlook.OlBusyStatus.olOutOfOffice;
                _logger.LogInformation("Termin '{Subject}' in Outlook (COM) aktualisiert (SyncId {SyncId}).", item.Subject, item.SyncId);

                return new CalendarWriteResult(
                    item.ExistingCalendarEntryId,
                    verified,
                    verified ? null : "Der Termin wurde aktualisiert, aber der verpflichtende Status \"Abwesend\" konnte nicht bestätigt werden.");
            }
            finally
            {
                Marshal.ReleaseComObject(appointment);
            }
        }
        catch (COMException ex)
        {
            throw new OutlookComException($"Termin '{item.ExistingCalendarEntryId}' konnte in Outlook nicht aktualisiert werden: {ex.Message}", ex);
        }
        finally
        {
            Marshal.ReleaseComObject(ns);
            Marshal.ReleaseComObject(app);
        }
    }

    private void DeleteCore(string calendarEntryId)
    {
        var (app, ns) = GetOrCreateApplication();
        try
        {
            var appointment = GetAppointmentByEntryId(ns, calendarEntryId);
            try
            {
                if (!IsManagedByApp(appointment))
                {
                    throw new OutlookComException(
                        $"Der Termin '{calendarEntryId}' trägt keine gültige Kennzeichnung dieser Anwendung und " +
                        "wird daher nicht automatisch gelöscht, um manuell erstellte Termine zu schützen.");
                }

                appointment.Delete();
                _logger.LogInformation("Termin '{EntryId}' in Outlook (COM) gelöscht.", calendarEntryId);
            }
            finally
            {
                Marshal.ReleaseComObject(appointment);
            }
        }
        catch (COMException ex)
        {
            throw new OutlookComException($"Termin '{calendarEntryId}' konnte in Outlook nicht gelöscht werden: {ex.Message}", ex);
        }
        finally
        {
            Marshal.ReleaseComObject(ns);
            Marshal.ReleaseComObject(app);
        }
    }

    private static void ApplySyncItemToAppointment(Outlook.AppointmentItem appointment, CalendarSyncItem item)
    {
        appointment.Subject = item.Subject;
        appointment.Body = item.Body;
        appointment.AllDayEvent = item.IsAllDay;
        appointment.Start = item.Start.LocalDateTime;
        appointment.End = item.End.LocalDateTime;
        appointment.BusyStatus = MapBusyStatus(item.ShowAs);
        appointment.Sensitivity = MapSensitivity(item.Sensitivity);
        appointment.ReminderSet = item.ReminderEnabled;
        appointment.Categories = item.Category;

        SetUserProperty(appointment, ManagedEntryMetadata.SyncIdPropertyName, item.SyncId);
        SetUserProperty(appointment, ManagedEntryMetadata.ManagedByPropertyName, ManagedEntryMetadata.ManagedByValue);
        SetUserProperty(appointment, ManagedEntryMetadata.FingerprintPropertyName, item.SourceFingerprint);
    }

    private ManagedCalendarEntry MapToManagedEntry(Outlook.AppointmentItem appointment)
    {
        var syncId = GetUserProperty(appointment, ManagedEntryMetadata.SyncIdPropertyName);
        var managedByValue = GetUserProperty(appointment, ManagedEntryMetadata.ManagedByPropertyName);
        var fingerprint = GetUserProperty(appointment, ManagedEntryMetadata.FingerprintPropertyName);

        var isManaged = string.Equals(managedByValue, ManagedEntryMetadata.ManagedByValue, StringComparison.Ordinal)
            && !string.IsNullOrEmpty(syncId);

        if (!isManaged && ManagedEntryMetadata.BodyContainsManagedByMarker(appointment.Body))
        {
            syncId ??= ManagedEntryMetadata.TryExtractSyncIdFromBody(appointment.Body);
            isManaged = !string.IsNullOrEmpty(syncId);
        }

        var localZone = TimeZoneInfo.Local;

        return new ManagedCalendarEntry(
            CalendarEntryId: appointment.EntryID,
            SyncId: syncId,
            IsManagedByApp: isManaged,
            Subject: appointment.Subject ?? string.Empty,
            Start: new DateTimeOffset(appointment.Start, localZone.GetUtcOffset(appointment.Start)),
            End: new DateTimeOffset(appointment.End, localZone.GetUtcOffset(appointment.End)),
            IsAllDay: appointment.AllDayEvent,
            Category: appointment.Categories,
            ShowAs: MapBusyStatusFromOutlook(appointment.BusyStatus),
            SourceFingerprint: fingerprint,
            LastModifiedUtc: appointment.LastModificationTime == default
                ? null
                : new DateTimeOffset(appointment.LastModificationTime, localZone.GetUtcOffset(appointment.LastModificationTime)));
    }

    private static bool IsManagedByApp(Outlook.AppointmentItem appointment)
    {
        var managedByValue = GetUserProperty(appointment, ManagedEntryMetadata.ManagedByPropertyName);
        var syncId = GetUserProperty(appointment, ManagedEntryMetadata.SyncIdPropertyName);

        if (string.Equals(managedByValue, ManagedEntryMetadata.ManagedByValue, StringComparison.Ordinal) && !string.IsNullOrEmpty(syncId))
        {
            return true;
        }

        return ManagedEntryMetadata.BodyContainsManagedByMarker(appointment.Body);
    }

    private static string? GetUserProperty(Outlook.AppointmentItem appointment, string name)
    {
        var property = appointment.UserProperties.Find(name, true);
        try
        {
            return property?.Value as string;
        }
        finally
        {
            if (property is not null)
            {
                Marshal.ReleaseComObject(property);
            }
        }
    }

    private static void SetUserProperty(Outlook.AppointmentItem appointment, string name, string value)
    {
        var property = appointment.UserProperties.Find(name, true)
            ?? appointment.UserProperties.Add(name, Outlook.OlUserPropertyType.olText, true);
        try
        {
            property.Value = value;
        }
        finally
        {
            Marshal.ReleaseComObject(property);
        }
    }

    private static Outlook.AppointmentItem GetAppointmentByEntryId(Outlook.NameSpace ns, string entryId)
    {
        var item = ns.GetItemFromID(entryId) as Outlook.AppointmentItem;
        return item ?? throw new OutlookComException($"Termin mit der ID '{entryId}' wurde in Outlook nicht gefunden.");
    }

    private static Outlook.MAPIFolder FindCalendarFolder(Outlook.NameSpace ns, string? folderName)
    {
        var defaultCalendar = ns.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderCalendar);

        if (string.IsNullOrWhiteSpace(folderName) || string.Equals(defaultCalendar.Name, folderName, StringComparison.OrdinalIgnoreCase))
        {
            return defaultCalendar;
        }

        foreach (var rawSubFolder in defaultCalendar.Folders)
        {
            if (rawSubFolder is Outlook.MAPIFolder subFolder)
            {
                if (string.Equals(subFolder.Name, folderName, StringComparison.OrdinalIgnoreCase))
                {
                    Marshal.ReleaseComObject(defaultCalendar);
                    return subFolder;
                }

                Marshal.ReleaseComObject(subFolder);
            }
        }

        throw new OutlookComException(
            $"Der konfigurierte Outlook-Kalenderordner '{folderName}' wurde nicht gefunden. " +
            "Bitte prüfen Sie den Namen in den Einstellungen oder wählen Sie den Standardkalender.");
    }

    private static (Outlook.Application Application, Outlook.NameSpace Namespace) GetOrCreateApplication()
    {
        Outlook.Application application;
        try
        {
            application = (Outlook.Application)Marshal.GetActiveObject("Outlook.Application");
        }
        catch (COMException)
        {
            try
            {
                application = new Outlook.Application();
            }
            catch (COMException ex)
            {
                throw new OutlookComException(
                    "Outlook konnte nicht gestartet werden. Bitte stellen Sie sicher, dass Outlook Classic (Desktop) " +
                    "installiert und mit einem Profil eingerichtet ist.",
                    ex);
            }
        }

        try
        {
            var ns = application.GetNamespace("MAPI");
            return (application, ns);
        }
        catch (COMException ex)
        {
            Marshal.ReleaseComObject(application);
            throw new OutlookComException("Auf das Outlook-Profil (MAPI) konnte nicht zugegriffen werden.", ex);
        }
    }

    private static Outlook.OlBusyStatus MapBusyStatus(CalendarShowAs showAs) => showAs switch
    {
        CalendarShowAs.Free => Outlook.OlBusyStatus.olFree,
        CalendarShowAs.Tentative => Outlook.OlBusyStatus.olTentative,
        CalendarShowAs.Busy => Outlook.OlBusyStatus.olBusy,
        CalendarShowAs.OutOfOffice => Outlook.OlBusyStatus.olOutOfOffice,
        CalendarShowAs.WorkingElsewhere => Outlook.OlBusyStatus.olWorkingElsewhere,
        _ => Outlook.OlBusyStatus.olBusy,
    };

    private static CalendarShowAs MapBusyStatusFromOutlook(Outlook.OlBusyStatus status) => status switch
    {
        Outlook.OlBusyStatus.olFree => CalendarShowAs.Free,
        Outlook.OlBusyStatus.olTentative => CalendarShowAs.Tentative,
        Outlook.OlBusyStatus.olBusy => CalendarShowAs.Busy,
        Outlook.OlBusyStatus.olOutOfOffice => CalendarShowAs.OutOfOffice,
        Outlook.OlBusyStatus.olWorkingElsewhere => CalendarShowAs.WorkingElsewhere,
        _ => CalendarShowAs.Busy,
    };

    private static Outlook.OlSensitivity MapSensitivity(CalendarSensitivity sensitivity) => sensitivity switch
    {
        CalendarSensitivity.Private => Outlook.OlSensitivity.olPrivate,
        _ => Outlook.OlSensitivity.olNormal,
    };
}
