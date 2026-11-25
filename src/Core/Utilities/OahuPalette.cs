#nullable enable
using Bit.App.Contracts;
using Bit.App.Styles;
using Bit.Core.Abstractions;
using Bit.Core.Utilities;
using Oahu;

namespace Bit.App.Utilities
{
    /// <summary>
    /// The app theme's colors for native screens (docs/oahu/gaps.md, G11 and G16). One state shared by every contract
    /// that declares a <c>Palette</c>, read from the MAUI theme resources and set again when the app's theme changes
    /// ("updatedTheme", sent by the Appearance settings page and on a system appearance change).
    /// </summary>
    public static class OahuPalette
    {
        private static StateSource<AppPalette>? _current;

        public static IOahuState<AppPalette> Current
        {
            get
            {
                if (_current == null)
                {
                    _current = new StateSource<AppPalette>(Read());
                    ServiceContainer.Resolve<IBroadcasterService>("broadcasterService").Subscribe(nameof(OahuPalette), message =>
                    {
                        if (message.Command == ThemeManager.UPDATED_THEME_MESSAGE_KEY)
                        {
                            // The theme switch has applied the resources by the time the message is sent; read them on
                            // the main thread, where OAHU states may be set.
                            MainThread.BeginInvokeOnMainThread(Refresh);
                        }
                    });
                }
                return _current;
            }
        }

        /// <summary>Reads the theme resources again (after a theme change).</summary>
        public static void Refresh() => _current?.Set(Read());

        private static AppPalette Read()
        {
            long C(string key)
            {
                try
                {
                    var c = ThemeManager.GetResourceColor(key);
                    return ((long)Math.Round(c.Alpha * 255) << 24) | ((long)Math.Round(c.Red * 255) << 16)
                        | ((long)Math.Round(c.Green * 255) << 8) | (long)Math.Round(c.Blue * 255);
                }
                catch
                {
                    return 0xFF808080;
                }
            }

            string name;
            try
            {
                name = ThemeManager.Resources()?.MergedDictionaries.FirstOrDefault(d => d is IThemeResourceDictionary)
                    ?.GetType().Name ?? "Light";
            }
            catch
            {
                name = "Light";
            }

            return new AppPalette(
                Name: name,
                IsDark: !ThemeManager.UsingLightTheme,
                Background: C("BackgroundColor"),
                ListHeaderBackground: C("ListHeaderBackgroundColor"),
                ListHeaderText: C("ListHeaderTextColor"),
                Text: C("TextColor"),
                Muted: C("MutedColor"),
                Primary: C("PrimaryColor"),
                Danger: C("DangerColor"),
                Separator: C("SeparatorColor"),
                PasswordNumber: C("PasswordNumberColor"),
                PasswordSpecial: C("PasswordSpecialColor"),
                BoxHeaderText: C("BoxHeaderTextColor"));
        }
    }
}
