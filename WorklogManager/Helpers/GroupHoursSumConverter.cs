using System.Collections;
using System.Globalization;
using System.Windows.Data;
using WorklogManager.Models;

namespace WorklogManager.Helpers;

/// <summary>
/// Sums RoundedTimeSpentSeconds across the items of a CollectionViewGroup
/// and formats the total as "Xh YYm".
/// </summary>
public class GroupHoursSumConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not IEnumerable items) return string.Empty;

        int totalSeconds = 0;
        foreach (var item in items)
            if (item is WorklogRecord r) totalSeconds += r.RoundedTimeSpentSeconds;

        int totalMinutes = totalSeconds / 60;
        return $"{totalMinutes / 60}h {totalMinutes % 60:D2}m";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
