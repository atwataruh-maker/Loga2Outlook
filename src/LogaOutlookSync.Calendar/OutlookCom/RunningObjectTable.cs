using System.Runtime.InteropServices;

namespace LogaOutlookSync.Calendar.OutlookCom;

/// <summary>
/// Ersatz fuer <c>System.Runtime.InteropServices.Marshal.GetActiveObject</c>, das unter
/// .NET (Core) 5+ nicht mehr Teil von <see cref="Marshal"/> ist (im Gegensatz zum .NET
/// Framework). Ermittelt eine bereits laufende COM-Serverinstanz (hier: ein bereits
/// geoeffnetes Outlook) ueber die Running Object Table.
/// </summary>
internal static class RunningObjectTable
{
    [DllImport("ole32.dll")]
    private static extern int CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] string progId, out Guid clsid);

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(
        ref Guid rclsid,
        IntPtr reserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object activeObject);

    /// <summary>
    /// Versucht, die bereits laufende COM-Instanz fuer <paramref name="progId"/> zu ermitteln.
    /// Gibt <see langword="false"/> zurueck, wenn keine Instanz laeuft oder die ProgID
    /// unbekannt ist - in beiden Faellen ohne Ausnahme, damit der Aufrufer stattdessen eine
    /// neue Instanz erzeugen kann.
    /// </summary>
    public static bool TryGetActiveObject(string progId, out object? activeObject)
    {
        if (CLSIDFromProgID(progId, out var clsid) != 0)
        {
            activeObject = null;
            return false;
        }

        if (GetActiveObject(ref clsid, IntPtr.Zero, out var comObject) != 0)
        {
            activeObject = null;
            return false;
        }

        activeObject = comObject;
        return true;
    }
}
