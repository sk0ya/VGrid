using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace VGrid.Converters;

/// <summary>
/// Converts row index and cursor row position to determine row header background color.
/// Pass the target element as an optional third binding (RelativeSource Self) so theme brushes
/// are resolved through the element tree (TryFindResource); this lets hosts scope the theme
/// dictionary to a control instead of Application.Current.Resources, which remains the fallback.
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

    private static Brush FindBrush(FrameworkElement? element, string key, Brush fallback)
    {
        return element?.TryFindResource(key) as Brush
            ?? Application.Current?.Resources[key] as Brush
            ?? fallback;
    }

    private static Brush GetHighlightBrush(FrameworkElement? element)
        => FindBrush(element, "DataGridCurrentRowHeaderBrush", FallbackHighlightBrush);

    private static Brush GetDefaultBrush(FrameworkElement? element)
        => FindBrush(element, "DataGridHeaderBackgroundBrush", FallbackDefaultBrush);

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // values[0]: AlternationIndex (row index)
        // values[1]: CursorPosition.Row
        // values[2]: (optional) the header element, for element-tree resource lookup

        var element = values.Length > 2 ? values[2] as FrameworkElement : null;

        if (values.Length < 2 || values[0] == null || values[1] == null)
            return GetDefaultBrush(element);

        try
        {
            int rowIndex = System.Convert.ToInt32(values[0]);
            int cursorRow = System.Convert.ToInt32(values[1]);

            return rowIndex == cursorRow ? GetHighlightBrush(element) : GetDefaultBrush(element);
        }
        catch
        {
            return GetDefaultBrush(element);
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
