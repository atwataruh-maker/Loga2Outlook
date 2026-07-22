using System.Runtime.Versioning;
using System.Text;

namespace LogaOutlookSync.Security;

/// <summary>
/// Speichert die LOGA-Zugangsdaten im Windows Credential Manager unter dem eindeutigen
/// Eintragsnamen <c>LOGA-Outlook-Sync</c>. Bevorzugte, sicherheitsempfohlene Implementierung
/// von <see cref="ICredentialStore"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialManagerStore : ICredentialStore
{
    /// <summary>Eindeutiger Name des Credential-Manager-Eintrags.</summary>
    public const string TargetName = "LOGA-Outlook-Sync";

    public Task SaveAsync(string userName, string password, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        cancellationToken.ThrowIfCancellationRequested();

        var secretBytes = Encoding.Unicode.GetBytes(password);
        NativeCredentialInterop.Write(TargetName, userName, secretBytes);
        return Task.CompletedTask;
    }

    public Task<StoredCredential?> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entry = NativeCredentialInterop.Read(TargetName);
        if (entry is null)
        {
            return Task.FromResult<StoredCredential?>(null);
        }

        var (userName, secret) = entry.Value;
        var password = Encoding.Unicode.GetString(secret);
        return Task.FromResult<StoredCredential?>(new StoredCredential(userName, password));
    }

    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NativeCredentialInterop.Delete(TargetName);
        return Task.CompletedTask;
    }
}
