using System.Windows;
using LogaOutlookSync.App.ViewModels;

namespace LogaOutlookSync.App.Views;

/// <summary>
/// Einrichtungsassistent beim ersten Start. Verwendet dieselbe <see cref="SettingsView"/> wie
/// der Einstellungen-Tab der Hauptanwendung, ergänzt um "Fertig"/"Abbrechen".
/// </summary>
public partial class SetupWizardWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SetupWizardWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        SettingsHost.DataContext = viewModel;
    }

    private async void OnFinishClicked(object sender, RoutedEventArgs e)
    {
        var success = await _viewModel.SaveInternalAsync().ConfigureAwait(true);
        if (success)
        {
            DialogResult = true;
            Close();
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
