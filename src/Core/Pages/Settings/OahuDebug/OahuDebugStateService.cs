#if DEBUG
// DEBUG ONLY: the real state service with a few answers replaced, so the vault pages see a signed-in, unlocked
// account that doesn't exist (docs/oahu/03-vault.md). Every other member forwards to the real service
// (OahuDebugStateService.Generated.cs, written by gen_decorator.py from IStateService.cs). Nothing here touches keys,
// tokens or secure storage; the real account state of the device is left as it was.
using Bit.Core.Abstractions;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Domain;

namespace Bit.App.Pages.OahuDebug
{
    public sealed partial class OahuDebugStateService
    {
        public const string UserId = "oahu-debug-user";

        private readonly IStateService _inner;
        private readonly FakeVault _vault;
        private string _theme;

        /// <summary>Step 7: the theme the debug route switches to while a screen is open (BW_OAHU_THEME_SWITCH).</summary>
        public string Theme { set => _theme = value; }
        private bool _screenCaptureAllowed;

        public OahuDebugStateService(IStateService inner, FakeVault vault, string theme, bool screenCaptureAllowed)
        {
            _inner = inner;
            _vault = vault;
            _theme = theme;
            _screenCaptureAllowed = screenCaptureAllowed;
        }

        public Task<bool> IsAuthenticatedAsync(string userId = null) => Task.FromResult(true);
        public Task<string> GetActiveUserIdAsync() => Task.FromResult(UserId);
        public Task<string> GetActiveUserEmailAsync() => Task.FromResult("alice@example.com");

        public Task<T> GetActiveUserCustomDataAsync<T>(Func<Account, T> dataMapper) =>
            Task.FromResult(dataMapper(new Account(
                new Account.AccountProfile { UserId = UserId, Email = "alice@example.com", Name = "Alice Example" },
                new Account.AccountTokens())));

        public Task<bool> CanAccessPremiumAsync(string userId = null) => Task.FromResult(true);
        public Task<bool?> GetDisableFaviconAsync() => Task.FromResult<bool?>(false);

        // Pull to refresh runs a (fake) sync first, as with the app's "Sync on refresh" option on.
        public Task<bool> GetSyncOnRefreshAsync(string userId = null) => Task.FromResult(true);

        public Task<string> GetThemeAsync() => _theme != null ? Task.FromResult(_theme) : _inner.GetThemeAsync();
        public Task<string> GetAutoDarkThemeAsync() => _inner.GetAutoDarkThemeAsync();

        // No push prompt or registration for the fake account.
        public Task<bool?> GetPushInitialPromptShownAsync() => Task.FromResult<bool?>(true);
        public Task<DateTime?> GetPushLastRegistrationDateAsync(string userId = null) => Task.FromResult<DateTime?>(DateTime.UtcNow);

        public Task<ForcePasswordResetReason?> GetForcePasswordResetReasonAsync(string userId = null) =>
            Task.FromResult<ForcePasswordResetReason?>(null);

        public Task<bool> GetScreenCaptureAllowedAsync(string userId = null) => Task.FromResult(_screenCaptureAllowed);

        public Task SetScreenCaptureAllowedAsync(bool value, string userId = null)
        {
            _screenCaptureAllowed = value;
            return Task.CompletedTask;
        }

        public Task<Dictionary<string, OrganizationData>> GetOrganizationsAsync(string userId = null) =>
            Task.FromResult(_vault.Organizations.ToDictionary(o => o.Id));

        public Task SetLastActiveTimeAsync(long? value, string userId = null) => Task.CompletedTask;
    }
}
#endif
