namespace LogaOutlookSync.Domain;

/// <summary>
/// Beschreibt einen Termin, der in einem <c>ICalendarProvider</c> angelegt oder
/// aktualisiert werden soll. Wird von der Synchronisationsplanung aus einem
/// <see cref="AbsenceEntry"/> abgeleitet.
/// </summary>
/// <param name="SyncId">Stabile, deterministische LOGA-Sync-Kennung dieses Termins.</param>
/// <param name="SourceAbsenceId">Quell-ID des zugrunde liegenden LOGA-Eintrags.</param>
/// <param name="Type">Abwesenheitsart.</param>
/// <param name="Subject">Anzuzeigender Betreff (inkl. optionalem Präfix/Suffix aus den Einstellungen).</param>
/// <param name="Category">Outlook-Kategorie, z. B. "LOGA Urlaub" oder "LOGA Gleitzeit".</param>
/// <param name="Start">Startzeitpunkt in der Windows-Zeitzone des Benutzers.</param>
/// <param name="End">
/// Endzeitpunkt. Bei Ganztagsterminen exklusiv (siehe <c>AllDayRangeCalculator</c>),
/// bei stundenweisen Terminen die tatsächliche Endzeit.
/// </param>
/// <param name="IsAllDay">Ganztägig ja/nein.</param>
/// <param name="ShowAs">
/// Anzeigen-als-Status. Für Urlaub und Gleitzeit verpflichtend <see cref="CalendarShowAs.OutOfOffice"/>.
/// </param>
/// <param name="Sensitivity">Vertraulichkeit, für Urlaub/Gleitzeit verpflichtend <see cref="CalendarSensitivity.Private"/>.</param>
/// <param name="ReminderEnabled">Erinnerung aktiv, für Urlaub/Gleitzeit verpflichtend <see langword="false"/>.</param>
/// <param name="Body">Termintext inkl. technischem Fußabschnitt (siehe <see cref="ManagedEntryMetadata"/>).</param>
/// <param name="SourceFingerprint">Fingerprint der zugrunde liegenden LOGA-Quelldaten.</param>
/// <param name="ExistingCalendarEntryId">
/// Bei Aktualisierung: providerinterne ID des bestehenden Termins. <see langword="null"/> bei Neuanlage.
/// </param>
public sealed record CalendarSyncItem(
    string SyncId,
    string SourceAbsenceId,
    AbsenceType Type,
    string Subject,
    string Category,
    DateTimeOffset Start,
    DateTimeOffset End,
    bool IsAllDay,
    CalendarShowAs ShowAs,
    CalendarSensitivity Sensitivity,
    bool ReminderEnabled,
    string Body,
    string SourceFingerprint,
    string? ExistingCalendarEntryId);
