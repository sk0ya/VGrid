using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using VGrid.Models;

namespace VGrid.Converters;

/// <summary>
/// Converts DiffStatus to background color brush
/// </summary>
public class DiffStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DiffStatus status)
        {
            return status switch
            {
                DiffStatus.Unchanged => new SolidColorBrush(Colors.White),
                DiffStatus.Modified => new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 250, 205)),  // Light yellow
                DiffStatus.Added => new SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 255, 230)),     // Light green
                DiffStatus.Deleted => new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 230, 230)),   // Light red
                _ => new SolidColorBrush(Colors.White)
            };
        }

        return new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
