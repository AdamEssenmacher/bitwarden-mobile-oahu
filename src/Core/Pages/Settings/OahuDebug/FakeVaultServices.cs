#if DEBUG
// DEBUG ONLY: in-memory stand-ins for the services the vault pages use (docs/oahu/03-vault.md). Each serves the
// FakeVault and never reaches a server. Members the vault pages don't use throw NotSupportedException.
using System.Text;
using System.Text.RegularExpressions;
using Bit.App.Abstractions;
using Bit.Core.Abstractions;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Domain;
using Bit.Core.Models.Response;
using Bit.Core.Models.View;
using CollectionView = Bit.Core.Models.View.CollectionView;
using Bit.Core.Resources.Localization;
using Bit.Core.Utilities;
using Bit.Core.Services;

namespace Bit.App.Pages.OahuDebug
{
    internal static class Fake
    {
        public static NotSupportedException Unused([System.Runtime.CompilerServices.CallerMemberName] string member = "") =>
            new($"OAHU debug fake: {member} is not used by the vault pages.");
    }

    public sealed class FakeCipherService : ICipherService
    {
        private readonly FakeVault _vault;

        public FakeCipherService(FakeVault vault) => _vault = vault;

        public async Task<List<CipherView>> GetAllDecryptedAsync(Func<Cipher, bool> filter = null)
        {
            await Task.Delay(_vault.Variant == "vault-large" ? 400 : 150); // a visible load, as decrypting takes time
            var all = await _vault.GetAllDecryptedAsync();
            return filter == null ? all : all.Where(v => filter(_vault.Get(v.Id))).ToList();
        }

        public Task<List<Cipher>> GetAllAsync() => Task.FromResult(_vault.Ciphers.ToList());

        public Task<Cipher> GetAsync(string id) => Task.FromResult(_vault.Get(id));

        public async Task SoftDeleteWithServerAsync(string id)
        {
            await Task.Delay(300);
            var c = _vault.Get(id);
            if (c != null)
            {
                c.DeletedDate = DateTime.UtcNow;
                _vault.Changed();
            }
        }

        public async Task DeleteWithServerAsync(string id)
        {
            await Task.Delay(300);
            _vault.Remove(id);
        }

        public async Task RestoreWithServerAsync(string id)
        {
            await Task.Delay(300);
            var c = _vault.Get(id);
            if (c != null)
            {
                c.DeletedDate = null;
                _vault.Changed();
            }
        }

        public async Task<byte[]> DownloadAndDecryptAttachmentAsync(string cipherId, AttachmentView attachment, string organizationId)
        {
            await Task.Delay(500);
            return Encoding.UTF8.GetBytes($"Fake attachment {attachment.FileName} of {cipherId}.\n");
        }

        public Task<Cipher> EncryptAsync(CipherView model, SymmetricCryptoKey key = null, Cipher originalCipher = null)
        {
            // Step 5: everything the add/edit page can change round-trips (FakeVault.FromView), so an edit, an add and
            // a clone can be opened again and checked.
            return Task.FromResult(FakeVault.FromView(model, originalCipher ?? _vault.Get(model.Id)));
        }

        public async Task SaveWithServerAsync(Cipher cipher)
        {
            await Task.Delay(300);
            cipher.Id ??= "fake-new-" + Guid.NewGuid().ToString("N")[..8];
            _vault.Remove(cipher.Id);
            _vault.Add(cipher);
        }

        public Task UpdateLastUsedDateAsync(string id) => Task.CompletedTask;
        public Task CopyTotpCodeIfNeededAsync(CipherView cipher) => Task.CompletedTask;
        public Task ClearAsync(string userId) => Task.CompletedTask;
        public Task ClearCacheAsync() => Task.CompletedTask;
        public Task DeleteAsync(List<string> ids) => Task.CompletedTask;
        public Task DeleteAsync(string id) => Task.CompletedTask;
        public Task DeleteAttachmentAsync(string id, string attachmentId) => throw Fake.Unused();
        public Task DeleteAttachmentWithServerAsync(string id, string attachmentId) => throw Fake.Unused();
        public Task<Tuple<List<CipherView>, List<CipherView>, List<CipherView>>> GetAllDecryptedByUrlAsync(string url,
            List<CipherType> includeOtherTypes = null) => throw Fake.Unused();
        public Task<List<CipherView>> GetAllDecryptedForGroupingAsync(string groupingId, bool folder = true) => throw Fake.Unused();
        public Task<List<CipherView>> GetAllDecryptedForUrlAsync(string url) => throw Fake.Unused();
        public Task<CipherView> GetLastUsedForUrlAsync(string url) => throw Fake.Unused();
        public Task ReplaceAsync(Dictionary<string, CipherData> ciphers) => Task.CompletedTask;
        public Task<Cipher> SaveAttachmentRawWithServerAsync(Cipher cipher, CipherView cipherView, string filename, byte[] data) =>
            throw Fake.Unused();
        public Task SaveCollectionsWithServerAsync(Cipher cipher) => throw Fake.Unused();
        public Task ShareWithServerAsync(CipherView cipher, string organizationId, HashSet<string> collectionIds) => throw Fake.Unused();
        public Task UpsertAsync(CipherData cipher) => Task.CompletedTask;
        public Task UpsertAsync(List<CipherData> cipher) => Task.CompletedTask;
        public Task<string> CreateNewLoginForPasskeyAsync(Fido2ConfirmNewCredentialParams newPasskeyParams) => throw Fake.Unused();
    }

