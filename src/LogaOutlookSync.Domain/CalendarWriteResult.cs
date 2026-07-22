namespace LogaOutlookSync.Domain;

/// <summary>
/// Ergebnis einer Erstellungs- oder Aktualisierungsoperation an einem Kalenderprovider,
/// einschließlich der verpflichtenden Prüfung des "Abwesend"-Status (siehe Anforderung,
/// dass jeder synchronisierte Urlaubs-/Gleitzeittermin als "Abwesend" gespeichert sein muss).
/// </summary>
/// <param name="CalendarEntryId">Providerinterne ID des erstellten bzw. aktualisierten Termins.</param>
/// <param name="ShowAsVerifiedOutOfOffice">
/// <see langword="true"/>, wenn nach dem Schreiben verifiziert wurde, dass der Termin
/// tatsächlich als "Abwesend" gespeichert ist.
/// </param>
/// <param name="Warning">
/// Klartext-Warnung, falls die Verifikation fehlgeschlagen ist oder nicht durchgeführt werden konnte.
/// </param>
public sealed record CalendarWriteResult(
    string CalendarEntryId,
    bool ShowAsVerifiedOutOfOffice,
    string? Warning);
