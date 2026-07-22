using AngleSharp.Html.Parser;
using LogaOutlookSync.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace LogaOutlookSync.Loga;

/// <summary>
/// Erstellt bei Fehlern während der Browserautomatisierung einen Screenshot und optional
/// einen HTML-Snapshot der aktuellen Seite zur Fehlerdiagnose. Entfernt dabei zuverlässig
/// Passwortfeld-Werte aus dem HTML, bevor es gespeichert wird.
/// </summary>
public sealed class DebugArtifactCollector
{
    private readonly ILogger<DebugArtifactCollector> _logger;

    public DebugArtifactCollector(ILogger<DebugArtifactCollector> logger)
    {
        _logger = logger;
    }

    public async Task<(string? ScreenshotPath, string? HtmlPath)> CaptureAsync(IPage page, string context, CancellationToken cancellationToken)
    {
        AppPaths.EnsureDirectoriesExist();
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff");
        var safeContext = SanitizeFileNameComponent(context);

        var screenshotPath = await TryCaptureScreenshotAsync(page, timestamp, safeContext).ConfigureAwait(false);
        var htmlPath = await TryCaptureHtmlAsync(page, timestamp, safeContext, cancellationToken).ConfigureAwait(false);

        return (screenshotPath, htmlPath);
    }

    private async Task<string?> TryCaptureScreenshotAsync(IPage page, string timestamp, string safeContext)
    {
        var path = Path.Combine(AppPaths.DebugArtifactsDirectory, $"{timestamp}-{safeContext}.png");
        try
        {
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true }).ConfigureAwait(false);
            return path;
        }
        catch (PlaywrightException ex)
        {
            _logger.LogWarning(ex, "Screenshot für Debug-Artefakt '{Context}' konnte nicht erstellt werden.", safeContext);
            return null;
        }
    }

    private async Task<string?> TryCaptureHtmlAsync(IPage page, string timestamp, string safeContext, CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppPaths.DebugArtifactsDirectory, $"{timestamp}-{safeContext}.html");
        try
        {
            var html = await page.ContentAsync().ConfigureAwait(false);
            var redacted = RedactSensitiveHtml(html);
            await File.WriteAllTextAsync(path, redacted, cancellationToken).ConfigureAwait(false);
            return path;
        }
        catch (PlaywrightException ex)
        {
            _logger.LogWarning(ex, "HTML-Snapshot für Debug-Artefakt '{Context}' konnte nicht erstellt werden.", safeContext);
            return null;
        }
    }

    /// <summary>
    /// Entfernt Werte von Passwortfeldern sowie Script-Inhalte (die u. U. Session-Token
    /// enthalten könnten) aus einem HTML-Dokument, bevor es als Diagnose-Artefakt gespeichert wird.
    /// </summary>
    internal static string RedactSensitiveHtml(string html)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);

        foreach (var passwordField in document.QuerySelectorAll("input[type='password']"))
        {
            passwordField.SetAttribute("value", string.Empty);
        }

        foreach (var scriptTag in document.QuerySelectorAll("script").ToArray())
        {
            scriptTag.TextContent = string.Empty;
        }

        return document.DocumentElement.OuterHtml;
    }

    private static string SanitizeFileNameComponent(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}
