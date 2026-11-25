#if DEBUG
// DEBUG ONLY: a launch route that shows the OAHU folder pages without an account (docs/oahu/02-folders.md).
//
// iOS simulator: SIMCTL_CHILD_BW_OAHU_DEBUG=folders xcrun simctl launch <device> com.8bit.bitwarden
// Android:       adb shell am start -n com.x8bit.bitwarden/com.x8bit.bitwarden.MainActivity --es bwOahuDebug folders
//
// "folders" seeds four fake folders; "folders-empty" starts with none. The route replaces the registered
// folderService with FakeFolderService, so it must never run against a real account; it is never compiled into
// Release, and without the variable or extra nothing changes.
using Bit.Core.Abstractions;
using Bit.Core.Utilities;

namespace Bit.App.Pages.OahuDebug
{
    public static class OahuDebugRoute
    {
        private static bool _shown;

        public static string Requested
        {
            get
            {
                var value = Environment.GetEnvironmentVariable("BW_OAHU_DEBUG");
#if ANDROID
                value ??= Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Intent?.GetStringExtra("bwOahuDebug");
#endif
                return value;
            }
        }

        /// <summary>Shows the debug host page instead of the login home page when the route was requested.</summary>
        public static bool TryShow()
        {
            var requested = Requested;
            if (requested is not ("folders" or "folders-empty"))
            {
                return false;
            }
            if (!_shown)
            {
                _shown = true;
                ServiceContainer.RegisteredServices["folderService"] =
                    new FakeFolderService(seed: requested == "folders");
            }
            MainThread.BeginInvokeOnMainThread(() => Bit.App.App.MainPage = new NavigationPage(new OahuDebugHostPage()));
            return true;
        }
    }

    /// <summary>Stands in for Settings > Vault: opens the Folders page modally, as VaultSettingsPageViewModel does.</summary>
    public sealed class OahuDebugHostPage : ContentPage
    {
        // Once per process: on Android the app navigates to the login home twice at start, which makes two hosts.
        private static bool _openedOnce;

        public OahuDebugHostPage()
        {
            Title = "OAHU debug";
            var open = new Button { Text = "Open Folders (fake data)", AutomationId = "OahuDebugOpenFolders" };
            open.Clicked += async (_, _) => await OpenAsync();
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 16,
                Children =
                {
                    new Label { Text = "DEBUG build: folders come from an in-memory fake, not a Bitwarden account." },
                    open,
                },
            };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (!_openedOnce)
            {
                _openedOnce = true;
                await Task.Delay(500);
                await OpenAsync();
            }
        }

        private async Task OpenAsync()
        {
            Console.WriteLine("OAHUDEBUG opening FoldersPage");
            try
            {
                await Navigation.PushModalAsync(new NavigationPage(new FoldersPage()));
                Console.WriteLine("OAHUDEBUG FoldersPage pushed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OAHUDEBUG open failed: {ex}");
            }
        }
    }
}
#endif
