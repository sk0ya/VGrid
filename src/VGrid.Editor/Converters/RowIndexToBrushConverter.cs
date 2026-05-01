using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace VGrid.Converters;

/// <summary>
/// Converts row index and cursor row position to determine row header background color
/// </summary>
public class RowIndexToBrushConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush FallbackHighlightBrush = new SolidColorBrush(Color.FromRgb(200, 230, 245));
    private static readonly SolidColorBrush FallbackDefaultBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240));

    static RowIndexToBrushConverter()
    {
        FallbackHighlightBrush.Freeze();
        FallbackDefaultBrush.Freeze();
    }

    private Brush GetHighlightBrush()
    {
        return Application.Current?.Resources["DataGridCurrentRowHeaderBrush"] as Brush ?? FallbackHighlightBrush;
    }

    private Brush GetDefaultBrush()
    {
        return Application.Current?.Resources["DataGridHeaderBackgroundBrush"] as Brush ?? FallbackDefaultBrush;
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] == null || values[1] == null)
            return GetDefaultBrush();

        // values[0]: AlternationIndex (row index)
        // values[1]: CursorPosition.Row

        try
        {
            int rowIndex = System.Convert.ToInt32(values[0]);
            int cursorRow = System.Convert.ToInt32(values[1]);

            return rowIndex == cursorRow ? GetHighlightBrush() : GetDefaultBrush();
        }
        catch
        {
            return GetDefaultBrush();
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
