using LogaOutlookSync.Domain;
using LogaOutlookSync.Infrastructure;

namespace LogaOutlookSync.Application;

/// <summary>
/// Leitet aus einem normalisierten LOGA-Abwesenheitseintrag den zu schreibenden
/// <see cref="CalendarSyncItem"/> ab, inklusive Betreff-/Kategorie-Namensgebung aus den
/// Einstellungen und der verpflichtenden "Abwesend"/Privat/Erinnerung-Aus-Kennzeichnung.
/// </summary>
public sealed class CalendarSyncItemFactory
{
    private readonly AppSettings _settings;
    private readonly TimeZoneInfo _timeZone;

    public CalendarSyncItemFactory(AppSettings settings, TimeZoneInfo? timeZone = null)
    {
        _settings = settings;
        _timeZone = timeZone ?? TimeZoneInfo.Local;
    }

    /// <summary>
    /// Erstellt das Zielobjekt für einen Termin. <paramref name="existingCalendarEntryId"/> ist
    /// bei einer Aktualisierung gesetzt, bei Neuanlage <see langword="null"/>.
    /// </summary>
    public CalendarSyncItem Create(AbsenceEntry absence, string? existingCalendarEntryId)
    {
        if (absence.Type != AbsenceType.Vacation && absence.Type != AbsenceType.FlexTime)
        {
            throw new InvalidOperationException(
                $"Abwesenheitstyp '{absence.Type}' wird aktuell nicht mit Outlook synchronisiert.");
        }

        var (start, end) = absence.IsAllDay
            ? AllDayRangeCalculator.ComputeAllDayRange(absence.StartDate, absence.EndDate, _timeZone)
            : AllDayRangeCalculator.ComputeTimedRange(absence.StartDate, absence.StartTime!.Value, absence.EndTime!.Value, _timeZone);

        var (baseSubject, category) = absence.Type == AbsenceType.Vacation
            ? (_settings.VacationSubject, _settings.VacationCategory)
            : (_settings.FlexTimeSubject, _settings.FlexTimeCategory);

        var subject = BuildSubject(baseSubject);
        var body = BuildBody(absence);

        return new CalendarSyncItem(
            SyncId: absence.SourceId,
            SourceAbsenceId: absence.SourceId,
            Type: absence.Type,
            Subject: subject,
            Category: category,
            Start: start,
            End: end,
            IsAllDay: absence.IsAllDay,
            ShowAs: CalendarShowAs.OutOfOffice,
            Sensitivity: CalendarSensitivity.Private,
            ReminderEnabled: false,
            Body: body,
            SourceFingerprint: absence.Fingerprint,
            ExistingCalendarEntryId: existingCalendarEntryId);
    }

    private string BuildSubject(string baseSubject)
    {
        var subject = baseSubject;

        if (_settings.AppendLogaSuffixToSubject)
        {
            subject = $"{subject} (LOGA)";
        }

        if (!string.IsNullOrEmpty(_settings.SubjectPrefix))
        {
            subject = $"{_settings.SubjectPrefix}{subject}";
        }

        return subject;
    }

    private static string BuildBody(AbsenceEntry absence)
    {
        var bodyText = string.IsNullOrWhiteSpace(absence.DisplayText) ? string.Empty : absence.DisplayText + Environment.NewLine;
        return bodyText + ManagedEntryMetadata.BuildTechnicalFooter(absence.SourceId, absence.Fingerprint);
    }
}
