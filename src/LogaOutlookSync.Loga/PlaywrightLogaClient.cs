using LogaOutlookSync.Domain;
using LogaOutlookSync.Infrastructure;
using LogaOutlookSync.Loga.Configuration;
using LogaOutlookSync.Loga.Exceptions;
using LogaOutlookSync.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace LogaOutlookSync.Loga;

/// <summary>
/// Bedient das LOGA-Webportal über Microsoft Edge (per Playwright) und liefert normalisierte
/// Abwesenheitseinträge. Jeder öffentliche Aufruf öffnet eine eigene, vollständig isolierte
/// Browsersitzung und schließt sie zuverlässig wieder, auch im Fehlerfall.
/// </summary>
public sealed class PlaywrightLogaClient : ILogaClient
{
    private readonly ICredentialStore _credentialStore;
    private readonly AppSettings _settings;
    private readonly LogaSelectorsProvider _selectorsProvider;
    private readonly DebugArtifactCollector _debugArtifacts;
    private readonly ILogger<PlaywrightLogaClient> _logger;

    public PlaywrightLogaClient(
        ICredentialStore credentialStore,
        AppSettings settings,
        LogaSelectorsProvider selectorsProvider,
        DebugArtifactCollector debugArtifacts,
        ILogger<PlaywrightLogaClient> logger)
    {
        _credentialStore = credentialStore;
        _settings = settings;
        _selectorsProvider = selectorsProvider;
        _debugArtifacts = debugArtifacts;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AbsenceEntry>> GetAbsencesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var selectors = _selectorsProvider.LoadSelectors();
        var navigation = _selectorsProvider.LoadNavigation();
        var credential = await LoadRequiredCredentialAsync(cancellationToken).ConfigureAwait(false);

        return await RunSessionAsync(async page =>
        {
            await LoginAsync(page, selectors.Login, navigation, credential, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("LOGA-Anmeldung erfolgreich.");

            await RunNavigationStepsAsync(page, navigation.StepsAfterLogin, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Persönlicher LOGA-Kalender geöffnet.");

            var html = await page.ContentAsync().ConfigureAwait(false);
            IReadOnlyList<AbsenceEntry> allEntries;
            try
            {
                allEntries = LogaCalendarHtmlParser.Parse(html, selectors.CalendarParsing);
            }
            catch (LogaParsingException)
            {
                await CaptureDebugArtifactsAsync(page, "parsing-error", cancellationToken).ConfigureAwait(false);
                throw;
            }

            var relevant = allEntries
                .Where(e => e.ApprovalStatus == AbsenceApprovalStatus.Approved)
                .Where(e => e.EndDate >= from && e.StartDate <= to)
                .ToList();

            var vacationCount = relevant.Count(e => e.Type == AbsenceType.Vacation);
            var flexTimeCount = relevant.Count(e => e.Type == AbsenceType.FlexTime);

            _logger.LogInformation(
                "LOGA-Kalender ausgewertet: {Total} Einträge erkannt, davon {Relevant} genehmigt im Zeitraum " +
                "{From:yyyy-MM-dd} - {To:yyyy-MM-dd} ({Vacation} Urlaub, {FlexTime} Gleitzeit).",
                allEntries.Count, relevant.Count, from, to, vacationCount, flexTimeCount);

            return (IReadOnlyList<AbsenceEntry>)relevant;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LoginTestResult> TestLoginAsync(CancellationToken cancellationToken)
    {
        try
        {
            var selectors = _selectorsProvider.LoadSelectors();
            var navigation = _selectorsProvider.LoadNavigation();
            var credential = await LoadRequiredCredentialAsync(cancellationToken).ConfigureAwait(false);

            await RunSessionAsync(async page =>
            {
                await LoginAsync(page, selectors.Login, navigation, credential, cancellationToken).ConfigureAwait(false);
                return true;
            }, cancellationToken).ConfigureAwait(false);

            return new LoginTestResult(true, null);
        }
        catch (LogaAutomationException ex)
        {
            _logger.LogWarning(ex, "LOGA-Anmeldetest fehlgeschlagen.");
            return new LoginTestResult(false, ex.Message);
        }
    }

    private async Task<StoredCredential> LoadRequiredCredentialAsync(CancellationToken cancellationToken)
    {
        var credential = await _credentialStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (credential is null)
        {
            throw new LogaConfigurationException(
                "Es sind keine LOGA-Zugangsdaten hinterlegt. Bitte richten Sie Benutzername und Passwort " +
                "im Einrichtungsassistenten ein.");
        }

        return credential;
    }

    private async Task LoginAsync(
        IPage page,
        LoginSelectors login,
        LogaNavigationConfig navigation,
        StoredCredential credential,
        CancellationToken cancellationToken)
    {
        try
        {
            await page.GotoAsync(navigation.LoginPageUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle })
                .ConfigureAwait(false);
        }
        catch (PlaywrightException ex)
        {
            throw new LogaConnectivityException(
                $"Die LOGA-Adresse '{navigation.LoginPageUrl}' konnte nicht geöffnet werden. " +
                "Bitte prüfen Sie die Internetverbindung und ob LOGA erreichbar ist.",
                ex);
        }

        try
        {
            await page.Locator(login.UsernameField).FillAsync(credential.UserName).ConfigureAwait(false);
            await page.Locator(login.PasswordField).FillAsync(credential.Password).ConfigureAwait(false);
            await page.Locator(login.SubmitButton).ClickAsync().ConfigureAwait(false);

            await CheckForMfaChallengeAsync(page, login, cancellationToken).ConfigureAwait(false);
            await CheckForLoginErrorAsync(page, login, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(login.LoggedInIndicator))
            {
                await page.Locator(login.LoggedInIndicator)
                    .WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 })
                    .ConfigureAwait(false);
            }
        }
        catch (LogaAutomationException)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            await CaptureDebugArtifactsAsync(page, "login-timeout", cancellationToken).ConfigureAwait(false);
            throw new LogaLoginException(
                "Die LOGA-Anmeldung hat zu lange gedauert bzw. ein erwartetes Element wurde nicht gefunden. " +
                "Möglicherweise hat sich die Struktur der LOGA-Loginseite geändert.",
                failedStep: "Login-Timeout",
                selector: login.LoggedInIndicator,
                innerException: ex);
        }
        catch (PlaywrightException ex)
        {
            await CaptureDebugArtifactsAsync(page, "login-error", cancellationToken).ConfigureAwait(false);
            throw new LogaLoginException($"Die LOGA-Anmeldung ist fehlgeschlagen: {ex.Message}", innerException: ex);
        }
    }

    private async Task CheckForMfaChallengeAsync(IPage page, LoginSelectors login, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(login.MfaChallengeIndicator))
        {
            return;
        }

        var mfaLocator = page.Locator(login.MfaChallengeIndicator);
        if (await mfaLocator.CountAsync().ConfigureAwait(false) > 0)
        {
            await CaptureDebugArtifactsAsync(page, "mfa-detected", cancellationToken).ConfigureAwait(false);
            throw new LogaLoginException(
                "Für dieses LOGA-Konto wurde eine Multi-Faktor-Authentifizierung erkannt. Diese Anwendung führt " +
                "keine automatisierte MFA durch und darf Sicherheitsmechanismen nicht umgehen. Bitte klären Sie " +
                "mit Ihrer IT, ob ein alternativer Zugang eingerichtet werden kann.",
                failedStep: "MFA-Erkennung",
                selector: login.MfaChallengeIndicator);
        }
    }

