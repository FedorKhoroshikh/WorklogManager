using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WorklogManager.Helpers;

/// <summary>
/// Returns green for a successful validation summary (starts with ✔),
/// orange for a partial/warning summary (starts with ⚠).
/// </summary>
[ValueConversion(typeof(string), typeof(Brush))]
public class ValidationSummaryColorConverter : IValueConverter
{
    public static readonly ValidationSummaryColorConverter Instance = new();

    private static readonly SolidColorBrush GreenBrush  = new(Color.FromRgb(0x1A, 0x80, 0x3A));
    private static readonly SolidColorBrush OrangeBrush = new(Color.FromRgb(0xCC, 0x77, 0x00));
    private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(0x33, 0x33, 0x33));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string ?? string.Empty;
        if (text.StartsWith("✔")) return GreenBrush;
        if (text.StartsWith("⚠")) return OrangeBrush;
        return DefaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
