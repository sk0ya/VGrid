using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VGrid.Converters;

/// <summary>
/// Converts row index and cursor row position to determine row header background color
/// </summary>
public class RowIndexToBrushConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush HighlightBrush = new SolidColorBrush(Color.FromRgb(200, 230, 245));
    private static readonly SolidColorBrush DefaultBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240));

    static RowIndexToBrushConverter()
    {
        HighlightBrush.Freeze();
        DefaultBrush.Freeze();
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] == null || values[1] == null)
            return DefaultBrush;

        // values[0]: AlternationIndex (row index)
        // values[1]: CursorPosition.Row

        try
        {
            int rowIndex = System.Convert.ToInt32(values[0]);
            int cursorRow = System.Convert.ToInt32(values[1]);

            return rowIndex == cursorRow ? HighlightBrush : DefaultBrush;
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
