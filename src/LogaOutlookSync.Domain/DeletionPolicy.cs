namespace LogaOutlookSync.Domain;

/// <summary>
/// Verhalten, wenn ein zuvor synchronisierter Outlook-Termin in LOGA nicht mehr
/// vorhanden oder storniert ist.
/// </summary>
public enum DeletionPolicy
{
    /// <summary>Verwaltete Termine ohne Rückfrage automatisch löschen.</summary>
    AutoDelete,

    /// <summary>Vor dem Löschen die Bestätigung des Benutzers einholen (Standard).</summary>
    ConfirmBeforeDelete,

    /// <summary>Nicht löschen, nur als Konflikt in der Zusammenfassung melden.</summary>
    ReportOnly,
}