    public sealed class FakeCollectionService : ICollectionService
    {
        private readonly FakeVault _vault;

        public FakeCollectionService(FakeVault vault) => _vault = vault;

        public Task<List<CollectionView>> GetAllDecryptedAsync() =>
            Task.FromResult(_vault.Collections.Select(c => new CollectionView
            {
                Id = c.Id, OrganizationId = c.OrganizationId, Name = c.Name,
            }).OrderBy(c => c.Name).ToList());

        public async Task<List<TreeNode<CollectionView>>> GetAllNestedAsync(List<CollectionView> collections = null)
        {
            collections ??= await GetAllDecryptedAsync();
            var nodes = new List<TreeNode<CollectionView>>();
            foreach (var c in collections)
            {
                var copy = new CollectionView { Id = c.Id, OrganizationId = c.OrganizationId };
                var parts = c.Name != null ? Regex.Replace(c.Name, "^\\/+|\\/+$", string.Empty).Split('/') : new string[0];
                CoreHelpers.NestedTraverse(nodes, 0, parts, copy, null, '/');
            }
            return nodes;
        }

        public async Task<TreeNode<CollectionView>> GetNestedAsync(string id) =>
            CoreHelpers.GetTreeNodeObject(await GetAllNestedAsync(), id);

        public Task ClearAsync(string userId) => Task.CompletedTask;
        public void ClearCache() { }
        public Task<List<CollectionView>> DecryptManyAsync(List<Collection> collections) => throw Fake.Unused();
        public Task DeleteAsync(string id) => throw Fake.Unused();
        public Task<Collection> EncryptAsync(CollectionView model) => throw Fake.Unused();
        public Task<List<Collection>> GetAllAsync() => Task.FromResult(new List<Collection>());
        public Task<Collection> GetAsync(string id) => Task.FromResult<Collection>(null);
        public Task ReplaceAsync(Dictionary<string, CollectionData> collections) => Task.CompletedTask;
        public Task UpsertAsync(CollectionData collection) => Task.CompletedTask;
        public Task UpsertAsync(List<CollectionData> collection) => Task.CompletedTask;
    }

    public sealed class FakeOrganizationService : IOrganizationService
    {
        private readonly FakeVault _vault;

        public FakeOrganizationService(FakeVault vault) => _vault = vault;

        public Task<List<Organization>> GetAllAsync(string userId = null) =>
            Task.FromResult(_vault.Organizations.Select(o => new Organization(o)).ToList());

        public Task<Organization> GetAsync(string id) =>
            Task.FromResult(_vault.Organizations.Where(o => o.Id == id).Select(o => new Organization(o)).FirstOrDefault());

        public Task<Organization> GetByIdentifierAsync(string identifier) => Task.FromResult<Organization>(null);
        public Task ReplaceAsync(Dictionary<string, OrganizationData> organizations) => Task.CompletedTask;
        public Task ClearAllAsync(string userId) => Task.CompletedTask;
        public Task<OrganizationDomainSsoDetailsResponse> GetClaimedOrganizationDomainAsync(string userEmail) => throw Fake.Unused();
    }

    public sealed class FakePolicyService : IPolicyService
    {
        private readonly FakeVault _vault;

        public FakePolicyService(FakeVault vault) => _vault = vault;

        public Task<bool> PolicyAppliesToUser(PolicyType policyType, Func<Policy, bool> policyFilter = null, string userId = null) =>
            Task.FromResult(false);

