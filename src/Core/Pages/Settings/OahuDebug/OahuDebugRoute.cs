#if DEBUG
// DEBUG ONLY: launch routes that show the OAHU pages without an account (docs/oahu/02-folders.md, 03-vault.md).
//
// iOS simulator: SIMCTL_CHILD_BW_OAHU_DEBUG=vault xcrun simctl launch <device> com.8bit.bitwarden
// Android:       adb shell am start -n com.x8bit.bitwarden/com.x8bit.bitwarden.MainActivity --es bwOahuDebug vault
//
// Routes:
//   folders, folders-empty      Settings > Folders over a fake folder service (step 2).
//   vault, vault-large, vault-empty
//                               The app's real TabsPage over a fake vault (FakeVault): about 25 items of every type,
//                               the same plus 2,000 generated logins, or nothing. Pull to refresh (and Sync on
//                               Android) runs a fake sync that changes the vault and announces "syncCompleted" from a
//                               thread-pool thread, as the real sync does.
// Options (environment variable on iOS, intent extra on Android):
//   BW_OAHU_THEME / bwOahuTheme      light, dark, black, nord or solarizeddark: the app theme to use.
//   BW_OAHU_OPEN / bwOahuOpen        a fake cipher id (login-github, card-visa, identity-alice, note-server, ...):
//                                    open its details page once the vault shows.
//   BW_OAHU_SECURE / bwOahuSecure    1: screen capture not allowed (Android FLAG_SECURE, as the app's setting does).
//   BW_OAHU_DRILL / bwOahuDrill      1: push Types > Login, then a folder, then a collection, one after the other.
//
// The vault routes replace the registered cipher, folder, collection, organization, policy, sync, vault timeout,
// event, audit and password re-prompt services with fakes, and wrap the state service so it reports a signed-in,
// unlocked account. They must never run on a device with a real account; they are never compiled into Release, and
// without the variable or extra nothing changes.
using Bit.App.Abstractions;
using Bit.Core.Abstractions;
using Bit.App.Utilities;
using Bit.Core.Utilities;

namespace Bit.App.Pages.OahuDebug
{
    public static class OahuDebugRoute
    {
        private static bool _shown;
        private static bool _vaultOpened;

        public static string Requested => Option("BW_OAHU_DEBUG", "bwOahuDebug");

        public static string Option(string variable, string extra)
        {
            var value = Environment.GetEnvironmentVariable(variable);
#if ANDROID
            value ??= Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Intent?.GetStringExtra(extra);
#endif
            return string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>Shows the debug page instead of the login home page when a route was requested.</summary>
        public static bool TryShow()
        {
            var requested = Requested;
            switch (requested)
            {
                case "folders" or "folders-empty":
                    if (!_shown)
                    {
                        _shown = true;
                        ServiceContainer.RegisteredServices["folderService"] =
                            new FakeFolderService(seed: requested == "folders");
                    }
                    MainThread.BeginInvokeOnMainThread(() =>
                        Bit.App.App.MainPage = new NavigationPage(new OahuDebugHostPage()));
                    return true;
                case "vault" or "vault-large" or "vault-empty":
                    if (!_shown)
                    {
                        _shown = true;
                        InstallVault(requested);
                    }
                    MainThread.BeginInvokeOnMainThread(ShowVault);
                    return true;
                default:
                    return false;
            }
        }

        private static void InstallVault(string variant)
        {
            var vault = new FakeVault(variant);
            var services = ServiceContainer.RegisteredServices;
            var messaging = ServiceContainer.Resolve<IMessagingService>("messagingService");
            var platformUtils = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            var realState = ServiceContainer.Resolve<IStateService>("stateService");

            var debugState = new OahuDebugStateService(realState, vault,
                Option("BW_OAHU_THEME", "bwOahuTheme"), screenCaptureAllowed: Option("BW_OAHU_SECURE", "bwOahuSecure") != "1");
            services["stateService"] = debugState;
            // The device action service resolved the real state service at start; it decides FLAG_SECURE on Android
            // (SetScreenCaptureAllowedAsync, called by every vault load), so point it at the debug one.
            var deviceActions = ServiceContainer.Resolve<IDeviceActionService>("deviceActionService");
            var stateField = deviceActions.GetType().GetField("_stateService",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (stateField?.FieldType == typeof(IStateService))
            {
                stateField.SetValue(deviceActions, debugState);
            }
            services["cipherService"] = new FakeCipherService(vault);
            services["folderService"] = new FakeFolderService(vault.Folders);
            services["collectionService"] = new FakeCollectionService(vault);
            services["organizationService"] = new FakeOrganizationService(vault);
            services["policyService"] = new FakePolicyService(vault);
            services["syncService"] = new FakeSyncService(vault, messaging);
            services["vaultTimeoutService"] = new FakeVaultTimeoutService();
            services["eventService"] = new FakeEventService();
            services["auditService"] = new FakeAuditService();
            services["passwordRepromptService"] = new FakePasswordRepromptService(platformUtils);
            Console.WriteLine($"OAHUDEBUG installed fake vault '{variant}' with {vault.Ciphers.Count} ciphers");
        }

        private static void ShowVault()
        {
            // The theme was applied at start from the real state; apply it again from the debug one.
            ThemeManager.SetTheme(Application.Current.Resources);
            Bit.App.Utilities.OahuPalette.Refresh();
            Bit.App.App.MainPage = new TabsPage();
            if (_vaultOpened)
            {
                return; // Android navigates to the login home twice at start (docs/oahu/gaps.md, "Not OAHU")
            }
            _vaultOpened = true;
            var open = Option("BW_OAHU_OPEN", "bwOahuOpen");
            var drill = Option("BW_OAHU_DRILL", "bwOahuDrill") == "1";
            if (open == null && !drill)
            {
                return;
            }
            Task.Run(async () =>
            {
                await Task.Delay(2500);
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        if (Bit.App.App.MainPage is not TabbedPage tabs || tabs.Children[0] is not NavigationPage nav)
                        {
                            return;
                        }
                        if (open != null)
                        {
                            Console.WriteLine($"OAHUDEBUG opening details of {open}");
                            await nav.Navigation.PushModalAsync(new NavigationPage(new CipherDetailsPage(open)));
                        }
                        else
                        {
                            Console.WriteLine("OAHUDEBUG drilling down");
                            await nav.PushAsync(new GroupingsPage(false, Core.Enums.CipherType.Login, pageTitle: "Logins"));
                            await Task.Delay(1500);
                            await nav.PushAsync(new GroupingsPage(false, folderId: "folder-work", pageTitle: "Work"));
                            await Task.Delay(1500);
                            await nav.PushAsync(new GroupingsPage(false, collectionId: "col-eng", pageTitle: "Engineering"));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"OAHUDEBUG open failed: {ex}");
                    }
                });
            });
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
