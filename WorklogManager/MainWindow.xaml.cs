using System.Windows;
using System.Windows.Controls;
using WorklogManager.ViewModels;

namespace WorklogManager;

/// <summary>
/// Code-behind for MainWindow.
/// Kept minimal — all logic lives in MainViewModel.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>Auto-scrolls the log TextBox to the bottom whenever new text arrives.</summary>
    private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb)
            tb.ScrollToEnd();
    }
}
