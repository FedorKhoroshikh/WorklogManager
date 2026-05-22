using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        viewModel.GroupedViewRefreshRequested += (_, _) =>
        {
            var view = (CollectionViewSource)FindResource("RecordsView");
            view.View?.Refresh();
        };
    }

    /// <summary>Auto-scrolls the log TextBox to the bottom whenever new text arrives.</summary>
    private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb)
            tb.ScrollToEnd();
    }

    private void GroupByIssueKey_Toggled(object sender, RoutedEventArgs e)
    {
        var view = (CollectionViewSource)FindResource("RecordsView");
        view.GroupDescriptions.Clear();
        if (GroupByIssueKeyCheckBox.IsChecked == true)
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Models.WorklogRecord.IssueKey)));
    }
}
