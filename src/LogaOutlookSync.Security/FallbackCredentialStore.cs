using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace LogaOutlookSync.Security;

/// <summary>
/// Versucht zuerst den Windows Credential Manager und weicht bei technischen Problemen
/// (z. B. durch Gruppenrichtlinien blockierter Zugriff) auf die DPAPI-Dateispeicherung aus.
/// Einmal erfolgreich gespeicherte Zugangsdaten werden konsistent im zuletzt erfolgreichen
/// Speicher gehalten.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FallbackCredentialStore : ICredentialStore
{
    private readonly WindowsCredentialManagerStore _primary;
    private readonly DpapiCredentialStore _fallback;
    private readonly ILogger<FallbackCredentialStore> _logger;

    public FallbackCredentialStore(
        WindowsCredentialManagerStore primary,
        DpapiCredentialStore fallback,
        ILogger<FallbackCredentialStore> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task SaveAsync(string userName, string password, CancellationToken cancellationToken)
    {
        try
        {
            await _primary.SaveAsync(userName, password, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("LOGA-Zugangsdaten im Windows Credential Manager gespeichert.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Speichern im Windows Credential Manager fehlgeschlagen, weiche auf DPAPI-Dateispeicherung aus.");
            await _fallback.SaveAsync(userName, password, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<StoredCredential?> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var credential = await _primary.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (credential is not null)
            {
                return credential;
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Lesen aus dem Windows Credential Manager fehlgeschlagen, versuche DPAPI-Dateispeicherung.");
        }

        return await _fallback.LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken)
    {
        await _primary.DeleteAsync(cancellationToken).ConfigureAwait(false);
        await _fallback.DeleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
