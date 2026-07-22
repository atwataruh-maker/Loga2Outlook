using System.Globalization;

namespace LogaOutlookSync.Domain;

/// <summary>
/// Berechnet den fachlichen Fingerprint eines LOGA-Eintrags aus seinen relevanten Feldern.
/// Zwei Aufrufe mit denselben fachlichen Werten liefern immer denselben Fingerprint, sodass
/// die Synchronisationsplanung Änderungen erkennen kann, ohne jedes Feld einzeln zu vergleichen.
/// </summary>
public static class AbsenceFingerprint
{
    public static string Compute(
        AbsenceType type,
        DateOnly startDate,
        DateOnly endDate,
        TimeOnly? startTime,
        TimeOnly? endTime,
        AbsenceApprovalStatus approvalStatus,
        string? displayText)
    {
        var input = string.Join(
            '|',
            type.ToString(),
            startDate.ToString("O", CultureInfo.InvariantCulture),
            endDate.ToString("O", CultureInfo.InvariantCulture),
            startTime?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            endTime?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            approvalStatus.ToString(),
            displayText ?? string.Empty);

        return DeterministicHash.Compute(input);
    }

    /// <summary>
    /// Berechnet einen stabilen Ersatz-Identifikator, wenn LOGA keine native Eintrags-ID
    /// im HTML liefert. Bewusst auf einer schmaleren Feldmenge als <see cref="Compute"/>
    /// basierend, damit die Identität auch dann stabil bleibt, wenn sich nur der
    /// Anzeigetext ändert.
    /// </summary>
    public static string ComputeFallbackSourceId(
        AbsenceType type,
        DateOnly startDate,
        DateOnly endDate,
        TimeOnly? startTime,
        TimeOnly? endTime)
    {
        var input = string.Join(
            '|',
            type.ToString(),
            startDate.ToString("O", CultureInfo.InvariantCulture),
            endDate.ToString("O", CultureInfo.InvariantCulture),
            startTime?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            endTime?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);

        return "loga-fallback-" + DeterministicHash.Compute(input);
    }
}
