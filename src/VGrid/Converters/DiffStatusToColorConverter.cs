using System.Globalization;
using System.Windows;
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
            var resourceKey = status switch
            {
                DiffStatus.Unchanged => "DiffUnchangedBrush",
                DiffStatus.Modified => "DiffModifiedBrush",
                DiffStatus.Added => "DiffAddedBrush",
                DiffStatus.Deleted => "DiffDeletedBrush",
                _ => "DiffUnchangedBrush"
            };

            if (Application.Current?.Resources[resourceKey] is SolidColorBrush brush)
            {
                return brush;
            }

            // Fallback to default colors if resource not found
            return status switch
            {
                DiffStatus.Unchanged => new SolidColorBrush(Colors.White),
                DiffStatus.Modified => new SolidColorBrush(Color.FromRgb(255, 250, 205)),
                DiffStatus.Added => new SolidColorBrush(Color.FromRgb(230, 255, 230)),
                DiffStatus.Deleted => new SolidColorBrush(Color.FromRgb(255, 230, 230)),
                _ => new SolidColorBrush(Colors.White)
            };
        }

        return Application.Current?.Resources["DiffUnchangedBrush"] as SolidColorBrush
               ?? new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
