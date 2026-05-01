using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace VGrid.Converters;

/// <summary>
/// Converts column index and cursor column position to determine column header background color
/// </summary>
public class ColumnIndexToBrushConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush FallbackHighlightBrush = new SolidColorBrush(Color.FromRgb(200, 230, 245));
    private static readonly SolidColorBrush FallbackDefaultBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240));

    static ColumnIndexToBrushConverter()
    {
        FallbackHighlightBrush.Freeze();
        FallbackDefaultBrush.Freeze();
    }

    private Brush GetHighlightBrush()
    {
        return Application.Current?.Resources["DataGridCurrentColumnHeaderBrush"] as Brush ?? FallbackHighlightBrush;
    }

    private Brush GetDefaultBrush()
    {
        return Application.Current?.Resources["DataGridHeaderBackgroundBrush"] as Brush ?? FallbackDefaultBrush;
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] == null || values[1] == null)
            return GetDefaultBrush();

        // values[0]: Column.DisplayIndex
        // values[1]: CursorPosition.Column

        try
        {
            int columnIndex = System.Convert.ToInt32(values[0]);
            int cursorColumn = System.Convert.ToInt32(values[1]);

            return columnIndex == cursorColumn ? GetHighlightBrush() : GetDefaultBrush();
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
