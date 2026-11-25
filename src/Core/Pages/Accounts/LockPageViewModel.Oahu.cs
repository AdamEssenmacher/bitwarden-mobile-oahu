#nullable enable
using Bit.App.Contracts;
using Bit.App.Utilities;
using Bit.Core.Resources.Localization;
using Oahu;

namespace Bit.App.Pages
{
    // The OAHU contract over the existing lock view model (docs/oahu/04-generator-lock.md). The secret fields are
    // editables over MasterPassword and Pin, so each keystroke reaches the view model as the MAUI Entry binding's did;
    // SubmitAsync, the biometric prompt and every dialog are unchanged. The view model's FocusSecretEntry event (and the
    // page's RequestFocus on appearing) become a one-shot FocusSecret event the screen acts on.
    public partial class LockPageViewModel : ILockViewModel
    {
        private IOahuState<LockCopy>? _oahuCopy;
        private IOahuState<LockLayout>? _layout;
        private PropertyEditable<string>? _masterPasswordEditable;
        private PropertyEditable<string>? _pinEditable;
        private EventSource<SecretFocus>? _focusSecret;

        IOahuState<LockCopy> ILockViewModel.Copy => _oahuCopy ??= new StateSource<LockCopy>(new LockCopy(
            MasterPassword: AppResources.MasterPassword,
            Pin: AppResources.PIN,
            Unlock: AppResources.Unlock,
            ToggleVisibility: AppResources.ToggleVisibility,
            SecretShownHint: AppResources.PasswordIsVisibleTapToHide,
            SecretHiddenHint: AppResources.PasswordIsNotVisibleTapToShow,
            BiometricInvalidated: AppResources.AccountBiometricInvalidated));

        IOahuState<AppPalette> ILockViewModel.Palette => OahuPalette.Current;

        // HasMasterPassword raises nothing; InitAsync sets it before LoggedInAsText, which is watched.
        IOahuState<LockLayout> ILockViewModel.Layout => _layout ??= OahuState.FromProperty(
            this,
            [nameof(PinEnabled), nameof(BiometricEnabled), nameof(ShowPassword), nameof(LockedVerifyText),
                nameof(LoggedInAsText), nameof(BiometricButtonVisible), nameof(BiometricButtonText),
                nameof(BiometricIntegrityValid)],
            () => new LockLayout(
                PinEnabled: PinEnabled,
                // MAUI's OnAppearing hid the field and the Unlock button for a biometric-only account.
                ShowSecretEntry: !(BiometricEnabled && !HasMasterPassword && !PinEnabled),
                ShowSecret: ShowPassword,
                LockedVerifyText: LockedVerifyText ?? string.Empty,
                LoggedInAsText: LoggedInAsText ?? string.Empty,
                BiometricButton: BiometricButtonVisible ? BiometricButtonText : null,
                BiometricInvalidated: !BiometricIntegrityValid));

        IOahuEditable<string> ILockViewModel.MasterPassword => _masterPasswordEditable ??= OahuEditable.FromProperty(
            this, nameof(MasterPassword), () => MasterPassword ?? string.Empty, value => MasterPassword = value);

        IOahuEditable<string> ILockViewModel.Pin => _pinEditable ??= OahuEditable.FromProperty(
            this, nameof(Pin), () => Pin ?? string.Empty, value => Pin = value);

        IOahuEvents<SecretFocus> ILockViewModel.FocusSecret
        {
            get
            {
                if (_focusSecret == null)
                {
                    _focusSecret = new EventSource<SecretFocus>();
                    FocusSecretEntry += OnFocusSecretEntry;
                }
                return _focusSecret;
            }
        }

        Task ILockViewModel.Submit()
        {
            // The Entry's ReturnCommand and the Unlock button both ran SubmitCommand (one run at a time).
            SubmitCommand.Execute(null);
            return Task.CompletedTask;
        }

        void ILockViewModel.ToggleShowSecret() => TogglePassword();

        Task ILockViewModel.UseBiometrics() => PromptBiometricAsync();

        /// <summary>The page's RequestFocus(SecretEntry) when it appears without biometrics.</summary>
        public void RequestSecretFocus(int? cursor = null)
        {
            _ = ((ILockViewModel)this).FocusSecret;
            _focusSecret!.Publish(new SecretFocus(cursor));
        }

        // The weak event manager can raise this off the main thread (the biometric fallback); OAHU sources are main-thread only.
        private void OnFocusSecretEntry(int? cursor) =>
            MainThread.BeginInvokeOnMainThread(() => _focusSecret?.Publish(new SecretFocus(cursor)));
    }
}
