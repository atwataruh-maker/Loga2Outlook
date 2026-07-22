using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogaOutlookSync.Calendar;
using LogaOutlookSync.Calendar.Exceptions;
using LogaOutlookSync.Calendar.Graph;
using LogaOutlookSync.Calendar.OutlookCom;
using LogaOutlookSync.Domain;
using LogaOutlookSync.Infrastructure;
using LogaOutlookSync.Loga;
using LogaOutlookSync.Loga.Configuration;
using LogaOutlookSync.Security;
using Microsoft.Extensions.Logging;

namespace LogaOutlookSync.App.ViewModels;

/// <summary>
/// Bildet den Einrichtungsassistenten und den Einstellungen-Tab ab (dieselbe Ansicht wird an
/// beiden Stellen verwendet). Speichert nicht sensible Einstellungen über
/// <see cref="AppSettingsStore"/> und das LOGA-Passwort ausschließlich über
/// <see cref="ICredentialStore"/>.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettingsStore _settingsStore;
    private readonly ICredentialStore _credentialStore;
    private readonly ILoggerFactory _loggerFactory;

    [ObservableProperty]
    private string logaBaseUrl = "https://dedalus.pi-asp.de/loga3/private/layout?action=afterlogin";

    [ObservableProperty]
    private string logaUserName = string.Empty;

    /// <summary>
    /// Neues Passwort, nur befüllt, wenn der Benutzer es ändern möchte. Wird nie aus dem
    /// sicheren Speicher vorbelegt und nach dem Speichern sofort wieder geleert.
    /// </summary>
    [ObservableProperty]
    private string logaPassword = string.Empty;

    [ObservableProperty]
    private CalendarProviderKind calendarProvider = CalendarProviderKind.Automatic;

    [ObservableProperty]
    private string? targetCalendarDisplayName;

    [ObservableProperty]
    private string? targetCalendarId;

    [ObservableProperty]
    private string? graphClientId;

    [ObservableProperty]
    private string graphTenantId = "organizations";

    [ObservableProperty]
    private bool browserVisible = true;

    [ObservableProperty]
    private int syncPastDays = 30;

    [ObservableProperty]
    private int syncFutureMonths = 18;

    [ObservableProperty]
    private string? subjectPrefix;

    [ObservableProperty]
    private bool appendLogaSuffixToSubject;

    [ObservableProperty]
    private string vacationSubject = "Urlaub";

    [ObservableProperty]
    private string flexTimeSubject = "Gleitzeit";

    [ObservableProperty]
    private string vacationCategory = "LOGA Urlaub";

    [ObservableProperty]
    private string flexTimeCategory = "LOGA Gleitzeit";

    [ObservableProperty]
    private DeletionPolicy deletionPolicy = DeletionPolicy.ConfirmBeforeDelete;

    [ObservableProperty]
    private int logRetentionDays = 30;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public IReadOnlyList<CalendarProviderKind> CalendarProviderOptions { get; } = Enum.GetValues<CalendarProviderKind>();

    public IReadOnlyList<DeletionPolicy> DeletionPolicyOptions { get; } = Enum.GetValues<DeletionPolicy>();

    public ObservableCollection<CalendarDescriptor> AvailableCalendars { get; } = new();

    public SettingsViewModel(AppSettingsStore settingsStore, ICredentialStore credentialStore, ILoggerFactory loggerFactory)
    {
        _settingsStore = settingsStore;
        _credentialStore = credentialStore;
        _loggerFactory = loggerFactory;
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsStore.LoadAsync(cancellationToken).ConfigureAwait(true);
        ApplyFrom(settings);

        var credential = await _credentialStore.LoadAsync(cancellationToken).ConfigureAwait(true);
        if (credential is not null)
        {
            LogaUserName = credential.UserName;
        }
    }

    private void ApplyFrom(AppSettings settings)
    {
        LogaBaseUrl = settings.LogaBaseUrl;
        LogaUserName = settings.LogaUserName;
        CalendarProvider = settings.CalendarProvider;
        TargetCalendarDisplayName = settings.TargetCalendarDisplayName;
        TargetCalendarId = settings.TargetCalendarId;
        GraphClientId = settings.GraphClientId;
        GraphTenantId = settings.GraphTenantId;
        BrowserVisible = settings.BrowserVisible;
        SyncPastDays = settings.SyncPastDays;
        SyncFutureMonths = settings.SyncFutureMonths;
        SubjectPrefix = settings.SubjectPrefix;
        AppendLogaSuffixToSubject = settings.AppendLogaSuffixToSubject;
        VacationSubject = settings.VacationSubject;
        FlexTimeSubject = settings.FlexTimeSubject;
        VacationCategory = settings.VacationCategory;
        FlexTimeCategory = settings.FlexTimeCategory;
        DeletionPolicy = settings.DeletionPolicy;
        LogRetentionDays = settings.LogRetentionDays;
    }

    private AppSettings BuildSettings(bool firstRunCompleted) => new()
    {
        LogaBaseUrl = LogaBaseUrl,
        LogaUserName = LogaUserName,
        CalendarProvider = CalendarProvider,
        TargetCalendarDisplayName = TargetCalendarDisplayName,
        TargetCalendarId = TargetCalendarId,
        GraphClientId = GraphClientId,
        GraphTenantId = GraphTenantId,
        BrowserVisible = BrowserVisible,
        SyncPastDays = SyncPastDays,
        SyncFutureMonths = SyncFutureMonths,
        SubjectPrefix = SubjectPrefix,
        AppendLogaSuffixToSubject = AppendLogaSuffixToSubject,
        VacationSubject = VacationSubject,
        FlexTimeSubject = FlexTimeSubject,
        VacationCategory = VacationCategory,
        FlexTimeCategory = FlexTimeCategory,
        DeletionPolicy = DeletionPolicy,
        LogRetentionDays = LogRetentionDays,
        FirstRunCompleted = firstRunCompleted,
    };

    /// <summary>Speichert die Einstellungen. Wird auch vom Einrichtungsassistenten aufgerufen.</summary>
    public async Task<bool> SaveInternalAsync()
    {
        IsBusy = true;
        try
        {
            if (!string.IsNullOrEmpty(LogaPassword))
            {
                await _credentialStore.SaveAsync(LogaUserName, LogaPassword, CancellationToken.None).ConfigureAwait(true);
                LogaPassword = string.Empty;
            }

            var settings = BuildSettings(firstRunCompleted: true);
            await _settingsStore.SaveAsync(settings, CancellationToken.None).ConfigureAwait(true);
            StatusMessage = "Einstellungen gespeichert. Änderungen werden nach einem Neustart der Anwendung wirksam.";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Speichern fehlgeschlagen: {ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task SaveAsync() => SaveInternalAsync();

    [RelayCommand]
    private async Task TestLogaLoginAsync()
    {
        IsBusy = true;
        StatusMessage = "LOGA-Anmeldung wird getestet...";
        try
        {
            if (!string.IsNullOrEmpty(LogaPassword))
            {
                await _credentialStore.SaveAsync(LogaUserName, LogaPassword, CancellationToken.None).ConfigureAwait(true);
            }

            var draftSettings = BuildSettings(firstRunCompleted: true);
            var client = new PlaywrightLogaClient(
                _credentialStore,
                draftSettings,
                new LogaSelectorsProvider(_loggerFactory.CreateLogger<LogaSelectorsProvider>()),
                new DebugArtifactCollector(_loggerFactory.CreateLogger<DebugArtifactCollector>()),
                _loggerFactory.CreateLogger<PlaywrightLogaClient>());

            var result = await client.TestLoginAsync(CancellationToken.None).ConfigureAwait(true);
            StatusMessage = result.Succeeded
                ? "LOGA-Anmeldung erfolgreich."
                : $"LOGA-Anmeldung fehlgeschlagen: {result.ErrorMessage}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TestOutlookConnectionAsync()
    {
        IsBusy = true;
        StatusMessage = "Outlook-Verbindung wird getestet...";
        try
        {
            var provider = CreateDraftCalendarProvider();
            var now = DateTimeOffset.Now;
            await provider.GetManagedEntriesAsync(now.AddDays(-1), now.AddDays(1), CancellationToken.None).ConfigureAwait(true);
            StatusMessage = "Outlook-Verbindung erfolgreich.";
        }
        catch (CalendarProviderException ex)
        {
            StatusMessage = $"Outlook-Verbindung fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ShowAvailableCalendarsAsync()
    {
        IsBusy = true;
        StatusMessage = "Verfügbare Kalender werden geladen...";
        try
        {
            var provider = CreateDraftCalendarProvider();
            var calendars = await provider.ListCalendarsAsync(CancellationToken.None).ConfigureAwait(true);

            AvailableCalendars.Clear();
            foreach (var calendar in calendars)
            {
                AvailableCalendars.Add(calendar);
            }

            StatusMessage = $"{calendars.Count} Kalender gefunden.";
        }
        catch (CalendarProviderException ex)
        {
            StatusMessage = $"Kalender konnten nicht geladen werden: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TestSelectedCalendarAsync()
    {
        if (string.IsNullOrEmpty(TargetCalendarId))
        {
            StatusMessage = "Bitte zuerst einen Kalender aus der Liste auswählen.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Kalender '{TargetCalendarDisplayName}' wird getestet...";
        try
        {
            var provider = CreateDraftCalendarProvider();
            var now = DateTimeOffset.Now;
            var entries = await provider.GetManagedEntriesAsync(now.AddDays(-1), now.AddDays(1), CancellationToken.None).ConfigureAwait(true);
            StatusMessage = $"Kalender '{TargetCalendarDisplayName}' erfolgreich getestet ({entries.Count} Termine in den letzten/nächsten 24h).";
        }
        catch (CalendarProviderException ex)
        {
            StatusMessage = $"Kalendertest fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ICalendarProvider CreateDraftCalendarProvider()
    {
        var draftSettings = BuildSettings(firstRunCompleted: true);
        var factory = new CalendarProviderFactory(
            () => new MicrosoftGraphCalendarProvider(draftSettings, _loggerFactory.CreateLogger<MicrosoftGraphCalendarProvider>()),
            () => new OutlookComCalendarProvider(draftSettings, _loggerFactory.CreateLogger<OutlookComCalendarProvider>()),
            draftSettings,
            _loggerFactory.CreateLogger<CalendarProviderFactory>());

        return factory.Create();
    }
}
