namespace LogaOutlookSync.Loga.Configuration;

/// <summary>
/// Zentrale, LOGA-spezifische Navigationskonfiguration. Wird aus <c>loga-navigation.json</c>
/// geladen. Beschreibt, wie die Anwendung nach dem Login zum persönlichen Kalender navigiert.
/// Die aufzurufende LOGA-Adresse selbst wird bewusst nicht hier, sondern als
/// "LOGA-Webadresse" im Einrichtungsassistenten verwaltet (<c>AppSettings.LogaBaseUrl</c>),
/// damit es nur eine Stelle für diesen Wert gibt.
/// </summary>
public sealed class LogaNavigationConfig
{
    /// <summary>Geordnete Liste der Navigationsschritte nach erfolgreichem Login bis zum persönlichen Kalender.</summary>
    public List<NavigationStep> StepsAfterLogin { get; set; } = new();
}

/// <summary>Art der auszuführenden Navigationsaktion.</summary>
public enum NavigationActionType
{
    /// <summary>Direkt eine URL aufrufen.</summary>
    Goto,

    /// <summary>Auf ein Element klicken.</summary>
    Click,

    /// <summary>Warten, bis ein Element sichtbar ist, ohne zu interagieren.</summary>
    WaitForSelector,
}

/// <summary>Ein einzelner, konfigurierbarer Navigationsschritt.</summary>
public sealed class NavigationStep
{
    /// <summary>Für Menschen lesbare Beschreibung, wird in Fehlermeldungen verwendet.</summary>
    public string Description { get; set; } = string.Empty;

    public NavigationActionType Action { get; set; }

    /// <summary>Selektor, erforderlich bei <see cref="NavigationActionType.Click"/> und <see cref="NavigationActionType.WaitForSelector"/>.</summary>
    public string? Selector { get; set; }

    /// <summary>Ziel-URL, erforderlich bei <see cref="NavigationActionType.Goto"/>.</summary>
    public string? Url { get; set; }

    /// <summary>Timeout in Millisekunden für diesen Schritt.</summary>
    public int TimeoutMs { get; set; } = 15000;
}
