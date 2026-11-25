#if DEBUG
// DEBUG ONLY: what the lock routes need to show LockPage for an account that doesn't exist and unlock it without any
// real key (docs/oahu/04-generator-lock.md). The debug master password is "debug" and the debug PIN is "1234"; any
// other value is refused, as a wrong one would be. Biometrics are a MAUI action sheet standing in for the system
// prompt (Succeed / Fail / Use master password), unless BW_OAHU_BIO=real, which forwards to the real prompt.
//
// The real crypto, biometric and keychain code is untouched: these are registered in place of the real services for
// the debug process only, and forward everything the lock page doesn't use to the real ones (the vault itself is
// FakeVault, whose values are preset and never decrypted).
using Bit.App.Abstractions;
using Bit.Core.Abstractions;
using Bit.Core.Enums;
using Bit.Core.Models.Domain;
using Bit.Core.Models.Response;
using Bit.Core.Utilities;

namespace Bit.App.Pages.OahuDebug
{
    /// <summary>The fake account's lock settings and whether it is locked.</summary>
    public static class OahuDebugLock
    {
        public const string MasterPassword = "debug";
        public const string PinCode = "1234";

        public static bool Locked { get; set; }
        public static bool Pin { get; set; }
        public static bool Biometric { get; set; }
        public static bool HasMasterPassword { get; set; } = true;
        public static bool BiometricIntegrityValid { get; set; } = true;
        public static bool RealBiometric { get; set; }

        /// <summary>The unlocked vault's key: random bytes, never used to decrypt anything.</summary>
        public static readonly UserKey FakeUserKey = new(RandomBytes());

        private static byte[] RandomBytes()
        {
            var bytes = new byte[64];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            return bytes;
        }
    }

    /// <summary>The real crypto service, except unlocking: the master password and PIN are compared with the debug ones.</summary>
    public sealed partial class OahuDebugCryptoService
    {
        private readonly ICryptoService _inner;

        public OahuDebugCryptoService(ICryptoService inner) => _inner = inner;

        // The "master key" is a hash of what was typed, so a wrong password gives a different one.
        public Task<MasterKey> MakeMasterKeyAsync(string password, string email, KdfConfig kdfConfig) =>
            Task.FromResult(new MasterKey(System.Security.Cryptography.SHA512.HashData(
                System.Text.Encoding.UTF8.GetBytes("oahu-debug:" + password))));

        public Task<bool> IsLegacyUserAsync(MasterKey masterKey = null, string userId = null) => Task.FromResult(false);

        // A stored hash means the unlock is offline (no server call).
        public Task<string> GetMasterKeyHashAsync() => Task.FromResult("oahu-debug-hash");

        public Task<bool> CompareAndUpdateKeyHashAsync(string masterPassword, MasterKey key) =>
            Task.FromResult(masterPassword == OahuDebugLock.MasterPassword);

        public Task<UserKey> DecryptUserKeyWithMasterKeyAsync(MasterKey masterKey, EncString encUserKey = null, string userId = null) =>
            Task.FromResult(OahuDebugLock.FakeUserKey);

        public Task SetMasterKeyAsync(MasterKey masterKey, string userId = null) => Task.CompletedTask;

        public Task<bool> HasUserKeyAsync(string userId = null) => Task.FromResult(!OahuDebugLock.Locked);

        public Task SetUserKeyAsync(UserKey userKey, string userId = null)
        {
            OahuDebugLock.Locked = false;
            Console.WriteLine("OAHUDEBUG fake vault unlocked");
            return Task.CompletedTask;
        }

        public Task<UserKey> GetBiometricUnlockKeyAsync(string userId = null) => Task.FromResult(OahuDebugLock.FakeUserKey);

        // Any PIN "decrypts" the user key; the protected PIN then decrypts to the debug PIN, and LockPageViewModel
        // compares it with what was typed, as it does with a real one.
        public Task<UserKey> DecryptUserKeyWithPinAsync(string pin, string salt, KdfConfig kdfConfig, EncString pinProtectedUserKey = null) =>
            Task.FromResult(OahuDebugLock.FakeUserKey);

