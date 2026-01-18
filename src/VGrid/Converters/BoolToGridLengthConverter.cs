using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VGrid.Converters;

/// <summary>
/// boolをGridLengthに変換するコンバーター
/// trueの場合は指定された幅、falseの場合は0
/// </summary>
public class BoolToGridLengthConverter : IValueConverter
{
    public double OpenWidth { get; set; } = 250;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isOpen)
        {
            return isOpen ? new GridLength(OpenWidth, GridUnitType.Pixel) : new GridLength(0);
        }
        return new GridLength(OpenWidth, GridUnitType.Pixel);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is GridLength gridLength)
        {
            return gridLength.Value > 0;
        }
        return true;
    }
}
