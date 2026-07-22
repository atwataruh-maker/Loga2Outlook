using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LogaOutlookSync.Security;

/// <summary>
/// Low-Level-P/Invoke-Wrapper für die Windows Credential Manager API (advapi32.dll).
/// Wird ausschließlich von <see cref="WindowsCredentialManagerStore"/> verwendet.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class NativeCredentialInterop
{
    internal const uint CredTypeGeneric = 1;
    internal const uint CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct Credential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref Credential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr credentialPtr);

    /// <summary>Legt einen generischen Credential-Eintrag an oder überschreibt ihn.</summary>
    public static void Write(string targetName, string userName, byte[] secretBytes)
    {
        var blobHandle = GCHandle.Alloc(secretBytes, GCHandleType.Pinned);
        try
        {
            var credential = new Credential
            {
                Flags = 0,
                Type = CredTypeGeneric,
                TargetName = Marshal.StringToCoTaskMemUni(targetName),
                Comment = IntPtr.Zero,
                LastWritten = 0,
                CredentialBlobSize = (uint)secretBytes.Length,
                CredentialBlob = blobHandle.AddrOfPinnedObject(),
                Persist = CredPersistLocalMachine,
                AttributeCount = 0,
                Attributes = IntPtr.Zero,
                TargetAlias = IntPtr.Zero,
                UserName = Marshal.StringToCoTaskMemUni(userName),
            };

            try
            {
                if (!CredWrite(ref credential, 0))
                {
                    var error = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException(
                        $"Windows Credential Manager: Schreiben des Eintrags '{targetName}' fehlgeschlagen (Fehlercode {error}).");
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(credential.TargetName);
                Marshal.FreeCoTaskMem(credential.UserName);
            }
        }
        finally
        {
            blobHandle.Free();
        }
    }

    /// <summary>
    /// Liest einen generischen Credential-Eintrag. Gibt <see langword="null"/> zurück,
    /// wenn kein Eintrag mit diesem Namen existiert.
    /// </summary>
    public static (string UserName, byte[] Secret)? Read(string targetName)
    {
        if (!CredRead(targetName, CredTypeGeneric, 0, out var credentialPtr))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                return null;
            }

            throw new InvalidOperationException(
                $"Windows Credential Manager: Lesen des Eintrags '{targetName}' fehlgeschlagen (Fehlercode {error}).");
        }

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(credentialPtr);
            var userName = credential.UserName == IntPtr.Zero
                ? string.Empty
                : Marshal.PtrToStringUni(credential.UserName) ?? string.Empty;

            var secret = new byte[credential.CredentialBlobSize];
            if (credential.CredentialBlobSize > 0 && credential.CredentialBlob != IntPtr.Zero)
            {
                Marshal.Copy(credential.CredentialBlob, secret, 0, (int)credential.CredentialBlobSize);
            }

            return (userName, secret);
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    /// <summary>Entfernt einen generischen Credential-Eintrag, falls vorhanden.</summary>
    public static void Delete(string targetName)
    {
        if (!CredDelete(targetName, CredTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound)
            {
                throw new InvalidOperationException(
                    $"Windows Credential Manager: Löschen des Eintrags '{targetName}' fehlgeschlagen (Fehlercode {error}).");
            }
        }
    }
}
