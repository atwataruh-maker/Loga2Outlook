namespace LogaOutlookSync.Infrastructure;

/// <summary>
/// Zentrale, portable Dateipfade unterhalb des Benutzerprofils. Die Anwendung schreibt
/// bewusst nichts außerhalb von <see cref="RootDirectory"/>, damit sie ohne Installation
/// und ohne Administratorrechte lauffähig bleibt.
/// </summary>
public static class AppPaths
{
    /// <summary>Stammverzeichnis: <c>%LOCALAPPDATA%\LogaOutlookSync</c>.</summary>
    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LogaOutlookSync");

    public static string LogsDirectory => Path.Combine(RootDirectory, "Logs");

    public static string DiagnosticsDirectory => Path.Combine(RootDirectory, "Diagnostics");

    public static string DebugArtifactsDirectory => Path.Combine(RootDirectory, "DebugArtifacts");

    public static string ConfigDirectory => Path.Combine(RootDirectory, "Config");

    public static string SettingsFilePath => Path.Combine(RootDirectory, "settings.json");

    public static string SelectorsFilePath => Path.Combine(ConfigDirectory, "loga-selectors.json");

    public static string NavigationFilePath => Path.Combine(ConfigDirectory, "loga-navigation.json");

    /// <summary>Legt alle von der Anwendung benötigten Verzeichnisse an, falls sie noch nicht existieren.</summary>
    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(DiagnosticsDirectory);
        Directory.CreateDirectory(DebugArtifactsDirectory);
        Directory.CreateDirectory(ConfigDirectory);
    }
}
