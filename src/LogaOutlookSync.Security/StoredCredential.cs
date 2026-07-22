namespace LogaOutlookSync.Security;

/// <summary>Aus dem sicheren Speicher geladene LOGA-Zugangsdaten.</summary>
/// <param name="UserName">LOGA-Benutzername.</param>
/// <param name="Password">
/// LOGA-Passwort im Klartext, ausschließlich zur unmittelbaren Verwendung beim Login.
/// Darf niemals protokolliert, in Fehlermeldungen ausgegeben oder persistiert werden.
/// </param>
public sealed record StoredCredential(string UserName, string Password);
