using System.Globalization;
using System.Windows.Data;

namespace VGrid.Converters;

/// <summary>
/// Converter that checks if two strings are equal
/// </summary>
public class StringEqualityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return false;

        var str1 = values[0] as string;
        var str2 = values[1] as string;

        return string.Equals(str1, str2, StringComparison.Ordinal);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
