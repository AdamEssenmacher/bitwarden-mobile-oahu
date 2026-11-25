using Oahu;

namespace Bit.App.Contracts;

/// <summary>
/// The lock page (LockPage): unlock with the master password or a PIN, or with biometrics. MAUI keeps the title, the
/// account avatar and switcher overlay, the log out item or options menu, the loading overlay and every dialog
/// (docs/oahu/04-generator-lock.md).
/// </summary>
[OahuContract]
public interface ILockViewModel
{
    IOahuState<LockCopy> Copy { get; }

    IOahuState<AppPalette> Palette { get; }

    IOahuState<LockLayout> Layout { get; }

    /// <summary>The master password field. Each keystroke crosses, as MAUI's Entry binding did.</summary>
    IOahuEditable<string> MasterPassword { get; }

    /// <summary>The PIN field.</summary>
    IOahuEditable<string> Pin { get; }

    /// <summary>
    /// Focus the secret field, with the cursor at the given position (null: wherever it was). The page asks for it when
    /// it appears without biometrics, after the show/hide toggle, and when the biometric prompt falls back to the
    /// password (MAUI: RequestFocus and the view model's FocusSecretEntry event).
    /// </summary>
    IOahuEvents<SecretFocus> FocusSecret { get; }

    /// <summary>The Unlock button and the keyboard's Go key.</summary>
    Task Submit();

    /// <summary>The eye button: show or hide the secret.</summary>
    void ToggleShowSecret();

    /// <summary>"Use Face ID / biometrics to unlock".</summary>
    Task UseBiometrics();
}

public sealed record SecretFocus(int? Cursor);

public sealed record LockCopy(
    string MasterPassword,
    string Pin,
    string Unlock,
    string ToggleVisibility,
    string SecretShownHint,
    string SecretHiddenHint,
    string BiometricInvalidated);

/// <param name="PinEnabled">Unlock with a PIN instead of the master password.</param>
/// <param name="ShowSecretEntry">False for a biometric-only account with no master password or PIN.</param>
/// <param name="ShowSecret">The secret is shown in clear (the eye button).</param>
/// <param name="BiometricButton">The biometric button's text, or null when it is hidden.</param>
/// <param name="BiometricInvalidated">Biometric unlock was invalidated: show the warning.</param>
public sealed record LockLayout(
    bool PinEnabled,
    bool ShowSecretEntry,
    bool ShowSecret,
    string LockedVerifyText,
    string LoggedInAsText,
    string? BiometricButton,
    bool BiometricInvalidated);
