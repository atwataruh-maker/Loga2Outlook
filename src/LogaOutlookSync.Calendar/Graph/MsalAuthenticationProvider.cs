using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using LogaOutlookSync.Calendar.Exceptions;

namespace LogaOutlookSync.Calendar.Graph;

/// <summary>
/// Kiota-<see cref="IAuthenticationProvider"/>, der Zugriffstoken über MSAL.NET beschafft.
/// Versucht zunächst eine stille Anmeldung über den persistenten Token-Cache und fragt nur bei
/// Bedarf interaktiv über den System-Browser nach (kein automatisches Umgehen von MFA).
/// </summary>
internal sealed class MsalAuthenticationProvider : IAuthenticationProvider
{
    private readonly IPublicClientApplication _app;
    private readonly string[] _scopes;

    public MsalAuthenticationProvider(IPublicClientApplication app, string[] scopes)
    {
        _app = app;
        _scopes = scopes;
    }

    public async Task AuthenticateRequestAsync(
        RequestInformation request,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await AcquireAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
    }

    private async Task<string> AcquireAccessTokenAsync(CancellationToken cancellationToken)
    {
        var accounts = await _app.GetAccountsAsync().ConfigureAwait(false);

        try
        {
            var silentResult = await _app.AcquireTokenSilent(_scopes, accounts.FirstOrDefault())
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);
            return silentResult.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            try
            {
                var interactiveResult = await _app.AcquireTokenInteractive(_scopes)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(false);
                return interactiveResult.AccessToken;
            }
            catch (MsalException ex)
            {
                throw new GraphAuthenticationException(
                    $"Die Anmeldung bei Microsoft Graph ist fehlgeschlagen: {ex.Message}. " +
                    "Bitte prüfen Sie, ob die App-Registrierung korrekt eingerichtet ist und die " +
                    "erforderlichen Berechtigungen (Calendars.ReadWrite) von einem Administrator " +
                    "freigegeben wurden.",
                    ex);
            }
        }
    }
}
