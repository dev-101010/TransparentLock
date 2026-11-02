using System;
using System.Windows;
using Microsoft.Win32;

namespace TransparentLock
{
    /// <summary>
    /// Follows the Windows "app mode" setting (light / dark) and swaps the color palette at runtime.
    /// </summary>
    public static class ThemeManager
    {
        private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        private static ResourceDictionary? _palette;

        public static bool IsLight { get; private set; }

        /// <summary>Raised on the UI thread after the palette has changed.</summary>
        public static event Action? ThemeChanged;

        public static void Initialize(Application app)
        {
            Apply(app, ReadSystemUsesLightTheme());

            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (e.Category != UserPreferenceCategory.General)
                    return;

                app.Dispatcher.BeginInvoke(new Action(() =>
                {
                    bool light = ReadSystemUsesLightTheme();
                    if (light != IsLight)
                        Apply(app, light);
                }));
            };
        }

        /// <summary>Caption color for the DWM title bar as COLORREF (0x00BBGGRR).</summary>
        public static int CaptionColor => IsLight ? 0x00F3F3F3 : 0x00202020;

        private static void Apply(Application app, bool light)
        {
            IsLight = light;

            var palette = new ResourceDictionary
            {
                Source = new Uri(light ? "Themes/Colors.Light.xaml" : "Themes/Colors.Dark.xaml", UriKind.Relative)
            };

            var dictionaries = app.Resources.MergedDictionaries;
            if (_palette != null)
                dictionaries.Remove(_palette);
            dictionaries.Insert(0, palette);
            _palette = palette;

            ThemeChanged?.Invoke();
        }

        private static bool ReadSystemUsesLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
                return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
