#if DEBUG
// DEBUG ONLY: the step 5 add/edit routes (docs/oahu/05-cipher-edit.md). Opens CipherAddEditPage modally over the fake
// vault's TabsPage, as the vault's add button, an item's Edit and its Clone do. Never compiled into Release.
//
//   BW_OAHU_DEBUG=edit with BW_OAHU_EDIT / bwOahuEdit:
//     add | add-card | add-identity | add-note   a new item of that type (add: login)
//     add-org                                   a new login in the Engineering collection (owned by Example Org)
//     edit:<id> | clone:<id>                    e.g. edit:login-many, edit:card-visa, edit:identity-alice,
//                                               edit:note-server, edit:login-jira (organization), edit:login-aws
//                                               (password hidden from the user), clone:login-github (passkey)
//   BW_OAHU_VAULT / bwOahuVault       vault (default) or vault-manypick (300 folders, 2,000 collections)
//   BW_OAHU_CHURN / bwOahuChurn       1: every 4 s, C# moves, inserts, removes or rewrites other rows (focus probe)
//   BW_OAHU_FAKESCAN / bwOahuFakeScan 1: the TOTP camera opens a page that returns a fake key (no camera here)
//   BW_OAHU_HIDESOFT / bwOahuHideSoft 0: the page's HideSoftInputOnTapped off (G29 probe)
using Bit.Core.Enums;

namespace Bit.App.Pages.OahuDebug
{
    public static class OahuDebugEdit
    {
        private static bool _opened;

        public static void Show()
        {
            if (_opened)
            {
                return; // Android navigates to the login home twice at start
            }
            _opened = true;
            var request = OahuDebugRoute.Option("BW_OAHU_EDIT", "bwOahuEdit") ?? "add";
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
                        var page = Create(request);
                        Console.WriteLine($"OAHUDEBUG opening add/edit '{request}'");
                        await nav.Navigation.PushModalAsync(new NavigationPage(page));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"OAHUDEBUG add/edit route failed: {ex}");
                    }
                });
            });
        }

        private static CipherAddEditPage Create(string request)
        {
            var parts = request.Split(':', 2);
            return parts[0] switch
            {
                "add-card" => new CipherAddEditPage(type: CipherType.Card),
                "add-identity" => new CipherAddEditPage(type: CipherType.Identity),
                "add-note" => new CipherAddEditPage(type: CipherType.SecureNote),
                "add-org" => new CipherAddEditPage(type: CipherType.Login, collectionId: "col-eng",
                    organizationId: FakeVault.OrgId),
                "edit" => new CipherAddEditPage(parts[1]),
                "clone" => new CipherAddEditPage(parts[1], cloneMode: true),
                _ => new CipherAddEditPage(type: CipherType.Login),
            };
        }

        /// <summary>Called by the page once its form is loaded: starts the churn probe if asked.</summary>
        public static void Attached(CipherAddEditPage page, CipherAddEditPageViewModel vm)
        {
            if (OahuDebugRoute.Option("BW_OAHU_CHURN", "bwOahuChurn") != "1")
            {
                return;
            }
            var step = 0;
            page.Dispatcher.StartTimer(TimeSpan.FromSeconds(4), () =>
            {
                if (page.Window == null)
                {
                    return false;
                }
                vm.OahuDebugChurn(step++);
                return true;
            });
        }

        /// <summary>The TOTP camera button: a stand-in scan page when BW_OAHU_FAKESCAN=1.</summary>
        public static bool TryFakeScan(Page page, Func<string, Task> scanned)
        {
            if (OahuDebugRoute.Option("BW_OAHU_FAKESCAN", "bwOahuFakeScan") != "1")
            {
                return false;
            }
            var fake = new ContentPage { Title = "Fake scan (DEBUG)" };
            var ok = new Button { Text = "Return a fake key", AutomationId = "OahuDebugFakeScanOk" };
            ok.Clicked += async (_, _) =>
                await scanned("otpauth://totp/Fake:alice@example.com?secret=JBSWY3DPEHPK3PXP&issuer=Fake");
            var empty = new Button { Text = "Return nothing (read error)" };
            empty.Clicked += async (_, _) => await scanned(null);
            fake.Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 16,
                Children = { new Label { Text = "DEBUG: no camera. Pick what the scanner returns." }, ok, empty },
            };
            _ = page.Navigation.PushModalAsync(new NavigationPage(fake));
            return true;
        }
    }
}
#endif
