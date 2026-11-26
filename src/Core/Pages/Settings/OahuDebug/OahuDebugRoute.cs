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
//   BW_OAHU_THEME_SWITCH / bwOahuThemeSwitch  a theme name: 12 s after start, switch the app to it as the Appearance
//                                    settings page does (state, ThemeManager.SetTheme, "updatedTheme"), so a theme
//                                    change reaches screens that are open (step 7).
//   folders with BW_OAHU_THEME:      installs the fake vault (its folders) so the theme applies to the folder pages.
//
// Step 5 (docs/oahu/05-cipher-edit.md): edit, with BW_OAHU_EDIT and the options listed in OahuDebugEdit.cs.
//
// Step 4 (docs/oahu/04-generator-lock.md):
//   generator, generator-policy  The vault routes, opened on the Generator tab; -policy adds an organization password
//                               generator policy (minimum length 20, at least 3 numbers and 2 special characters, all
//                               character sets and passphrase capitalize/number required, at least 6 words).
//   lock, lock-pin, lock-bio, lock-pin-bio, lock-bio-only
//                               LockPage for the fake account, unlocking into the fake vault: master password "debug",
//                               PIN "1234"; -bio adds biometric unlock (a fake prompt), -bio-only has no master password.
//                               The vault locks again whenever the app goes to the background (as a vault timeout of
//                               "immediately" does), and Settings > Lock now locks it.
//   BW_OAHU_GEN / bwOahuGen          modal or modal-username: also open the generator as add/edit does (Select logs the
//                                    value's length and shows a toast).
//   BW_OAHU_SLOWSAVE / bwOahuSlowSave 1: saving generator options yields before completing (G27).
//   BW_OAHU_CLAMP_LIVE / bwOahuClampLive 1: clamp the length to the policy minimum during the drag, not on release.
//   BW_OAHU_BIO / bwOahuBio          real: the system biometric prompt instead of the fake one; invalid: the account's
//                                    biometric integrity is invalidated.
//   BW_OAHU_DIAG / bwOahuDiag        1: print OahuDiagnostics events (metadata only: screen, member, outcome).
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
                    var themed = Option("BW_OAHU_THEME", "bwOahuTheme") != null
                        || Option("BW_OAHU_THEME_SWITCH", "bwOahuThemeSwitch") != null;
                    if (!_shown)
                    {
                        _shown = true;
                        if (themed)
                        {
                            InstallVault("vault");
                        }
                        else
                        {
                            ServiceContainer.RegisteredServices["folderService"] =
                                new FakeFolderService(seed: requested == "folders");
                        }
                    }
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (themed)
                        {
                            ThemeManager.SetTheme(Application.Current.Resources);
                            Bit.App.Utilities.OahuPalette.Refresh();
                        }
                        Bit.App.App.MainPage = new NavigationPage(new OahuDebugHostPage());
                    });
                    return true;
                case "vault" or "vault-large" or "vault-empty":
                    if (!_shown)
                    {
                        _shown = true;
                        InstallVault(requested);
                    }
                    MainThread.BeginInvokeOnMainThread(ShowVault);
                    return true;
                case "generator" or "generator-policy":
                    if (!_shown)
                    {
                        _shown = true;
                        InstallVault("vault");
                        if (requested == "generator-policy")
                        {
                            _policy.GeneratorPolicy = new Bit.Core.Models.Domain.PasswordGeneratorPolicyOptions
                            {
                                MinLength = 20,
                                UseUppercase = true,
                                UseLowercase = true,
                                UseNumbers = true,
                                UseSpecial = true,
                                NumberCount = 3,
                                SpecialCount = 2,
                                MinNumberOfWords = 6,
                                Capitalize = true,
                                IncludeNumber = true,
                            };
                        }
                    }
                    MainThread.BeginInvokeOnMainThread(ShowGenerator);
                    return true;
                case "lock" or "lock-pin" or "lock-bio" or "lock-pin-bio" or "lock-bio-only":
                    if (!_shown)
                    {
                        _shown = true;
                        InstallVault("vault");
                        InstallLock(requested);
                    }
                    // Posted, not BeginInvokeOnMainThread (which runs at once on the main thread): ShowLock must run
                    // after App.Bootstrap has finished with the real platform utilities.
                    Application.Current.Dispatcher.Dispatch(ShowLock);
                    return true;
                case "edit":
                    if (!_shown)
                    {
                        _shown = true;
                        InstallVault(Option("BW_OAHU_VAULT", "bwOahuVault") ?? "vault");
                        // The page asks whether the account has a master password (the re-prompt toggle).
                        ServiceContainer.RegisteredServices["userVerificationService"] = new FakeUserVerificationService();
                    }
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ShowVault();
                        OahuDebugEdit.Show();
                    });
                    return true;
                default:
                    return false;
            }
        }

        private static FakePolicyService _policy;
        private static FakeVaultTimeoutService _vaultTimeout;
        private static OahuDebugStateService _debugState;

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
            services["policyService"] = _policy = new FakePolicyService(vault);
            services["syncService"] = new FakeSyncService(vault, messaging);
            services["vaultTimeoutService"] = _vaultTimeout = new FakeVaultTimeoutService();
            services["eventService"] = new FakeEventService();
            services["auditService"] = new FakeAuditService();
            services["passwordRepromptService"] = new FakePasswordRepromptService(platformUtils);
            _debugState = debugState;
            InstallGenerator(debugState);
            InstallDiagnostics();
            var switchTo = Option("BW_OAHU_THEME_SWITCH", "bwOahuThemeSwitch");
            if (switchTo != null)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(12000);
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // As AppearanceSettingsPageViewModel.OnThemeChangingAsync does.
                        debugState.Theme = switchTo;
                        ThemeManager.SetTheme(Application.Current.Resources);
                        messaging.Send(ThemeManager.UPDATED_THEME_MESSAGE_KEY);
                        Console.WriteLine($"OAHUDEBUG theme switched to {switchTo}");
                    });
                });
            }
            Console.WriteLine($"OAHUDEBUG installed fake vault '{variant}' with {vault.Ciphers.Count} ciphers");
        }

        // The generator services were built at start over the real state service (no account): the fake account's
        // token (for the plus-addressed email), in-memory options and the fake policy need new instances. The real
        // generation and normalization code runs; forwarded-email aliases never reach a forwarding service.
        private static void InstallGenerator(OahuDebugStateService debugState)
        {
            var services = ServiceContainer.RegisteredServices;
            var crypto = ServiceContainer.Resolve<ICryptoService>("cryptoService");
            var cryptoFunctions = ServiceContainer.Resolve<ICryptoFunctionService>("cryptoFunctionService");
            var api = ServiceContainer.Resolve<IApiService>("apiService");
            services["tokenService"] = new Bit.Core.Services.TokenService(debugState);
            services["passwordGenerationService"] =
                new Bit.Core.Services.PasswordGenerationService(crypto, debugState, cryptoFunctions, _policy);
            services["usernameGenerationService"] = new FakeUsernameGenerationService(
                new Bit.Core.Services.UsernameGenerationService(crypto, api, debugState));
            debugState.SlowSave = Option("BW_OAHU_SLOWSAVE", "bwOahuSlowSave") == "1";
        }

        private static void InstallDiagnostics()
        {
            if (Option("BW_OAHU_DIAG", "bwOahuDiag") != "1")
            {
                return;
            }
            // Metadata only (OAHU's debugging.md): no edit values, so a master password typed here isn't printed.
            Oahu.OahuDiagnostics.Sink = e => Console.WriteLine(
                $"OAHUDIAG {e.Kind} {e.ContractName} screen={e.ScreenId} inv={e.InvocationId} {e.ActionName} {e.Outcome} {e.Elapsed?.TotalMilliseconds:0.0}ms");
        }

        private static void InstallLock(string variant)
        {
            var services = ServiceContainer.RegisteredServices;
            OahuDebugLock.Locked = true;
            OahuDebugLock.Pin = variant is "lock-pin" or "lock-pin-bio";
            OahuDebugLock.Biometric = variant is "lock-bio" or "lock-pin-bio" or "lock-bio-only";
            OahuDebugLock.HasMasterPassword = variant != "lock-bio-only";
            var bio = Option("BW_OAHU_BIO", "bwOahuBio");
            OahuDebugLock.RealBiometric = bio == "real";
            OahuDebugLock.BiometricIntegrityValid = bio != "invalid";
            _vaultTimeout.LockEnabled = true;
            services["cryptoService"] = new OahuDebugCryptoService(ServiceContainer.Resolve<ICryptoService>("cryptoService"));
            services["biometricService"] = new FakeBiometricService();
            services["userVerificationService"] = new FakeUserVerificationService();
            services["deviceTrustCryptoService"] = new FakeDeviceTrustCryptoService();
            Console.WriteLine($"OAHUDEBUG installed lock '{variant}' (pin {OahuDebugLock.Pin}, biometric {OahuDebugLock.Biometric}, " +
                $"master password {OahuDebugLock.HasMasterPassword}, real prompt {OahuDebugLock.RealBiometric})");
        }

        private static bool _lockWatching;
        private static bool _wentToBackground;

        private static void ShowLock()
        {
            // Replaced here, not in InstallLock: App.Bootstrap resolves "platformUtilsService" as the concrete
            // MobilePlatformUtilsService right after the start navigation that runs this route.
            if (ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService") is not OahuDebugPlatformUtilsService)
            {
                ServiceContainer.RegisteredServices["platformUtilsService"] = new OahuDebugPlatformUtilsService(
                    ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService"));
            }
            ThemeManager.SetTheme(Application.Current.Resources);
            Bit.App.Utilities.OahuPalette.Refresh();
            Bit.App.App.MainPage = new NavigationPage(new LockPage(autoPromptBiometric: true));
            if (_lockWatching)
            {
                return;
            }
            _lockWatching = true;
            // A vault timeout of "immediately": lock when the app goes to the background, so LockPage shows on resume.
            var window = Application.Current.Windows.FirstOrDefault();
            if (window != null)
            {
                window.Stopped += (_, _) =>
                {
                    Console.WriteLine("OAHUDEBUG window stopped");
                    _wentToBackground = true;
                };
                window.Resumed += async (_, _) =>
                {
                    Console.WriteLine($"OAHUDEBUG window resumed (locked: {OahuDebugLock.Locked})");
                    if (_wentToBackground && !OahuDebugLock.Locked)
                    {
                        _wentToBackground = false;
                        await _vaultTimeout.LockAsync(allowSoftLock: true, userInitiated: false);
                    }
                };
            }
        }

        private static bool _generatorOpened;

        private static void ShowGenerator()
        {
            ShowVault();
            if (_generatorOpened)
            {
                return;
            }
            _generatorOpened = true;
            var modal = Option("BW_OAHU_GEN", "bwOahuGen");
            Task.Run(async () =>
            {
                await Task.Delay(1500);
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        if (Bit.App.App.MainPage is not TabsPage tabs)
                        {
                            return;
                        }
                        var generatorTab = tabs.Children.FirstOrDefault(c => c is NavigationPage n && n.RootPage is GeneratorPage);
                        if (generatorTab != null)
                        {
                            tabs.CurrentPage = generatorTab;
                        }
                        if (modal is "modal" or "modal-username")
                        {
                            await Task.Delay(1500);
                            var nav = tabs.CurrentPage.Navigation;
                            GeneratorPage page = null;
                            page = new GeneratorPage(false, async value =>
                            {
                                // What CipherAddEditPageViewModel's select action does, minus the cipher.
                                Console.WriteLine($"OAHUDEBUG generator selected a value of length {value?.Length}");
                                ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService")
                                    .ShowToast("info", null, $"OAHU debug: selected {value?.Length} characters");
                                await nav.PopModalAsync();
                            }, isUsernameGenerator: modal == "modal-username", emailWebsite: "github.com", editMode: true);
                            await nav.PushModalAsync(new NavigationPage(page));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"OAHUDEBUG generator route failed: {ex}");
                    }
                });
            });
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
