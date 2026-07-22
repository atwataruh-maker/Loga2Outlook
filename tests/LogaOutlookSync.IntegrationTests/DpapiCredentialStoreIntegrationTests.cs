using FluentAssertions;
using LogaOutlookSync.Security;
using Xunit;

namespace LogaOutlookSync.IntegrationTests;

/// <summary>
/// Prüft das tatsächliche Verschlüsseln/Entschlüsseln über Windows DPAPI und das Schreiben/
/// Lesen der Datei auf der Festplatte. Erfordert Windows und ein reguläres Benutzerprofil.
/// </summary>
public sealed class DpapiCredentialStoreIntegrationTests : IDisposable
{
    private readonly string _tempFilePath = Path.Combine(Path.GetTempPath(), $"loga-credential-test-{Guid.NewGuid():N}.dpapi");

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsCredential()
    {
        var store = new DpapiCredentialStore(_tempFilePath);

        await store.SaveAsync("max.mustermann", "Sup3rSecret!", CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        loaded.Should().NotBeNull();
        loaded!.UserName.Should().Be("max.mustermann");
        loaded.Password.Should().Be("Sup3rSecret!");
    }

    [Fact]
    public async Task SavedFile_DoesNotContainThePasswordInPlainText()
    {
        var store = new DpapiCredentialStore(_tempFilePath);
        const string password = "Sup3rSecret!";

        await store.SaveAsync("max.mustermann", password, CancellationToken.None);
        var rawBytes = await File.ReadAllBytesAsync(_tempFilePath);
        var rawText = System.Text.Encoding.UTF8.GetString(rawBytes);

        rawText.Should().NotContain(password);
    }

    [Fact]
    public async Task LoadAsync_ReturnsNull_WhenNoFileExists()
    {
        var store = new DpapiCredentialStore(_tempFilePath);

        var loaded = await store.LoadAsync(CancellationToken.None);

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheFile()
    {
        var store = new DpapiCredentialStore(_tempFilePath);
        await store.SaveAsync("max.mustermann", "Sup3rSecret!", CancellationToken.None);

        await store.DeleteAsync(CancellationToken.None);

        File.Exists(_tempFilePath).Should().BeFalse();
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }
}
