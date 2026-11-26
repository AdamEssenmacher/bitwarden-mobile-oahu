using Bit.App.Abstractions;
using Bit.App.Models;
using Bit.Core.Resources.Localization;
using Bit.App.Utilities;
using Bit.Core.Abstractions;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using Bit.Core.Services;

namespace Bit.App.Pages
{
    public partial class CipherAddEditPage : BaseContentPage
    {
        private readonly AppOptions _appOptions;
        private readonly IStateService _stateService;
        private readonly IDeviceActionService _deviceActionService;
        private readonly IAutofillHandler _autofillHandler;
        private readonly IVaultTimeoutService _vaultTimeoutService;
        private readonly IUserVerificationService _userVerificationService;
#if ANDROID
        private readonly LazyResolve<IFido2MakeCredentialConfirmationUserInterface> _fido2MakeCredentialConfirmationUserInterface = new LazyResolve<IFido2MakeCredentialConfirmationUserInterface>();
#endif

        private CipherAddEditPageViewModel _vm;
        private bool _fromAutofill;

        public CipherAddEditPage(
            string cipherId = null,
            CipherType? type = null,
            string folderId = null,
            string collectionId = null,
            string organizationId = null,
            string name = null,
            string uri = null,
            bool fromAutofill = false,
            AppOptions appOptions = null,
            bool cloneMode = false,
            CipherDetailsPage cipherDetailsPage = null)
        {
            _stateService = ServiceContainer.Resolve<IStateService>("stateService");
            _deviceActionService = ServiceContainer.Resolve<IDeviceActionService>("deviceActionService");
            _autofillHandler = ServiceContainer.Resolve<IAutofillHandler>();
            _vaultTimeoutService = ServiceContainer.Resolve<IVaultTimeoutService>("vaultTimeoutService");
            _userVerificationService = ServiceContainer.Resolve<IUserVerificationService>();

            _appOptions = appOptions;
            _fromAutofill = fromAutofill;
            FromAutofillFramework = _appOptions?.FromAutofillFramework ?? false;
#if ANDROID
            FromAndroidFido2Framework = _fido2MakeCredentialConfirmationUserInterface.Value.IsConfirmingNewCredential;
#endif
            InitializeComponent();
            _vm = BindingContext as CipherAddEditPageViewModel;
            _vm.Page = this;
            _vm.CipherId = cipherId;
            _vm.FolderId = folderId == "none" ? null : folderId;
            _vm.CollectionIds = collectionId != null ? new HashSet<string>(new List<string> { collectionId }) : null;
            _vm.OrganizationId = organizationId;
            _vm.Type = type;
            _vm.DefaultName = name ?? appOptions?.SaveName;
            _vm.DefaultUri = uri ?? appOptions?.Uri;
            _vm.CloneMode = cloneMode;
            _vm.CipherDetailsPage = cipherDetailsPage;
            _vm.Init();
            SetActivityIndicator();
            if (_vm.EditMode && !_vm.CloneMode && DeviceInfo.Platform == DevicePlatform.Android)
            {
                ToolbarItems.Add(_attachmentsItem);
                ToolbarItems.Add(_deleteItem);
            }
            if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
                ToolbarItems.Add(_closeItem);
                if (_vm.EditMode && !_vm.CloneMode)
                {
                    ToolbarItems.Add(_moreItem);
                }
                _vm.ShowNotesSeparator = true;

            }

            // OAHU: the pickers, Return-key chain and entries are native (docs/oahu/05-cipher-edit.md); the screen
            // asks for the TOTP scan page through the view model.
            _vm.ScanTotpRequested += () => ScanTotp_Clicked(this, EventArgs.Empty);
            // Step 7, D25 trial: the sections go after the screen that draws them (contracts.md, the Dispose comment).
            _scrollView.TornDown += (_, _) => _vm.DisposeSections();
#if ANDROID
            _scrollView.AndroidSavedStateViewId = OahuAndroidIds.Get("oahu_cipher_edit");
#endif
#if DEBUG
            // G29 probe: BW_OAHU_HIDESOFT=0 turns the page's HideSoftInputOnTapped off.
            if (Bit.App.Pages.OahuDebug.OahuDebugRoute.Option("BW_OAHU_HIDESOFT", "bwOahuHideSoft") == "0")
            {
                HideSoftInputOnTapped = false;
                Console.WriteLine("OAHUDEBUG add/edit: HideSoftInputOnTapped off");
            }
#endif
        }

