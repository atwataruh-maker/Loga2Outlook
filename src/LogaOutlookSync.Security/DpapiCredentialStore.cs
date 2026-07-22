using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;

namespace LogaOutlookSync.Security;

/// <summary>
/// Alternative Implementierung von <see cref="ICredentialStore"/> auf Basis von Windows DPAPI,
/// falls der Windows Credential Manager technisch nicht sinnvoll einsetzbar ist (z. B. durch
/// Gruppenrichtlinien eingeschränkt). Die verschlüsselte Datei ist über
/// <see cref="DataProtectionScope.CurrentUser"/> untrennbar an das aktuelle Windows-Benutzerkonto
/// gebunden und kann von keinem anderen Konto entschlüsselt werden.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiCredentialStore : ICredentialStore
{
    private static readonly byte[] Entropy = "LogaOutlookSync.CredentialEntropy.v1"u8.ToArray();

    private readonly string _filePath;

    public DpapiCredentialStore(string? credentialFilePath = null)
    {
        _filePath = credentialFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LogaOutlookSync",
            "credentials.dpapi");
    }

    public async Task SaveAsync(string userName, string password, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var payload = JsonSerializer.SerializeToUtf8Bytes(new PersistedCredential(userName, password));
        var protectedPayload = ProtectedData.Protect(payload, Entropy, DataProtectionScope.CurrentUser);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(_filePath, protectedPayload, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredCredential?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        var protectedPayload = await File.ReadAllBytesAsync(_filePath, cancellationToken).ConfigureAwait(false);

        byte[] payload;
        try
        {
            payload = ProtectedData.Unprotect(protectedPayload, Entropy, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                "Die gespeicherten LOGA-Zugangsdaten konnten nicht entschlüsselt werden. " +
                "Dies geschieht typischerweise, wenn die Datei mit einem anderen Windows-Benutzerkonto " +
                "verschlüsselt wurde. Bitte richten Sie die Zugangsdaten über den Einrichtungsassistenten neu ein.",
                ex);
        }

        var credential = JsonSerializer.Deserialize<PersistedCredential>(payload)
            ?? throw new InvalidOperationException("Die gespeicherten LOGA-Zugangsdaten sind beschädigt.");

        return new StoredCredential(credential.UserName, credential.Password);
    }

    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }

        return Task.CompletedTask;
    }

    private sealed record PersistedCredential(string UserName, string Password);
}
