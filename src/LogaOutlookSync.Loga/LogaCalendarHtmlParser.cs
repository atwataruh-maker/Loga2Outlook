using System.Globalization;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using LogaOutlookSync.Domain;
using LogaOutlookSync.Loga.Configuration;
using LogaOutlookSync.Loga.Exceptions;

namespace LogaOutlookSync.Loga;

/// <summary>
/// Wertet einen HTML-Snapshot des persönlichen LOGA-Kalenders aus und normalisiert die
/// gefundenen Einträge in das unabhängige Domänenmodell. Bewusst getrennt von der
/// Browsersteuerung (<see cref="PlaywrightLogaClient"/>), damit dieser Parser mit
/// gespeicherten, anonymisierten HTML-Testdateien unit-getestet werden kann, ohne einen
/// Browser zu starten.
/// </summary>
public static class LogaCalendarHtmlParser
{
    /// <summary>
    /// Parst alle erkennbaren Abwesenheitseinträge aus <paramref name="html"/>. Gibt Einträge
    /// unabhängig von ihrem Genehmigungsstatus zurück (inkl. z. B. "Beantragt" oder "Storniert"),
    /// damit sowohl die Erkennung als auch die Statusauswertung isoliert getestet werden können.
    /// Die Filterung auf tatsächlich genehmigte Einträge erfolgt beim Aufrufer.
    /// </summary>
    public static IReadOnlyList<AbsenceEntry> Parse(string html, CalendarParsingSelectors config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (string.IsNullOrWhiteSpace(config.EntryContainer))
        {
            throw new LogaConfigurationException("Kein EntryContainer-Selektor für die Kalenderauswertung konfiguriert.");
        }

        var parser = new HtmlParser();
        var document = parser.ParseDocument(html ?? string.Empty);

        var culture = CultureInfo.GetCultureInfo(config.CultureName);
        var entryNodes = document.QuerySelectorAll(config.EntryContainer);

        var entries = new List<AbsenceEntry>(entryNodes.Length);
        var now = DateTimeOffset.UtcNow;

        foreach (var node in entryNodes)
        {
            var entry = TryParseEntry(node, config, culture, now);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    private static AbsenceEntry? TryParseEntry(IElement node, CalendarParsingSelectors config, CultureInfo culture, DateTimeOffset now)
    {
        var dateRangeText = SelectText(node, config.DateRange);
        if (string.IsNullOrWhiteSpace(dateRangeText))
        {
            throw new LogaParsingException(
                "Ein Kalendereintrag enthält keinen auswertbaren Datumsbereichstext. " +
                "Die LOGA-Seitenstruktur hat sich möglicherweise geändert.",
                failedStep: "Kalenderauswertung: Datumsbereich",
                selector: config.DateRange);
        }

        if (!TryParseDateRange(dateRangeText, config, culture, out var startDate, out var endDate))
        {
            throw new LogaParsingException(
                $"Der Datumsbereichstext '{dateRangeText}' konnte nicht mit dem konfigurierten Format " +
                $"'{config.DateFormat}' geparst werden.",
                failedStep: "Kalenderauswertung: Datumsbereich",
                selector: config.DateRange);
        }

        var typeText = SelectText(node, config.Type) ?? string.Empty;
        var type = MapText(typeText, config.TypeTextMapping, AbsenceType.Unknown);

        var statusText = string.IsNullOrEmpty(config.Status) ? null : SelectText(node, config.Status);
        var status = string.IsNullOrEmpty(config.Status)
            ? AbsenceApprovalStatus.Approved
            : MapText(statusText ?? string.Empty, config.StatusTextMapping, AbsenceApprovalStatus.Unknown);

        var displayText = string.IsNullOrEmpty(config.DisplayText)
            ? node.TextContent.Trim()
            : SelectText(node, config.DisplayText) ?? node.TextContent.Trim();

        TimeOnly? startTime = null;
        TimeOnly? endTime = null;
        var isAllDay = true;

        if (!string.IsNullOrEmpty(config.TimeRange))
        {
            var timeRangeText = SelectText(node, config.TimeRange);
            if (!string.IsNullOrWhiteSpace(timeRangeText) && TryParseTimeRange(timeRangeText, config, culture, out var parsedStart, out var parsedEnd))
            {
                startTime = parsedStart;
                endTime = parsedEnd;
                isAllDay = false;
            }
        }

        var sourceId = ResolveSourceId(node, config, type, startDate, endDate, startTime, endTime);
        var fingerprint = AbsenceFingerprint.Compute(type, startDate, endDate, startTime, endTime, status, displayText);

        var entry = new AbsenceEntry(
            sourceId,
            type,
            startDate,
            endDate,
            startTime,
            endTime,
            isAllDay,
            status,
            displayText,
            fingerprint,
            now);

        entry.Validate();
        return entry;
    }

    private static string ResolveSourceId(
        IElement node,
        CalendarParsingSelectors config,
        AbsenceType type,
        DateOnly startDate,
        DateOnly endDate,
        TimeOnly? startTime,
        TimeOnly? endTime)
    {
        if (!string.IsNullOrEmpty(config.SourceIdAttribute))
        {
            var attributeValue = node.GetAttribute(config.SourceIdAttribute);
            if (!string.IsNullOrWhiteSpace(attributeValue))
            {
                return attributeValue.Trim();
            }
        }

        return AbsenceFingerprint.ComputeFallbackSourceId(type, startDate, endDate, startTime, endTime);
    }

    private static string? SelectText(IElement node, string? selector)
    {
        if (string.IsNullOrEmpty(selector))
        {
            return null;
        }

        var selected = node.QuerySelector(selector);
        return selected?.TextContent.Trim();
    }

    private static bool TryParseDateRange(
        string rangeText,
        CalendarParsingSelectors config,
        CultureInfo culture,
        out DateOnly startDate,
        out DateOnly endDate)
    {
        var parts = SplitRange(rangeText, config.RangeSeparator);

        if (parts.Length == 1)
        {
            if (DateOnly.TryParseExact(parts[0], config.DateFormat, culture, DateTimeStyles.None, out var singleDate))
            {
                startDate = singleDate;
                endDate = singleDate;
                return true;
            }
        }
        else if (parts.Length == 2
            && DateOnly.TryParseExact(parts[0], config.DateFormat, culture, DateTimeStyles.None, out var start)
            && DateOnly.TryParseExact(parts[1], config.DateFormat, culture, DateTimeStyles.None, out var end))
        {
            startDate = start;
            endDate = end;
            return true;
        }

        startDate = default;
        endDate = default;
        return false;
    }

    private static bool TryParseTimeRange(
        string rangeText,
        CalendarParsingSelectors config,
        CultureInfo culture,
        out TimeOnly startTime,
        out TimeOnly endTime)
    {
        var parts = SplitRange(rangeText, config.RangeSeparator);

        if (parts.Length == 2
            && TimeOnly.TryParseExact(parts[0], config.TimeFormat, culture, DateTimeStyles.None, out var start)
            && TimeOnly.TryParseExact(parts[1], config.TimeFormat, culture, DateTimeStyles.None, out var end))
        {
            startTime = start;
            endTime = end;
            return true;
        }

        startTime = default;
        endTime = default;
        return false;
    }

    private static string[] SplitRange(string text, string separator)
    {
        return text
            .Split(separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static TEnum MapText<TEnum>(string text, Dictionary<string, string> mapping, TEnum fallback)
        where TEnum : struct, Enum
    {
        foreach (var (key, value) in mapping)
        {
            if (text.Contains(key, StringComparison.OrdinalIgnoreCase) && Enum.TryParse<TEnum>(value, out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }
}