        public Task<bool> ShouldShowVaultFilterAsync() => Task.FromResult(_vault.Organizations.Any());

        public void ClearCache() { }
        public Task<IEnumerable<Policy>> GetAll(PolicyType? type, string userId = null) => Task.FromResult(Enumerable.Empty<Policy>());
        public Task<Policy> FirstOrDefault(PolicyType? type, string userId = null) => Task.FromResult<Policy>(null);
        public Task Replace(Dictionary<string, PolicyData> policies, string userId = null) => Task.CompletedTask;
        public Task ClearAsync(string userId) => Task.CompletedTask;
        public Task<MasterPasswordPolicyOptions> GetMasterPasswordPolicyOptions(IEnumerable<Policy> policies = null, string userId = null) =>
            Task.FromResult<MasterPasswordPolicyOptions>(null);
        public Task<bool> EvaluateMasterPassword(int passwordStrength, string newPassword, MasterPasswordPolicyOptions enforcedPolicyOptions) =>
            Task.FromResult(true);
        public Tuple<ResetPasswordPolicyOptions, bool> GetResetPasswordPolicyOptions(IEnumerable<Policy> policies, string orgId) =>
            new(new ResetPasswordPolicyOptions(), false);
        /// <summary>The generator-policy route (step 4) sets an organization's password generator policy here.</summary>
        public PasswordGeneratorPolicyOptions GeneratorPolicy { get; set; }

        public Task<PasswordGeneratorPolicyOptions> GetPasswordGeneratorPolicyOptionsAsync() =>
            Task.FromResult(GeneratorPolicy ?? new PasswordGeneratorPolicyOptions());
    }

    /// <summary>
    /// A sync that "talks to the server" on a thread-pool thread and announces itself as the real SyncService does:
    /// "syncStarted", then "syncCompleted" with {"successfully": true}, sent from that background thread.
    /// </summary>
    public sealed class FakeSyncService : ISyncService
    {
        private readonly FakeVault _vault;
        private readonly IMessagingService _messaging;

        public FakeSyncService(FakeVault vault, IMessagingService messaging)
        {
            _vault = vault;
            _messaging = messaging;
        }

        public bool SyncInProgress { get; set; }

        public Task<bool> FullSyncAsync(bool forceSync, bool allowThrowOnError = false)
        {
            SyncInProgress = true;
            _messaging.Send("syncStarted");
            return Task.Run(async () =>
            {
                await Task.Delay(1500).ConfigureAwait(false);
                _vault.ApplyServerChanges();
                SyncInProgress = false;
                Console.WriteLine($"OAHUDEBUG sync completed on thread {Environment.CurrentManagedThreadId} " +
                    $"(main: {MainThread.IsMainThread})");
                _messaging.Send("syncCompleted", new Dictionary<string, object> { { "successfully", true } });
                return true;
            });
        }

        public Task<DateTime?> GetLastSyncAsync() => Task.FromResult<DateTime?>(DateTime.UtcNow);
        public Task SetLastSyncAsync(DateTime date) => Task.CompletedTask;
        public Task<bool> SyncDeleteCipherAsync(SyncCipherNotification notification) => Task.FromResult(false);
        public Task<bool> SyncDeleteFolderAsync(SyncFolderNotification notification) => Task.FromResult(false);
        public Task<bool> SyncDeleteSendAsync(SyncSendNotification notification) => Task.FromResult(false);
        public Task<bool> SyncUpsertCipherAsync(SyncCipherNotification notification, bool isEdit) => Task.FromResult(false);
        public Task<bool> SyncUpsertFolderAsync(SyncFolderNotification notification, bool isEdit) => Task.FromResult(false);
        public Task<bool> SyncUpsertSendAsync(SyncSendNotification notification, bool isEdit) => Task.FromResult(false);
        public Task SyncPasswordlessLoginRequestsAsync() => Task.CompletedTask;
    }

    public sealed class FakeVaultTimeoutService : IVaultTimeoutService
    {
        public long? DelayLockAndLogoutMs { get; set; }
        /// <summary>The lock routes (step 4): lock settings and state come from OahuDebugLock.</summary>
        public bool LockEnabled { get; set; }

