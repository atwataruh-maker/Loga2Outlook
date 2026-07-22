namespace LogaOutlookSync.Domain;

/// <summary>
/// Genehmigungsstatus eines LOGA-Abwesenheitseintrags.
/// Nur <see cref="Approved"/> Einträge werden mit Outlook synchronisiert.
/// </summary>
public enum AbsenceApprovalStatus
{
    /// <summary>Status konnte nicht eindeutig aus LOGA ermittelt werden.</summary>
    Unknown = 0,

    /// <summary>Eintrag ist genehmigt.</summary>
    Approved,

    /// <summary>Eintrag wurde beantragt, aber noch nicht genehmigt.</summary>
    Pending,

    /// <summary>Eintrag wurde abgelehnt.</summary>
    Rejected,

    /// <summary>Eintrag wurde storniert bzw. zurückgezogen.</summary>
    Cancelled,
}
