using System.Windows;
using WorklogManager.ViewModels;

namespace WorklogManager.Views;

/// <summary>
/// Code-behind for SettingsWindow.
/// Handles PasswordBox bindings (WPF PasswordBox.Password cannot bind in XAML for security reasons)
/// and wires Save/Cancel dialogs.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // PasswordBox doesn't support two-way binding — push changes to VM immediately
        // so that Test buttons can use the freshly typed token without requiring Save first.
        JiraPasswordBox.PasswordChanged += (_, _) =>
            _viewModel.JiraApiToken = JiraPasswordBox.Password;
        TempoPasswordBox.PasswordChanged += (_, _) =>
            _viewModel.TempoApiToken = TempoPasswordBox.Password;

        // Seed PasswordBoxes from VM (e.g. when token was pre-populated from an env var)
        if (!string.IsNullOrEmpty(_viewModel.JiraApiToken))
            JiraPasswordBox.Password = _viewModel.JiraApiToken;
        if (!string.IsNullOrEmpty(_viewModel.TempoApiToken))
            TempoPasswordBox.Password = _viewModel.TempoApiToken;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Push PasswordBox values into the ViewModel before saving
        if (!string.IsNullOrEmpty(JiraPasswordBox.Password))
            _viewModel.JiraApiToken = JiraPasswordBox.Password;

        if (!string.IsNullOrEmpty(TempoPasswordBox.Password))
            _viewModel.TempoApiToken = TempoPasswordBox.Password;

        if (_viewModel.SaveCommand.CanExecute(null))
            _viewModel.SaveCommand.Execute(null);

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
