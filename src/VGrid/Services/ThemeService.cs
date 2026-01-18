using System;
using System.Windows;

namespace VGrid.Services
{
    public enum ThemeType
    {
        Light,
        Dark
    }

    public class ThemeService
    {
        private static ThemeService? _instance;
        public static ThemeService Instance => _instance ??= new ThemeService();

        private ThemeType _currentTheme = ThemeType.Light;

        public ThemeType CurrentTheme
        {
            get => _currentTheme;
            set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    ApplyTheme(value);
                    ThemeChanged?.Invoke(this, value);
                }
            }
        }

        public event EventHandler<ThemeType>? ThemeChanged;

        private ThemeService()
        {
        }

        public void ApplyTheme(ThemeType theme)
        {
            var app = Application.Current;
            if (app == null) return;

            var themePath = theme switch
            {
                ThemeType.Light => "Themes/LightTheme.xaml",
                ThemeType.Dark => "Themes/DarkTheme.xaml",
                _ => "Themes/LightTheme.xaml"
            };

            var themeDict = new ResourceDictionary
            {
                Source = new Uri(themePath, UriKind.Relative)
            };

            // Remove old theme dictionaries
            var toRemove = new System.Collections.Generic.List<ResourceDictionary>();
            foreach (var dict in app.Resources.MergedDictionaries)
            {
                if (dict.Source != null &&
                    (dict.Source.OriginalString.Contains("LightTheme") ||
                     dict.Source.OriginalString.Contains("DarkTheme")))
                {
                    toRemove.Add(dict);
                }
            }

            foreach (var dict in toRemove)
            {
                app.Resources.MergedDictionaries.Remove(dict);
            }

            // Add new theme
            app.Resources.MergedDictionaries.Add(themeDict);

            _currentTheme = theme;
        }

        public void ToggleTheme()
        {
            CurrentTheme = CurrentTheme == ThemeType.Light ? ThemeType.Dark : ThemeType.Light;
        }
    }
}
