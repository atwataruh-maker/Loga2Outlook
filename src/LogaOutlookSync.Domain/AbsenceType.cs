namespace LogaOutlookSync.Domain;

/// <summary>
/// Typisierte Abwesenheitsarten, wie sie im LOGA-Kalender vorkommen können.
/// Nicht jeder Wert wird aktuell synchronisiert; welche Typen tatsächlich mit
/// Outlook abgeglichen werden, legt <see cref="AbsenceSyncPolicy"/> fest.
/// </summary>
public enum AbsenceType
{
    /// <summary>Nicht erkannter oder nicht zugeordneter Eintragstyp.</summary>
    Unknown = 0,

    /// <summary>Urlaub.</summary>
    Vacation,

    /// <summary>Gleitzeit (ganztägig oder stundenweise).</summary>
    FlexTime,

    /// <summary>Krankheit. Aktuell nicht synchronisiert, für spätere Erweiterung vorgesehen.</summary>
    Sickness,

    /// <summary>Dienstreise. Aktuell nicht synchronisiert, für spätere Erweiterung vorgesehen.</summary>
    BusinessTrip,

    /// <summary>Fortbildung. Aktuell nicht synchronisiert, für spätere Erweiterung vorgesehen.</summary>
    Training,

    /// <summary>Homeoffice. Aktuell nicht synchronisiert, für spätere Erweiterung vorgesehen.</summary>
    HomeOffice,

    /// <summary>Rufbereitschaft. Aktuell nicht synchronisiert, für spätere Erweiterung vorgesehen.</summary>
    OnCallDuty,

    /// <summary>Zeitausgleich. Aktuell nicht synchronisiert, für spätere Erweiterung vorgesehen.</summary>
    TimeOffInLieu,
}
