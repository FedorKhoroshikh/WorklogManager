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

        // Default: today (or the previous working day before 16:00 — worklog usually
        // covers the previous day; skip Sat/Sun so Monday morning lands on Friday).
        var initial = DateTime.Today;
        if (DateTime.Now.Hour < 16)
        {
            initial = initial.AddDays(-1);
            while (initial.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                initial = initial.AddDays(-1);
        }
        FromPicker.SelectedDate = initial;
        ToPicker.SelectedDate   = initial;
    }

    private void DatePicker_Changed(object sender, SelectionChangedEventArgs e)
    {
        // If "To" was changed to a date earlier than "From", drag "From" along.
        if (ReferenceEquals(sender, ToPicker)
            && FromPicker.SelectedDate.HasValue
            && ToPicker.SelectedDate.HasValue
            && ToPicker.SelectedDate.Value < FromPicker.SelectedDate.Value)
        {
            FromPicker.SelectedDate = ToPicker.SelectedDate;
        }

        LoadButton.IsEnabled =
            FromPicker.SelectedDate.HasValue &&
            ToPicker.SelectedDate.HasValue &&
            FromPicker.SelectedDate.Value <= ToPicker.SelectedDate.Value;
    }

    private void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        DateFrom = DateOnly.FromDateTime(FromPicker.SelectedDate!.Value);
        DateTo   = DateOnly.FromDateTime(ToPicker.SelectedDate!.Value);
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) =>
        DialogResult = false;
}
