using System.Windows;
using LogaOutlookSync.App.ViewModels;
using LogaOutlookSync.App.Views;

namespace LogaOutlookSync.App;

/// <summary>Hauptfenster mit den vier Bereichen Dashboard, Synchronisation, Einstellungen und Protokoll.</summary>
public partial class MainWindow : Window
{
    public MainWindow(
        DashboardViewModel dashboardViewModel,
        SyncViewModel syncViewModel,
        SettingsViewModel settingsViewModel,
        LogViewModel logViewModel)
    {
        InitializeComponent();

        DashboardHost.Content = new DashboardView { DataContext = dashboardViewModel };
        SyncHost.Content = new SyncView { DataContext = syncViewModel };
        SettingsHost.Content = new SettingsView { DataContext = settingsViewModel };
        LogHost.Content = new LogView { DataContext = logViewModel };

        Loaded += async (_, _) =>
        {
            await settingsViewModel.LoadAsync(CancellationToken.None).ConfigureAwait(true);
            logViewModel.RefreshCommand.Execute(null);
        };
    }
}
