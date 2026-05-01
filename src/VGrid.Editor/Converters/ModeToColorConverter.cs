using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using VGrid.VimEngine;

namespace VGrid.Converters;

/// <summary>
/// Converts VimMode to a color for the status bar
/// </summary>
public class ModeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is VimMode mode)
        {
            return mode switch
            {
                VimMode.Normal => new SolidColorBrush(Colors.CornflowerBlue),
                VimMode.Insert => new SolidColorBrush(Colors.LimeGreen),
                VimMode.Visual => new SolidColorBrush(Colors.DodgerBlue),
                VimMode.Command => new SolidColorBrush(Colors.MediumPurple),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }

        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
