namespace LogaOutlookSync.Domain;

/// <summary>Art der geplanten Änderung an einem Outlook-Termin.</summary>
public enum SyncOperationKind
{
    /// <summary>Kein von dieser Anwendung verwalteter Termin vorhanden; unverändert, keine Aktion.</summary>
    Unchanged,

    /// <summary>Neuer LOGA-Eintrag ohne passenden Outlook-Termin; wird angelegt.</summary>
    Create,

    /// <summary>Bestehender verwalteter Termin weicht vom LOGA-Eintrag ab; wird aktualisiert.</summary>
    Update,

    /// <summary>Verwalteter Termin ohne passenden LOGA-Eintrag mehr; wird entfernt.</summary>
    Delete,

    /// <summary>
    /// Zuordnung nicht eindeutig oder Termin wurde offensichtlich manuell verändert
    /// (z. B. Marker vorhanden, aber Fingerprint und Inhalt weichen unerwartet ab).
    /// Erfordert manuelle Prüfung, es wird keine automatische Änderung vorgenommen.
    /// </summary>
    Conflict,
}
