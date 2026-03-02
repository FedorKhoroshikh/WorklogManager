using System.Windows;
using System.Windows.Controls;

namespace WorklogManager.Views;

/// <summary>
/// Simple date-range picker dialog for the TogglTrack API import.
/// No ViewModel — all state lives in the two DatePickers.
/// </summary>
public partial class DateRangeWindow : Window
{
    public DateOnly DateFrom { get; private set; }
    public DateOnly DateTo   { get; private set; }

    public DateRangeWindow()
    {
        InitializeComponent();

        // Default: first day of the current month → today
        var today = DateTime.Today;
        FromPicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
        ToPicker.SelectedDate   = today;
    }

    private void DatePicker_Changed(object sender, SelectionChangedEventArgs e) =>
        LoadButton.IsEnabled =
            FromPicker.SelectedDate.HasValue &&
            ToPicker.SelectedDate.HasValue &&
            FromPicker.SelectedDate.Value <= ToPicker.SelectedDate.Value;

    private void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        DateFrom = DateOnly.FromDateTime(FromPicker.SelectedDate!.Value);
        DateTo   = DateOnly.FromDateTime(ToPicker.SelectedDate!.Value);
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) =>
        DialogResult = false;
}
