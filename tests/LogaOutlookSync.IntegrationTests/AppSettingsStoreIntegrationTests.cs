using FluentAssertions;
using LogaOutlookSync.Domain;
using LogaOutlookSync.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LogaOutlookSync.IntegrationTests;

/// <summary>
/// Prüft das tatsächliche Lesen/Schreiben der Einstellungsdatei auf der Festplatte
/// (im Gegensatz zu reinen, dateisystemunabhängigen Unit-Tests).
/// </summary>
public sealed class AppSettingsStoreIntegrationTests : IDisposable
{
    private readonly string _tempFilePath = Path.Combine(Path.GetTempPath(), $"loga-settings-test-{Guid.NewGuid():N}.json");

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsAllFields()
    {
        var store = new AppSettingsStore(NullLogger<AppSettingsStore>.Instance, _tempFilePath);
        var settings = new AppSettings
        {
            LogaBaseUrl = "https://example.invalid/loga",
            LogaUserName = "max.mustermann",
            CalendarProvider = CalendarProviderKind.OutlookCom,
            TargetCalendarDisplayName = "Kalender",
            TargetCalendarId = "Kalender",
            BrowserVisible = false,
            SyncPastDays = 45,
            SyncFutureMonths = 12,
            SubjectPrefix = "[Test] ",
            AppendLogaSuffixToSubject = true,
            DeletionPolicy = DeletionPolicy.AutoDelete,
            LogRetentionDays = 14,
            FirstRunCompleted = true,
        };

        await store.SaveAsync(settings, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        loaded.Should().BeEquivalentTo(settings);
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefaults_WhenFileDoesNotExist()
    {
        var store = new AppSettingsStore(NullLogger<AppSettingsStore>.Instance, _tempFilePath);

        var loaded = await store.LoadAsync(CancellationToken.None);

        loaded.FirstRunCompleted.Should().BeFalse();
        loaded.SyncPastDays.Should().Be(30);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }
}
