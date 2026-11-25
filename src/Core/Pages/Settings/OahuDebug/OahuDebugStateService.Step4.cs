#if DEBUG
// DEBUG ONLY: state the generator and lock routes need (docs/oahu/04-generator-lock.md), held in memory for the fake
// account. Nothing is written to the device's storage or keychain. The PIN "keys" are placeholders that only the
// debug crypto service (OahuDebugCryptoService) recognizes.
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Domain;

namespace Bit.App.Pages.OahuDebug
{
    public sealed partial class OahuDebugStateService
    {
        // A JWT with the fake account's email, unsigned (TokenService only decodes the payload).
        private const string FakeAccessToken =
            "eyJhbGciOiJub25lIn0.eyJzdWIiOiJvYWh1LWRlYnVnLXVzZXIiLCJlbWFpbCI6ImFsaWNlQGV4YW1wbGUuY29tIiwiZW1haWxfdmVyaWZpZWQiOnRydWUsImV4cCI6NDEwMjQ0NDgwMH0.x";

        // Placeholder ciphertexts: well formed, meaningless, recognized only by OahuDebugCryptoService.
        public const string FakePinEncString = "2.AAAAAAAAAAAAAAAAAAAAAA==|AAAAAAAAAAAAAAAAAAAAAA==|AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

        private PasswordGenerationOptions _passwordOptions;
        private UsernameGenerationOptions _usernameOptions;
        private PreviousPageInfo _previousPage;
        private int _invalidUnlockAttempts;

        /// <summary>BW_OAHU_SLOWSAVE=1: saving generator options yields before it completes, as a storage write that
        /// isn't synchronous would. Without it the save completes in the setter's own call.</summary>
        public bool SlowSave { get; set; }

        public Task<PasswordGenerationOptions> GetPasswordGenerationOptionsAsync(string userId = null) =>
            Task.FromResult(_passwordOptions);

        public async Task SetPasswordGenerationOptionsAsync(PasswordGenerationOptions value, string userId = null)
        {
            if (SlowSave)
            {
                await Task.Delay(30);
            }
            _passwordOptions = value;
        }

        public Task<UsernameGenerationOptions> GetUsernameGenerationOptionsAsync(string userId = null) =>
            Task.FromResult(_usernameOptions);

        public async Task SetUsernameGenerationOptionsAsync(UsernameGenerationOptions value, string userId = null)
        {
            if (SlowSave)
            {
                await Task.Delay(30);
            }
            _usernameOptions = value;
        }

        public Task<string> GetAccessTokenAsync(string userId = null) => Task.FromResult(FakeAccessToken);
        public Task<string> GetEmailAsync(string userId = null) => Task.FromResult("alice@example.com");
        public Task<bool> IsActiveAccountAsync(string userId = null) => Task.FromResult(true);
        public Task<PendingAdminAuthRequest> GetPendingAdminAuthRequestAsync(string userId = null) =>
            Task.FromResult<PendingAdminAuthRequest>(null);

        public Task<string> GetProtectedPinAsync(string userId = null) =>
            Task.FromResult(OahuDebugLock.Pin ? FakePinEncString : null);
        public Task<EncString> GetPinKeyEncryptedUserKeyAsync(string userId = null) =>
            Task.FromResult(OahuDebugLock.Pin ? new EncString(FakePinEncString) : null);
        public Task<EncString> GetPinKeyEncryptedUserKeyEphemeralAsync(string userId = null) =>
            Task.FromResult<EncString>(null);
        public Task<string> GetPinProtectedAsync(string userId = null) => Task.FromResult<string>(null);
        public Task<EncString> GetPinProtectedKeyAsync(string userId = null) => Task.FromResult<EncString>(null);

        public Task<bool> GetBiometricLockedAsync(string userId = null) => Task.FromResult(OahuDebugLock.Locked);
        public Task SetBiometricLockedAsync(bool value, string userId = null) => Task.CompletedTask;
        public Task<bool?> GetBiometricUnlockAsync(string userId = null) => Task.FromResult<bool?>(OahuDebugLock.Biometric);

        public Task<int> GetInvalidUnlockAttemptsAsync(string userId = null) => Task.FromResult(_invalidUnlockAttempts);

        public Task SetInvalidUnlockAttemptsAsync(int? value, string userId = null)
        {
            _invalidUnlockAttempts = value ?? 0;
            Console.WriteLine($"OAHUDEBUG invalid unlock attempts: {_invalidUnlockAttempts}");
            return Task.CompletedTask;
        }

        public Task<int?> GetVaultTimeoutAsync(string userId = null) => Task.FromResult<int?>(15);
        public Task SetForcePasswordResetReasonAsync(ForcePasswordResetReason? value, string userId = null) =>
            Task.CompletedTask;

        public Task<PreviousPageInfo> GetPreviousPageInfoAsync(string userId = null) => Task.FromResult(_previousPage);

        public Task SetPreviousPageInfoAsync(PreviousPageInfo value, string userId = null)
        {
            _previousPage = value;
            return Task.CompletedTask;
        }
    }
}
#endif