        public Task<UserKey> DecryptAndMigrateOldPinKeyAsync(bool masterPasswordOnRestart, string pin, string email, KdfConfig kdfConfig, EncString oldPinKey) =>
            Task.FromResult(OahuDebugLock.FakeUserKey);

        public Task<string> DecryptToUtf8Async(EncString encString, SymmetricCryptoKey key = null) =>
            ReferenceEquals(key, OahuDebugLock.FakeUserKey) && encString?.EncryptedString == OahuDebugStateService.FakePinEncString
                ? Task.FromResult(OahuDebugLock.PinCode)
                : _inner.DecryptToUtf8Async(encString, key);
    }

    /// <summary>The real platform utilities, except biometrics.</summary>
    public sealed class OahuDebugPlatformUtilsService : IPlatformUtilsService
    {
        private readonly IPlatformUtilsService _inner;

        public OahuDebugPlatformUtilsService(IPlatformUtilsService inner) => _inner = inner;

        public Task<bool> SupportsBiometricAsync() =>
            OahuDebugLock.RealBiometric ? _inner.SupportsBiometricAsync() : Task.FromResult(true);

        public Task<bool> IsBiometricIntegrityValidAsync(string bioIntegritySrcKey = null) =>
            Task.FromResult(OahuDebugLock.BiometricIntegrityValid);

        public async Task<bool?> AuthenticateBiometricAsync(string text = null, string fallbackText = null, Action fallback = null,
            bool logOutOnTooManyAttempts = false, bool allowAlternativeAuthentication = false)
        {
            if (OahuDebugLock.RealBiometric)
            {
                Console.WriteLine("OAHUDEBUG biometric: system prompt");
                return await _inner.AuthenticateBiometricAsync(text, fallbackText, fallback, logOutOnTooManyAttempts,
                    allowAlternativeAuthentication);
            }
            const string succeed = "Succeed (OAHU debug)";
            const string fail = "Fail";
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            var choice = page == null
                ? null
                : await page.DisplayActionSheet("Fake biometric prompt", "Cancel", null, succeed, fail, fallbackText ?? "Fallback");
            Console.WriteLine($"OAHUDEBUG biometric: {choice}");
            if (choice == succeed)
            {
                return true;
            }
            if (choice == fail || choice == "Cancel" || choice == null)
            {
                return false;
            }
            fallback?.Invoke();
            return false;
        }

        public string GetApplicationVersion() => _inner.GetApplicationVersion();
        public Bit.Core.Enums.DeviceType GetDevice() => _inner.GetDevice();
        public string GetDeviceString() => _inner.GetDeviceString();
        public ClientType GetClientType() => _inner.GetClientType();
        public bool IsSelfHost() => _inner.IsSelfHost();
        public bool IsViewOpen() => _inner.IsViewOpen();
        public void LaunchUri(string uri, Dictionary<string, object> options = null) => _inner.LaunchUri(uri, options);
        public Task<string> ReadFromClipboardAsync(Dictionary<string, object> options = null) => _inner.ReadFromClipboardAsync(options);
        public Task<bool> ShowDialogAsync(string text, string title = null, string confirmText = null, string cancelText = null,
            string type = null) => _inner.ShowDialogAsync(text, title, confirmText, cancelText, type);
        public Task<bool> ShowPasswordDialogAsync(string title, string body, Func<string, Task<bool>> validator) =>
            _inner.ShowPasswordDialogAsync(title, body, validator);
        public Task<(string password, bool valid)> ShowPasswordDialogAndGetItAsync(string title, string body,
            Func<string, Task<bool>> validator) => _inner.ShowPasswordDialogAndGetItAsync(title, body, validator);
        public void ShowToast(string type, string title, string text, Dictionary<string, object> options = null) =>
            _inner.ShowToast(type, title, text, options);
        public void ShowToast(string type, string title, string[] text, Dictionary<string, object> options = null) =>
            _inner.ShowToast(type, title, text, options);
        public void ShowToastForCopiedValue(string valueNameCopied) => _inner.ShowToastForCopiedValue(valueNameCopied);
        public bool SupportsFido2() => _inner.SupportsFido2();
        public bool SupportsDuo() => _inner.SupportsDuo();
        public long GetActiveTime() => _inner.GetActiveTime();
    }

