using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using VGrid.Models;

namespace VGrid.Converters;

/// <summary>
/// SidebarViewと特定の値を比較してVisibilityを返すコンバーター
/// </summary>
public class SidebarViewToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SidebarView currentView && parameter is string paramString)
        {
            if (Enum.TryParse<SidebarView>(paramString, out var targetView))
            {
                return currentView == targetView ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
