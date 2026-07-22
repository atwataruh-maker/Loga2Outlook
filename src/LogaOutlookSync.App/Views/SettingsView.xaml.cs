using System.Windows;
using System.Windows.Controls;
using LogaOutlookSync.App.ViewModels;

namespace LogaOutlookSync.App.Views;

/// <summary>
/// Codebehind für <see cref="SettingsView"/>. Enthält ausschließlich das Weiterreichen des
/// <see cref="PasswordBox"/>-Inhalts an das ViewModel, da WPF aus Sicherheitsgründen keine
/// direkte Datenbindung von <c>PasswordBox.Password</c> unterstützt.
/// </summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnLogaPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.LogaPassword = LogaPasswordBox.Password;
        }
    }
}
