using System.Globalization;
using System.Text;

namespace LogaOutlookSync.Domain;

/// <summary>
/// Providerunabhängige Konventionen zur Kennzeichnung von Outlook-Terminen, die von
/// dieser Anwendung erstellt und verwaltet werden. Nur Termine, die eindeutig als
/// von dieser Anwendung verwaltet erkannt werden, dürfen aktualisiert oder gelöscht werden.
/// </summary>
public static class ManagedEntryMetadata
{
    /// <summary>Name der Anwendung, wie er in der "ManagedBy"-Kennzeichnung hinterlegt wird.</summary>
    public const string ManagedByValue = "LogaOutlookSync";

    /// <summary>Name der erweiterten Eigenschaft / des benutzerdefinierten Felds für die LOGA-Sync-ID.</summary>
    public const string SyncIdPropertyName = "LOGA-SYNC-ID";

    /// <summary>Name der erweiterten Eigenschaft / des benutzerdefinierten Felds für den "ManagedBy"-Marker.</summary>
    public const string ManagedByPropertyName = "ManagedBy";

    /// <summary>Name der erweiterten Eigenschaft für den Quell-Fingerprint (Änderungserkennung).</summary>
    public const string FingerprintPropertyName = "LOGA-SOURCE-FINGERPRINT";

    /// <summary>
    /// Namespace/GUID-Präfix, unter dem Microsoft-Graph-Open-Extensions bzw.
    /// Outlook-COM-benutzerdefinierte Eigenschaften abgelegt werden.
    /// </summary>
    public const string ExtensionNamespace = "com.logaoutlooksync.managedentry";

    private const string FooterMarkerStart = "---- LogaOutlookSync (nicht bearbeiten) ----";
    private const string FooterMarkerEnd = "---- Ende LogaOutlookSync ----";

    /// <summary>
    /// Baut den technischen Fußabschnitt, der zusätzlich zur erweiterten Eigenschaft
    /// im Termintext hinterlegt wird, damit die Kennung auch bei Anzeigeproblemen der
    /// erweiterten Eigenschaften sichtbar bleibt.
    /// </summary>
    public static string BuildTechnicalFooter(string syncId, string sourceFingerprint)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine(FooterMarkerStart);
        sb.AppendLine(CultureInfo.InvariantCulture, $"{SyncIdPropertyName}: {syncId}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"{ManagedByPropertyName}: {ManagedByValue}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"{FingerprintPropertyName}: {sourceFingerprint}");
        sb.Append(FooterMarkerEnd);
        return sb.ToString();
    }

    /// <summary>
    /// Extrahiert die LOGA-Sync-ID aus dem technischen Fußabschnitt eines Termintexts,
    /// falls dort vorhanden (Fallback, wenn keine erweiterte Eigenschaft gelesen werden kann).
    /// </summary>
    public static string? TryExtractSyncIdFromBody(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return null;
        }

        var startIndex = body.IndexOf(FooterMarkerStart, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            return null;
        }

        var prefix = SyncIdPropertyName + ":";
        var prefixIndex = body.IndexOf(prefix, startIndex, StringComparison.Ordinal);
        if (prefixIndex < 0)
        {
            return null;
        }

        var lineEnd = body.IndexOf('\n', prefixIndex);
        var line = lineEnd >= 0
            ? body[(prefixIndex + prefix.Length)..lineEnd]
            : body[(prefixIndex + prefix.Length)..];

        return line.Trim();
    }

    /// <summary>Prüft, ob ein Termintext die "ManagedBy"-Kennzeichnung dieser Anwendung enthält.</summary>
    public static bool BodyContainsManagedByMarker(string? body)
    {
        return !string.IsNullOrEmpty(body)
            && body.Contains($"{ManagedByPropertyName}: {ManagedByValue}", StringComparison.Ordinal);
    }
}
