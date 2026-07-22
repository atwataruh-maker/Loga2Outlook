using System.Text.RegularExpressions;

namespace LogaOutlookSync.Infrastructure.Logging;

/// <summary>
/// Zusätzliche Verteidigungslinie gegen versehentliches Protokollieren sensibler Daten
/// (Passwörter, Cookies, Bearer-Token). Ersetzt die eigentliche Sorgfaltspflicht im Code
/// nicht: Aufrufer dürfen Passwörter, Token und vollständige Sitzungs-Cookies grundsätzlich
/// niemals an den Logger übergeben.
/// </summary>
public static partial class SensitiveDataScrubber
{
    private const string Redacted = "[REDACTED]";

    private static readonly Regex[] Patterns =
    {
        PasswordAssignmentPattern(),
        BearerTokenPattern(),
        CookieHeaderPattern(),
        SetCookieHeaderPattern(),
    };

    /// <summary>Ersetzt bekannte Muster sensibler Daten in <paramref name="text"/> durch einen Platzhalter.</summary>
    public static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var result = text;
        foreach (var pattern in Patterns)
        {
            result = pattern.Replace(result, match => match.Groups["key"].Success
                ? $"{match.Groups["key"].Value}={Redacted}"
                : Redacted);
        }

        return result;
    }

    [GeneratedRegex(@"(?<key>(?i:password|pwd|passwort))\s*[:=]\s*\S+", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordAssignmentPattern();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-_\.]+", RegexOptions.IgnoreCase)]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex(@"(?i:cookie)\s*:\s*.+", RegexOptions.IgnoreCase)]
    private static partial Regex CookieHeaderPattern();

    [GeneratedRegex(@"(?i:set-cookie)\s*:\s*.+", RegexOptions.IgnoreCase)]
    private static partial Regex SetCookieHeaderPattern();
}