    private async Task CheckForLoginErrorAsync(IPage page, LoginSelectors login, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(login.ErrorMessage))
        {
            return;
        }

        var errorLocator = page.Locator(login.ErrorMessage);
        if (await errorLocator.CountAsync().ConfigureAwait(false) > 0)
        {
            var errorText = await errorLocator.First.TextContentAsync().ConfigureAwait(false);
            await CaptureDebugArtifactsAsync(page, "login-rejected", cancellationToken).ConfigureAwait(false);
            throw new LogaLoginException(
                $"LOGA-Anmeldung fehlgeschlagen: {errorText?.Trim() ?? "Unbekannter Fehler"}. " +
                "Bitte Benutzername und Passwort im Einrichtungsassistenten prüfen. Falls die Zugangsdaten " +
                "korrekt sind, wurde das Konto möglicherweise gesperrt oder das Passwort ist abgelaufen.",
                failedStep: "Login-Fehlermeldung",
                selector: login.ErrorMessage);
        }
    }

    private async Task RunNavigationStepsAsync(IPage page, IReadOnlyList<NavigationStep> steps, CancellationToken cancellationToken)
    {
        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await ExecuteNavigationStepAsync(page, step).ConfigureAwait(false);
            }
            catch (LogaAutomationException)
            {
                throw;
            }
            catch (Exception ex) when (ex is TimeoutException or PlaywrightException)
            {
                await CaptureDebugArtifactsAsync(page, $"navigation-{step.Description}", cancellationToken).ConfigureAwait(false);
                throw new LogaNavigationException(
                    $"Navigationsschritt '{step.Description}' ist fehlgeschlagen. Die Seitenstruktur von LOGA hat " +
                    "sich möglicherweise geändert. Bitte den betroffenen Selektor in loga-navigation.json im " +
                    "Analysemodus neu ermitteln.",
                    failedStep: step.Description,
                    selector: step.Selector,
                    innerException: ex);
            }
        }
    }

    private static async Task ExecuteNavigationStepAsync(IPage page, NavigationStep step)
    {
        switch (step.Action)
        {
            case NavigationActionType.Goto:
                var url = step.Url ?? throw new LogaConfigurationException(
                    $"Navigationsschritt '{step.Description}' hat keine URL konfiguriert.");
                await page.GotoAsync(url, new PageGotoOptions { Timeout = step.TimeoutMs }).ConfigureAwait(false);
                break;

            case NavigationActionType.Click:
                var clickSelector = step.Selector ?? throw new LogaConfigurationException(
                    $"Navigationsschritt '{step.Description}' hat keinen Selektor konfiguriert.");
                await page.Locator(clickSelector)
                    .ClickAsync(new LocatorClickOptions { Timeout = step.TimeoutMs })
                    .ConfigureAwait(false);
                break;

            case NavigationActionType.WaitForSelector:
                var waitSelector = step.Selector ?? throw new LogaConfigurationException(
                    $"Navigationsschritt '{step.Description}' hat keinen Selektor konfiguriert.");
                await page.Locator(waitSelector)
                    .WaitForAsync(new LocatorWaitForOptions { Timeout = step.TimeoutMs })
                    .ConfigureAwait(false);
                break;

            default:
                throw new LogaConfigurationException(
                    $"Unbekannte Navigationsaktion '{step.Action}' bei Schritt '{step.Description}'.");
        }
    }

    private async Task CaptureDebugArtifactsAsync(IPage page, string context, CancellationToken cancellationToken)
    {
        var (screenshotPath, htmlPath) = await _debugArtifacts.CaptureAsync(page, context, cancellationToken).ConfigureAwait(false);
        _logger.LogWarning(
            "Debug-Artefakte gespeichert. Screenshot: {ScreenshotPath}, HTML-Snapshot: {HtmlPath}",
            screenshotPath ?? "(nicht verfügbar)",
            htmlPath ?? "(nicht verfügbar)");
    }

    private async Task<T> RunSessionAsync<T>(Func<IPage, Task<T>> action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IPlaywright playwright;
        try
        {
            playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        }
        catch (PlaywrightException ex)
        {
            throw new LogaBrowserException(
                "Playwright konnte nicht initialisiert werden. Möglicherweise fehlen erforderliche Playwright-" +
                "Komponenten im portablen Anwendungsordner, oder der Ordner wurde unvollständig kopiert.",
                ex);
        }

        try
        {
            IBrowser browser;
            try
            {
                browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Channel = "msedge",
                    Headless = !_settings.BrowserVisible,
                }).ConfigureAwait(false);
            }
            catch (PlaywrightException ex)
            {
                throw new LogaBrowserException(
                    "Microsoft Edge konnte nicht gestartet werden. Bitte prüfen Sie, ob Microsoft Edge auf diesem " +
                    "Rechner installiert ist.",
                    ex);
            }

            try
            {
                var context = await browser.NewContextAsync(new BrowserNewContextOptions { Locale = "de-DE" })
                    .ConfigureAwait(false);
                try
                {
                    var page = await context.NewPageAsync().ConfigureAwait(false);
                    return await action(page).ConfigureAwait(false);
                }
                finally
                {
                    await context.CloseAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                await browser.CloseAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            playwright.Dispose();
        }
    }
}
