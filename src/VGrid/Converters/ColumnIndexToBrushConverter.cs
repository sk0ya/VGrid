using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VGrid.Converters;

/// <summary>
/// Converts column index and cursor column position to determine column header background color
/// </summary>
public class ColumnIndexToBrushConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush HighlightBrush = new SolidColorBrush(Color.FromRgb(200, 230, 245));
    private static readonly SolidColorBrush DefaultBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240));

    static ColumnIndexToBrushConverter()
    {
        HighlightBrush.Freeze();
        DefaultBrush.Freeze();
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] == null || values[1] == null)
            return DefaultBrush;

        // values[0]: Column.DisplayIndex
        // values[1]: CursorPosition.Column

        try
        {
            int columnIndex = System.Convert.ToInt32(values[0]);
            int cursorColumn = System.Convert.ToInt32(values[1]);

            return columnIndex == cursorColumn ? HighlightBrush : DefaultBrush;
        }
        catch
        {
            return DefaultBrush;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