        public bool FromAutofillFramework { get; set; }
        public bool FromAndroidFido2Framework { get; set; }
        public CipherAddEditPageViewModel ViewModel => _vm;

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (!await AppHelpers.IsVaultTimeoutImmediateAsync())
            {
                await _vaultTimeoutService.CheckVaultTimeoutAsync();
            }
            if (await _vaultTimeoutService.IsLockedAsync())
            {
                return;
            }
            await LoadOnAppearedAsync(_scrollView, true, async () =>
            {
                var success = await _vm.LoadAsync(_appOptions);
                if (!success)
                {
                    await Navigation.PopModalAsync();
                    return;
                }
                AdjustToolbar();
#if DEBUG
                // Step 7, D25 trial: BW_OAHU_SECTIONS=1 mounts the identity and URI sections as child contracts.
                if (Bit.App.Pages.OahuDebug.OahuDebugRoute.Option("BW_OAHU_SECTIONS", "bwOahuSections") == "1")
                {
#if IOS
                    _vm.MountSections(v => Oahu.iOS.OahuScreens.Mount<Bit.App.Contracts.IIdentitySectionViewModel>(v),
                        v => Oahu.iOS.OahuScreens.Mount<Bit.App.Contracts.IUriSectionViewModel>(v));
#elif ANDROID
                    _vm.MountSections(v => Oahu.Android.OahuScreens.Mount<Bit.App.Contracts.IIdentitySectionViewModel>(v),
                        v => Oahu.Android.OahuScreens.Mount<Bit.App.Contracts.IUriSectionViewModel>(v));
#endif
                }
#endif
                await ShowAlertsAsync();
                if (!_vm.EditMode && string.IsNullOrWhiteSpace(_vm.Cipher?.Name))
                {
                    // As RequestFocus(_nameEntry) did: wait for the modal animation. A focus request delivered while the
                    // modal is still presenting is dropped by SwiftUI (docs/oahu/gaps.md, step 5).
                    var delay = ShowModalAnimationDelay;
#if DEBUG
                    // Step 7 (#976): BW_OAHU_FOCUS=now or held publishes at once; held lets the screen hold it.
                    if (Bit.App.Pages.OahuDebug.OahuDebugRoute.Option("BW_OAHU_FOCUS", "bwOahuFocus") is "now" or "held")
                    {
                        delay = 0;
                    }
#endif
                    Task.Run(async () =>
                    {
                        await Task.Delay(delay);
                        MainThread.BeginInvokeOnMainThread(() => _vm.RequestFocus(Bit.App.Contracts.FormField.Name));
                    });
                }
#if DEBUG
                Bit.App.Pages.OahuDebug.OahuDebugEdit.Attached(this, _vm);
#endif
            });