        public Task<bool> IsLockedAsync(string userId = null) => Task.FromResult(LockEnabled && OahuDebugLock.Locked);
        public Task CheckVaultTimeoutAsync() => Task.CompletedTask;
        public Task<bool> ShouldTimeoutAsync(string userId = null) => Task.FromResult(false);
        public Task ExecuteTimeoutActionAsync(string userId = null) => Task.CompletedTask;
        public Task ClearAsync(string userId = null) => Task.CompletedTask;
        public Task<bool> ShouldLockAsync(string userId = null) => Task.FromResult(LockEnabled && OahuDebugLock.Locked);
        public Task<bool> IsLoggedOutByTimeoutAsync(string userId = null) => Task.FromResult(false);
        public Task<bool> ShouldLogOutByTimeoutAsync(string userId = null) => Task.FromResult(false);
        public Task<PinLockType> GetPinLockTypeAsync(string userId = null) =>
            Task.FromResult(LockEnabled && OahuDebugLock.Pin ? PinLockType.Persistent : PinLockType.Disabled);
        public Task<bool> IsBiometricLockSetAsync(string userId = null) => Task.FromResult(LockEnabled && OahuDebugLock.Biometric);

        public Task LockAsync(bool allowSoftLock = false, bool userInitiated = false, string userId = null)
        {
            if (LockEnabled)
            {
                // As VaultTimeoutService.LockAsync and AccountsManager.LockedAsync do: drop the key, then navigate to
                // LockPage through the app (App.Navigate replaces MainPage). The "locked" message itself can't be used:
                // AccountsManager holds the real state service, which doesn't know the fake account.
                OahuDebugLock.Locked = true;
                Console.WriteLine($"OAHUDEBUG locking (user initiated: {userInitiated})");
                MainThread.BeginInvokeOnMainThread(() => ((Bit.App.App)Application.Current).Navigate(
                    Bit.Core.Enums.NavigationTarget.Lock,
                    new Bit.App.Utilities.AccountManagement.LockNavigationParams(autoPromptBiometric: !userInitiated)));
                return Task.CompletedTask;
            }
            ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService")
                .ShowToast("info", null, "OAHU debug: Lock is not available with the fake vault.");
            return Task.CompletedTask;
        }

        public Task LogOutAsync(bool userInitiated = true, string userId = null) => Task.CompletedTask;
        public Task SetVaultTimeoutOptionsAsync(int? timeout, VaultTimeoutAction? action) => Task.CompletedTask;
        public Task<int?> GetVaultTimeout(string userId = null) => Task.FromResult<int?>(null);
        public Task<VaultTimeoutAction?> GetVaultTimeoutAction(string userId = null) => Task.FromResult<VaultTimeoutAction?>(null);
    }

    public sealed class FakeEventService : IEventService
    {
        public Task ClearEventsAsync() => Task.CompletedTask;

        public Task CollectAsync(EventType eventType, string cipherId = null, bool uploadImmediately = false)
        {
            Console.WriteLine($"OAHUDEBUG event {eventType} {cipherId}");
            return Task.CompletedTask;
        }

        public Task UploadEventsAsync() => Task.CompletedTask;
    }

    /// <summary>"Check if password has been exposed" without sending anything to haveibeenpwned.</summary>
    public sealed class FakeAuditService : IAuditService
    {
        public Task<List<BreachAccountResponse>> BreachedAccountsAsync(string username) => throw Fake.Unused();

        public async Task<int> PasswordLeakedAsync(string password)
        {
            await Task.Delay(400);
            return password.Length % 2 == 0 ? 0 : 42;
        }
    }

    /// <summary>The app's own password dialog; the debug master password is "debug".</summary>
    public sealed class FakePasswordRepromptService : IPasswordRepromptService
    {
        private readonly IPlatformUtilsService _platformUtilsService;

        public FakePasswordRepromptService(IPlatformUtilsService platformUtilsService) =>
            _platformUtilsService = platformUtilsService;

        public string[] ProtectedFields { get; } = { "LoginTotp", "LoginPassword", "H_FieldValue", "CardNumber", "CardCode" };

        public async Task<bool> PromptAndCheckPasswordIfNeededAsync(CipherRepromptType repromptType = CipherRepromptType.Password)
        {
            if (repromptType == CipherRepromptType.None)
            {
                return true;
            }
            return await _platformUtilsService.ShowPasswordDialogAsync(AppResources.PasswordConfirmation,
                AppResources.PasswordConfirmationDesc + " (OAHU debug: \"debug\")",
                password => Task.FromResult(password == "debug"));
        }

        public Task<(string password, bool valid)> ShowPasswordPromptAndGetItAsync() => throw Fake.Unused();

        public Task<bool> ShouldByPassMasterPasswordRepromptAsync() => Task.FromResult(false);
    }
}
#endif