    public sealed class FakeBiometricService : IBiometricService
    {
        public Task<bool> CanUseBiometricsUnlockAsync() => Task.FromResult(OahuDebugLock.Biometric);
        public Task<bool> SetupBiometricAsync(string bioIntegritySrcKey = null) => Task.FromResult(true);
        public Task<bool> IsSystemBiometricIntegrityValidAsync(string bioIntegritySrcKey = null) =>
            Task.FromResult(OahuDebugLock.BiometricIntegrityValid);
        public Task SetCanUnlockWithBiometricsAsync(bool canUnlockWithBiometrics) => Task.CompletedTask;
    }

    public sealed class FakeUserVerificationService : IUserVerificationService
    {
        public Task<bool> VerifyUser(string secret, VerificationType verificationType) =>
            Task.FromResult(secret == OahuDebugLock.MasterPassword);
        public Task<bool> VerifyMasterPasswordAsync(string masterPassword) =>
            Task.FromResult(masterPassword == OahuDebugLock.MasterPassword);
        public Task<bool> HasMasterPasswordAsync(bool checkMasterKeyHash = false) =>
            Task.FromResult(OahuDebugLock.HasMasterPassword);
    }

    public sealed class FakeDeviceTrustCryptoService : IDeviceTrustCryptoService
    {
        public Task<SymmetricCryptoKey> GetDeviceKeyAsync() => Task.FromResult<SymmetricCryptoKey>(null);
        public Task<DeviceResponse> TrustDeviceAsync() => Task.FromResult<DeviceResponse>(null);
        public Task<DeviceResponse> TrustDeviceIfNeededAsync() => Task.FromResult<DeviceResponse>(null);
        public Task RemoveTrustedDeviceAsync() => Task.CompletedTask;
        public Task<bool> GetShouldTrustDeviceAsync() => Task.FromResult(false);
        public Task SetShouldTrustDeviceAsync(bool value) => Task.CompletedTask;
        public Task<UserKey> DecryptUserKeyWithDeviceKeyAsync(string encryptedDevicePrivateKey, string encryptedUserKey) =>
            Task.FromResult<UserKey>(null);
        public Task<bool> IsDeviceTrustedAsync() => Task.FromResult(false);
    }

    /// <summary>A username generator that never calls a forwarding service: "bad" as the API key fails as a refused key does.</summary>
    public sealed class FakeUsernameGenerationService : IUsernameGenerationService
    {
        private readonly IUsernameGenerationService _inner;
        private int _alias;

        public FakeUsernameGenerationService(IUsernameGenerationService inner) => _inner = inner;

        public async Task<string> GenerateAsync(UsernameGenerationOptions options)
        {
            if (options.Type != UsernameType.ForwardedEmailAlias)
            {
                return await _inner.GenerateAsync(options);
            }
            await Task.Delay(300);
            var key = options.ServiceType switch
            {
                ForwardedEmailServiceType.AnonAddy => options.AnonAddyApiAccessToken,
                ForwardedEmailServiceType.FirefoxRelay => options.FirefoxRelayApiAccessToken,
                ForwardedEmailServiceType.SimpleLogin => options.SimpleLoginApiKey,
                ForwardedEmailServiceType.DuckDuckGo => options.DuckDuckGoApiKey,
                ForwardedEmailServiceType.Fastmail => options.FastMailApiKey,
                ForwardedEmailServiceType.ForwardEmail => options.ForwardEmailApiAccessToken,
                _ => null,
            };
            if (string.IsNullOrWhiteSpace(key) || key == "bad")
            {
                throw new Bit.Core.Exceptions.ForwardedEmailInvalidSecretException(new Exception("OAHU debug: refused API key"));
            }
            return $"alias{++_alias}@{options.ServiceType.ToString().ToLowerInvariant()}.example";
        }

        public void ClearCache() => _inner.ClearCache();
        public Task<UsernameGenerationOptions> GetOptionsAsync() => _inner.GetOptionsAsync();
        public Task SaveOptionsAsync(UsernameGenerationOptions options) => _inner.SaveOptionsAsync(options);
    }
}
#endif
