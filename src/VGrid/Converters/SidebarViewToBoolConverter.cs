using System;
using System.Globalization;
using System.Windows.Data;
using VGrid.Models;

namespace VGrid.Converters;

/// <summary>
/// SidebarViewと特定の値を比較してboolを返すコンバーター
/// </summary>
public class SidebarViewToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SidebarView currentView && parameter is string paramString)
        {
            if (Enum.TryParse<SidebarView>(paramString, out var targetView))
            {
                return currentView == targetView;
            }
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected && parameter is string paramString)
        {
            if (Enum.TryParse<SidebarView>(paramString, out var targetView))
            {
                return targetView;
            }
        }
        return Binding.DoNothing;
    }
}
