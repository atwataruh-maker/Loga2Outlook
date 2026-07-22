using LogaOutlookSync.Domain;

namespace LogaOutlookSync.Calendar.Graph;

/// <summary>
/// Baut die MAPI-Named-Property-IDs für die "Single Value Extended Properties", über die die
/// LOGA-Sync-Kennung, der "ManagedBy"-Marker und der Quell-Fingerprint an Graph-Terminen
/// hinterlegt werden. Format gemäß Microsoft-Graph-Dokumentation: "String {GUID} Name Name".
/// </summary>
internal static class GraphExtendedPropertyIds
{
    /// <summary>
    /// Anwendungseigener, fest verdrahteter Eigenschaftssatz-Namespace. Beliebig, aber stabil -
    /// entscheidend ist nur, dass er ausschließlich von dieser Anwendung verwendet wird.
    /// </summary>
    private const string PropertySetGuid = "c11ff204-1c11-40bb-9d52-9d3e9a4c5a2e";

    public static readonly string SyncId = Build(ManagedEntryMetadata.SyncIdPropertyName);
    public static readonly string ManagedBy = Build(ManagedEntryMetadata.ManagedByPropertyName);
    public static readonly string Fingerprint = Build(ManagedEntryMetadata.FingerprintPropertyName);

    private static string Build(string propertyName) => $"String {{{PropertySetGuid}}} Name {propertyName}";
}
