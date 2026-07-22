namespace LogaOutlookSync.Loga.Configuration;

/// <summary>
/// Zentrale, LOGA-spezifische Selektorkonfiguration. Wird aus <c>loga-selectors.json</c>
/// geladen, damit LOGA-spezifische Details nicht über den Quellcode verteilt werden müssen.
/// Die tatsächlichen Werte sind aktuell noch nicht bekannt (siehe
/// <c>config/loga-selectors.example.json</c>) und müssen nach einer Untersuchung der
/// echten LOGA-Seite eingetragen werden.
/// </summary>
public sealed class LogaSelectorsConfig
{
    public LoginSelectors Login { get; set; } = new();

    public CalendarParsingSelectors CalendarParsing { get; set; } = new();
}

/// <summary>Selektoren für den Login-Vorgang. Ausschließlich für Playwright (CSS/ARIA-Rolle/Text) bestimmt.</summary>
public sealed class LoginSelectors
{
    /// <summary>Selektor des Benutzername-Eingabefelds.</summary>
    public string UsernameField { get; set; } = string.Empty;

    /// <summary>Selektor des Passwort-Eingabefelds.</summary>
    public string PasswordField { get; set; } = string.Empty;

    /// <summary>Selektor der Anmelden-Schaltfläche.</summary>
    public string SubmitButton { get; set; } = string.Empty;

    /// <summary>Optionaler Selektor einer Fehlermeldung nach fehlgeschlagenem Login.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Optionaler Selektor, der auf eine nachträglich aktivierte MFA-Abfrage hindeutet.</summary>
    public string? MfaChallengeIndicator { get; set; }

    /// <summary>Optionaler Selektor, der nach erfolgreichem Login sicher sichtbar ist (z. B. Benutzermenü).</summary>
    public string? LoggedInIndicator { get; set; }
}

/// <summary>
/// Selektoren zur Auswertung des vom Browser eingesammelten HTML-Snapshots des persönlichen
/// Kalenders. Werden mit CSS-Selektoren gegen den mit AngleSharp geparsten HTML-Baum angewendet.
/// </summary>
public sealed class CalendarParsingSelectors
{
    /// <summary>Selektor, der jeden einzelnen Kalendereintrag (ein Container-Element) auswählt.</summary>
    public string EntryContainer { get; set; } = string.Empty;

    /// <summary>
    /// Optionales HTML-Attribut am Eintrags-Container, das eine eindeutige LOGA-ID enthält
    /// (bevorzugte Kennungsquelle). Wenn nicht vorhanden, wird ein deterministischer Hash gebildet.
    /// </summary>
    public string? SourceIdAttribute { get; set; }

    /// <summary>Selektor (relativ zum Eintrags-Container) für den Datumsbereichstext.</summary>
    public string DateRange { get; set; } = string.Empty;

    /// <summary>Optionaler Selektor (relativ) für einen Uhrzeitbereichstext bei stundenweiser Gleitzeit.</summary>
    public string? TimeRange { get; set; }

    /// <summary>Selektor (relativ) für den Text, aus dem die Abwesenheitsart abgeleitet wird.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Optionaler Selektor (relativ) für den Genehmigungsstatus-Text.</summary>
    public string? Status { get; set; }

    /// <summary>Optionaler Selektor (relativ) für einen Anzeigetext. Fallback: gesamter Text des Containers.</summary>
    public string? DisplayText { get; set; }

    /// <summary>.NET-Datumsformat zum Parsen einzelner Datumsangaben, z. B. "dd.MM.yyyy".</summary>
    public string DateFormat { get; set; } = "dd.MM.yyyy";

    /// <summary>.NET-Zeitformat zum Parsen einzelner Uhrzeitangaben, z. B. "HH:mm".</summary>
    public string TimeFormat { get; set; } = "HH:mm";

    /// <summary>Trennzeichen zwischen Start- und Endwert innerhalb eines Datums- bzw. Uhrzeitbereichstexts.</summary>
    public string RangeSeparator { get; set; } = " - ";

    /// <summary>Kulturname zum Parsen von Datum/Uhrzeit, z. B. "de-DE".</summary>
    public string CultureName { get; set; } = "de-DE";

    /// <summary>
    /// Ordnet Teiltexte (Schlüssel, case-insensitive "enthält") des Typ-Textes einer
    /// <c>AbsenceType</c> (Wert, als Enum-Name) zu, z. B. <c>{"Urlaub": "Vacation"}</c>.
    /// </summary>
    public Dictionary<string, string> TypeTextMapping { get; set; } = new();

    /// <summary>
    /// Ordnet Teiltexte (Schlüssel, case-insensitive "enthält") des Status-Textes einer
    /// <c>AbsenceApprovalStatus</c> (Wert, als Enum-Name) zu, z. B. <c>{"genehmigt": "Approved"}</c>.
    /// Ist <see cref="Status"/> nicht konfiguriert, werden alle Einträge als genehmigt behandelt,
    /// da viele Portale im persönlichen Kalender ausschließlich bestätigte Abwesenheiten anzeigen.
    /// </summary>
    public Dictionary<string, string> StatusTextMapping { get; set; } = new();
}
