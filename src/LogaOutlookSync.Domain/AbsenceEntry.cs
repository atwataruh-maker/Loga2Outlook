namespace LogaOutlookSync.Domain;

/// <summary>
/// Ein aus LOGA gelesener, in das unabhängige Domänenmodell normalisierter
/// Abwesenheitseintrag (Urlaub, Gleitzeit, ...).
/// </summary>
/// <param name="SourceId">
/// Stabile Quell-ID. Reihenfolge der Ermittlung: 1) eindeutige LOGA-ID aus dem HTML,
/// 2) stabile Kombination aus LOGA-Metadaten, 3) deterministischer Hash aus den
/// Quelldaten (siehe <see cref="Fingerprint"/> in der Application-Schicht).
/// </param>
/// <param name="Type">Abwesenheitsart.</param>
/// <param name="StartDate">Erster Tag der Abwesenheit (inklusive).</param>
/// <param name="EndDate">Letzter Tag der Abwesenheit (inklusive, wie in LOGA angezeigt).</param>
/// <param name="StartTime">Optionale Startzeit bei stundenweiser Gleitzeit.</param>
/// <param name="EndTime">Optionale Endzeit bei stundenweiser Gleitzeit.</param>
/// <param name="IsAllDay">
/// Ganztägig ja/nein. Wenn LOGA keine Uhrzeiten liefert, ist dieser Wert immer <see langword="true"/>.
/// </param>
/// <param name="ApprovalStatus">Genehmigungsstatus des Eintrags in LOGA.</param>
/// <param name="DisplayText">Optionaler LOGA-Anzeigetext, wie er im Portal dargestellt wird.</param>
/// <param name="Fingerprint">Deterministischer Hash der fachlich relevanten Felder, siehe <see cref="Type"/>.</param>
/// <param name="LastSeenUtc">Zeitpunkt, zu dem dieser Eintrag zuletzt in LOGA gesehen wurde.</param>
public sealed record AbsenceEntry(
    string SourceId,
    AbsenceType Type,
    DateOnly StartDate,
    DateOnly EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool IsAllDay,
    AbsenceApprovalStatus ApprovalStatus,
    string? DisplayText,
    string Fingerprint,
    DateTimeOffset LastSeenUtc)
{
    /// <summary>
    /// Prüft die interne Konsistenz des Eintrags (z. B. Start &lt;= Ende, keine Zeiten bei Ganztagsterminen).
    /// </summary>
    public void Validate()
    {
        if (EndDate < StartDate)
        {
            throw new InvalidOperationException(
                $"Ungültiger LOGA-Eintrag '{SourceId}': Enddatum {EndDate} liegt vor Startdatum {StartDate}.");
        }

        if (IsAllDay && (StartTime is not null || EndTime is not null))
        {
            throw new InvalidOperationException(
                $"Ungültiger LOGA-Eintrag '{SourceId}': Ganztägiger Eintrag darf keine Uhrzeiten enthalten.");
        }

        if (!IsAllDay && StartDate != EndDate)
        {
            throw new InvalidOperationException(
                $"Ungültiger LOGA-Eintrag '{SourceId}': Stundenweise Einträge müssen sich auf einen einzelnen Tag beziehen.");
        }

        if (!IsAllDay && (StartTime is null || EndTime is null))
        {
            throw new InvalidOperationException(
                $"Ungültiger LOGA-Eintrag '{SourceId}': Stundenweiser Eintrag benötigt Start- und Endzeit.");
        }

        if (!IsAllDay && StartTime is not null && EndTime is not null && EndTime <= StartTime)
        {
            throw new InvalidOperationException(
                $"Ungültiger LOGA-Eintrag '{SourceId}': Endzeit {EndTime} liegt nicht nach Startzeit {StartTime}.");
        }
    }
}
