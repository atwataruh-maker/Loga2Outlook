using LogaOutlookSync.Domain;

namespace LogaOutlookSync.Loga;

/// <summary>
/// Abstraktion über die LOGA-Browserautomatisierung. Liefert ausschließlich normalisierte,
/// genehmigte Abwesenheitseinträge innerhalb des angefragten Zeitraums.
/// </summary>
public interface ILogaClient
{
    /// <summary>
    /// Meldet sich bei LOGA an, öffnet den persönlichen Kalender und liest alle genehmigten
    /// Abwesenheitseinträge, deren Zeitraum sich mit [<paramref name="from"/>, <paramref name="to"/>]
    /// überschneidet.
    /// </summary>
    Task<IReadOnlyList<AbsenceEntry>> GetAbsencesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    /// <summary>
    /// Führt ausschließlich den Login-Vorgang aus, ohne den Kalender zu lesen.
    /// Wird vom Einrichtungsassistenten für "LOGA-Anmeldung testen" verwendet.
    /// </summary>
    Task<LoginTestResult> TestLoginAsync(CancellationToken cancellationToken);
}

/// <summary>Ergebnis eines isolierten Login-Tests.</summary>
/// <param name="Succeeded">Ob die Anmeldung erfolgreich war.</param>
/// <param name="ErrorMessage">Verständliche Fehlermeldung, sofern die Anmeldung fehlgeschlagen ist.</param>
public sealed record LoginTestResult(bool Succeeded, string? ErrorMessage);
