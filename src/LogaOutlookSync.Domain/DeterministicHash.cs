using System.Security.Cryptography;
using System.Text;

namespace LogaOutlookSync.Domain;

/// <summary>
/// Erzeugt kurze, deterministische, stabile Hash-Werte aus fachlichen Quelldaten.
/// Wird verwendet, um LOGA-Einträge ohne native ID stabil zu identifizieren
/// (siehe <see cref="AbsenceFingerprint"/>) und um Inhaltsänderungen zu erkennen.
/// </summary>
public static class DeterministicHash
{
    /// <summary>
    /// Berechnet einen 16 Zeichen langen hexadezimalen SHA-256-Hash aus <paramref name="input"/>.
    /// Die Länge ist bewusst gekürzt, damit der Wert lesbar im Termintext hinterlegt werden kann,
    /// bei gleichzeitig vernachlässigbarer Kollisionswahrscheinlichkeit für diesen Anwendungsfall.
    /// </summary>
    public static string Compute(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16];
    }
}