            _vm.ShowPasswordPromptOption = await _userVerificationService.HasMasterPasswordAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
        }

        protected override bool OnBackButtonPressed()
        {
            if (FromAutofillFramework)
            {
                App.MainPage = new TabsPage();
                return true;
            }
            return base.OnBackButtonPressed();
        }

        private async void PasswordHistory_Tapped(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                await Navigation.PushModalAsync(
                    new Microsoft.Maui.Controls.NavigationPage(new PasswordHistoryPage(_vm.CipherId)));
            }
        }

        private async void Save_Clicked(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                await _vm.SubmitAsync();
            }
        }

        private async void Attachments_Clicked(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                var page = new AttachmentsPage(_vm.CipherId);
                await Navigation.PushModalAsync(new Microsoft.Maui.Controls.NavigationPage(page));
            }
        }

        private async void Share_Clicked(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                var page = new SharePage(_vm.CipherId);
                await Navigation.PushModalAsync(new Microsoft.Maui.Controls.NavigationPage(page));
            }
        }

        private async void Delete_Clicked(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                if (await _vm.DeleteAsync())
                {
                    await Navigation.PopModalAsync();
                }
            }
        }

        private async void Collections_Clicked(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                var page = new CollectionsPage(_vm.CipherId);
                await Navigation.PushModalAsync(new Microsoft.Maui.Controls.NavigationPage(page));
            }
        }

        private async void ScanTotp_Clicked(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
#if DEBUG
                // The simulator and emulator have no camera: BW_OAHU_FAKESCAN=1 pushes a page that returns a key.
                if (Bit.App.Pages.OahuDebug.OahuDebugEdit.TryFakeScan(this, async key =>
                    {
                        await Navigation.PopModalAsync();
                        await _vm.UpdateTotpKeyAsync(key);
                    }))
                {
                    return;
                }
#endif
                var page = new ScanPage(key =>
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            await Navigation.PopModalAsync();
                            await _vm.UpdateTotpKeyAsync(key);
                        }
                        catch (Exception ex)
                        {
                            LoggerHelper.LogEvenIfCantBeResolved(ex);
                            throw;
                        }
                    });
                });

                await Navigation.PushModalAsync(new Microsoft.Maui.Controls.NavigationPage(page));
            }
        }

        private async void More_Clicked(object sender, System.EventArgs e)
        {
            if (!DoOnce())
            {
                return;
            }
            var options = new List<string> { AppResources.Attachments };
            if (_vm.EditMode)
            {
                options.Add(_vm.Cipher.OrganizationId == null ? AppResources.MoveToOrganization : AppResources.Collections);
            }
            var selection = await DisplayActionSheet(AppResources.Options, AppResources.Cancel,
                (_vm.EditMode && !_vm.CloneMode) ? AppResources.Delete : null, options.ToArray());
            if (selection == AppResources.Delete)
            {
                if (await _vm.DeleteAsync())
                {
                    await Navigation.PopModalAsync();
                }
            }
            else if (selection == AppResources.Attachments)
            {
                var page = new AttachmentsPage(_vm.CipherId);
                await Navigation.PushModalAsync(new Microsoft.Maui.Controls.NavigationPage(page));
            }
            else if (selection == AppResources.Collections)
            {
                var page = new CollectionsPage(_vm.CipherId);
                await Navigation.PushModalAsync(new Microsoft.Maui.Controls.NavigationPage(page));
            }
            else if (selection == AppResources.MoveToOrganization)
            {
                var page = new SharePage(_vm.CipherId);
                await Navigation.PushModalAsync(new Microsoft.Maui.Controls.NavigationPage(page));
            }
        }

        private async void Close_Clicked(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                await Navigation.PopModalAsync();
            }
        }

        private async Task ShowAlertsAsync()
        {
            if (!_vm.EditMode)
            {
                if (_vm.Cipher == null)
                {
                    return;
                }
                var addLoginShown = await _stateService.GetAddSitePromptShownAsync();
                if (_vm.Cipher.Type == CipherType.Login && !_fromAutofill && !addLoginShown.GetValueOrDefault())
                {
                    await _stateService.SetAddSitePromptShownAsync(true);
                    if (DeviceInfo.Platform == DevicePlatform.iOS)
                    {
                        if (_deviceActionService.SystemMajorVersion() < 12)
                        {
                            await DisplayAlert(AppResources.BitwardenAppExtension,
                                AppResources.BitwardenAppExtensionAlert2, AppResources.Ok);
                        }
                        else
                        {
                            await DisplayAlert(AppResources.PasswordAutofill,
                                AppResources.BitwardenAutofillAlert2, AppResources.Ok);
                        }
                    }
                    else if (DeviceInfo.Platform == DevicePlatform.Android &&
                             !_autofillHandler.AutofillAccessibilityServiceRunning() &&
                             !_autofillHandler.AutofillServiceEnabled())
                    {
                        await DisplayAlert(AppResources.BitwardenAutofillService,
                            AppResources.BitwardenAutofillServiceAlert2, AppResources.Ok);
                    }
                }
            }
        }

        private void AdjustToolbar()
        {
            if ((_vm.EditMode || _vm.CloneMode) && DeviceInfo.Platform == DevicePlatform.Android)
            {
                if (_vm.Cipher == null)
                {
                    return;
                }
                if (_vm.Cipher.OrganizationId == null)
                {
                    if (ToolbarItems.Contains(_collectionsItem))
                    {
                        ToolbarItems.Remove(_collectionsItem);
                    }
                    if (!ToolbarItems.Contains(_shareItem) && !_vm.CloneMode)
                    {
                        ToolbarItems.Insert(2, _shareItem);
                    }
                }
                else
                {
                    if (ToolbarItems.Contains(_shareItem))
                    {
                        ToolbarItems.Remove(_shareItem);
                    }
                    if (!ToolbarItems.Contains(_collectionsItem))
                    {
                        ToolbarItems.Insert(2, _collectionsItem);
                    }
                }
            }
        }
    }
}
