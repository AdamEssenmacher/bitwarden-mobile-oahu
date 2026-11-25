using Android.Content.Res;
using Bit.App.Utilities;
using Microsoft.Maui.Platform;
using Color = Android.Graphics.Color;

namespace Bit.App.Droid.Utilities
{
    public class ThemeHelpers
    {
        public static bool LightTheme = true;
        
        public static Color PrimaryColor
        {
            get => ThemeManager.GetResourceColor("PrimaryColor").ToPlatform();
        }
        public static Color MutedColor
        {
            get => ThemeManager.GetResourceColor("MutedColor").ToPlatform();
        }
        public static Color BackgroundColor
        {
            get => ThemeManager.GetResourceColor("BackgroundColor").ToPlatform();
        }
        public static Color NavBarBackgroundColor
        {
            get => ThemeManager.GetResourceColor("NavigationBarBackgroundColor").ToPlatform();
        }
        public static Color FabColor
        {
            get => ThemeManager.GetResourceColor("FabColor").ToPlatform();
        }
        public static Color SwitchOnColor
        {
            get => ThemeManager.GetResourceColor("SwitchOnColor").ToPlatform();
        }
        public static Color SwitchThumbColor
        {
            get => ThemeManager.GetResourceColor("SwitchThumbColor").ToPlatform();
        }
        public static Color TextColor
        {
            get => ThemeManager.GetResourceColor("TextColor").ToPlatform();
        }
        
        public static void SetAppearance(string theme, bool osDarkModeEnabled)
        {
            SetThemeVariables(theme, osDarkModeEnabled);
        }
        
        public static int GetDialogTheme()
        {
            if (LightTheme)
            {
                return Android.Resource.Style.ThemeMaterialLightDialog;
            }
            return Android.Resource.Style.ThemeMaterialDialog;
        }
        
        private static void SetThemeVariables(string theme, bool osDarkModeEnabled)
        {
            if (string.IsNullOrWhiteSpace(theme) && osDarkModeEnabled)
            {
                theme = ThemeManager.Dark;
            }

            if (theme == ThemeManager.Dark || theme == ThemeManager.Black || theme == ThemeManager.Nord || theme == ThemeManager.SolarizedDark)
            {
                LightTheme = false;
            }
            else
            {
                LightTheme = true;
            }
        }

        internal static ColorStateList GetStateFocusedColors()
        {
            var states = new[]
            {
                new[] { Android.Resource.Attribute.StateFocused }, // focused
                new[] { -Android.Resource.Attribute.StateFocused }, // unfocused
            };
            var colors = new int[]
            {
                PrimaryColor,
                MutedColor
            };
            return new ColorStateList(states, colors);
        }
    }
}
