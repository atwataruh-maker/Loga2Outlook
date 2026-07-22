using System.Windows;
using LogaOutlookSync.App.Services;
using LogaOutlookSync.App.ViewModels;
using LogaOutlookSync.App.Views;
using LogaOutlookSync.Application;
using LogaOutlookSync.Calendar;
using LogaOutlookSync.Calendar.Graph;
using LogaOutlookSync.Calendar.OutlookCom;
using LogaOutlookSync.Infrastructure;
using LogaOutlookSync.Infrastructure.Logging;
using LogaOutlookSync.Loga;
using LogaOutlookSync.Loga.Configuration;
using LogaOutlookSync.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace LogaOutlookSync.App;

/// <summary>
/// Kompositionswurzel der Anwendung. Lädt zuerst die Einstellungen, zeigt bei Erststart den
/// Einrichtungsassistenten und baut anschließend den Dependency-Injection-Container für die
/// Hauptanwendung auf.
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private Serilog.ILogger? _rootLogger;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppPaths.EnsureDirectoriesExist();

        using var bootstrapLoggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
        var bootstrapSettingsStore = new AppSettingsStore(bootstrapLoggerFactory.CreateLogger<AppSettingsStore>());
        var settings = await bootstrapSettingsStore.LoadAsync(CancellationToken.None).ConfigureAwait(true);

        _rootLogger = LoggingSetup.CreateLogger(settings.LogRetentionDays);
        Log.Logger = _rootLogger;
        _rootLogger.Information("Anwendung gestartet.");

        if (!settings.FirstRunCompleted)
        {
            using var wizardLoggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(_rootLogger, dispose: false));
            var wizardCredentialStore = new FallbackCredentialStore(
                new WindowsCredentialManagerStore(),
                new DpapiCredentialStore(),
                wizardLoggerFactory.CreateLogger<FallbackCredentialStore>());

            var wizardViewModel = new SettingsViewModel(bootstrapSettingsStore, wizardCredentialStore, wizardLoggerFactory);
            await wizardViewModel.LoadAsync(CancellationToken.None).ConfigureAwait(true);

            var wizardWindow = new SetupWizardWindow(wizardViewModel);
            var wizardResult = wizardWindow.ShowDialog();

            if (wizardResult != true)
            {
                _rootLogger.Information("Einrichtungsassistent abgebrochen, Anwendung wird beendet.");
                Shutdown();
                return;
            }

            settings = await bootstrapSettingsStore.LoadAsync(CancellationToken.None).ConfigureAwait(true);
        }

        _host = BuildHost(settings, _rootLogger);
        await _host.StartAsync().ConfigureAwait(true);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync().ConfigureAwait(true);
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static IHost BuildHost(AppSettings settings, Serilog.ILogger rootLogger)
    {
        return Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddSerilog(rootLogger, dispose: true);
            })
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton(settings);

                // Sichere Zugangsdatenverwaltung
                services.AddSingleton<WindowsCredentialManagerStore>();
                services.AddSingleton<DpapiCredentialStore>();
                services.AddSingleton<ICredentialStore, FallbackCredentialStore>();

                // Konfiguration
                services.AddSingleton<AppSettingsStore>();
                services.AddSingleton<DiagnosticsPackageExporter>();

                // LOGA-Automatisierung
                services.AddSingleton<LogaSelectorsProvider>();
                services.AddSingleton<DebugArtifactCollector>();
                services.AddSingleton<ILogaClient, PlaywrightLogaClient>();

                // Kalenderanbindung
                services.AddSingleton<MicrosoftGraphCalendarProvider>();
                services.AddSingleton<OutlookComCalendarProvider>();
                services.AddSingleton<Func<MicrosoftGraphCalendarProvider>>(sp => () => sp.GetRequiredService<MicrosoftGraphCalendarProvider>());
                services.AddSingleton<Func<OutlookComCalendarProvider>>(sp => () => sp.GetRequiredService<OutlookComCalendarProvider>());
                services.AddSingleton<CalendarProviderFactory>();

                // Anwendungslogik
                services.AddSingleton<IClock, SystemClock>();
                services.AddSingleton<CalendarSyncItemFactory>();
                services.AddSingleton<SyncPlanner>();
                services.AddSingleton<SyncOrchestrator>();

                // UI
                services.AddSingleton<SyncStateService>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<SyncViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<LogViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }
}
