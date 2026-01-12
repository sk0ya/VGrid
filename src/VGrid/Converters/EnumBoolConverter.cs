using System.Globalization;
using System.Windows.Data;

namespace VGrid.Converters;

/// <summary>
/// Converts enum value to boolean for RadioButton binding
/// </summary>
public class EnumBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        return value.ToString() == parameter.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter != null)
        {
            return Enum.Parse(targetType, parameter.ToString()!);
        }

        return System.Windows.Data.Binding.DoNothing;
    }
}
