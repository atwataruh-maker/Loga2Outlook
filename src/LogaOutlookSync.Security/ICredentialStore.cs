namespace LogaOutlookSync.Security;

/// <summary>
/// Sichere Speicherung der LOGA-Zugangsdaten. Implementierungen dürfen Passwörter
/// niemals im Klartext auf der Festplatte ablegen (siehe <see cref="WindowsCredentialManagerStore"/>
/// und <see cref="DpapiCredentialStore"/>).
/// </summary>
public interface ICredentialStore
{
    /// <summary>Speichert Benutzername und Passwort sicher, überschreibt einen vorhandenen Eintrag.</summary>
    Task SaveAsync(string userName, string password, CancellationToken cancellationToken);

    /// <summary>Lädt die gespeicherten Zugangsdaten, oder <see langword="null"/>, wenn keine hinterlegt sind.</summary>
    Task<StoredCredential?> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Entfernt gespeicherte Zugangsdaten, z. B. wenn der Benutzer sie zurücksetzt.</summary>
    Task DeleteAsync(CancellationToken cancellationToken);
}
