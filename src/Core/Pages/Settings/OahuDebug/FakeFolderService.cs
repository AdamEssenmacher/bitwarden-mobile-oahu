#if DEBUG
// DEBUG ONLY: an in-memory folder service for the OAHU folder pages' debug route (OahuDebugRoute). It never talks to
// a server and does no cryptography: "encrypted" names are base64 text whose decrypted value is preset. It exists so
// the Folders and FolderAddEdit pages can be exercised without a Bitwarden account. Never compiled into Release.
using System.Reflection;
using System.Text;
using Bit.Core.Abstractions;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Domain;
using Bit.Core.Models.View;

namespace Bit.App.Pages.OahuDebug
{
    public sealed class FakeFolderService : IFolderService
    {
        private static readonly FieldInfo DecryptedValueField =
            typeof(EncString).GetField("_decryptedValue", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("EncString._decryptedValue not found");

        private readonly List<(string Id, string Name)> _folders = new();
        private int _nextId = 1;

        public FakeFolderService(bool seed)
        {
            if (seed)
            {
                foreach (var name in new[] { "Finance", "Personal", "Shopping", "Work" })
                {
                    _folders.Add(($"fake-{_nextId++}", name));
                }
            }
        }

        /// <summary>The vault route's folders (FakeVault), with ids the fake ciphers refer to.</summary>
        public FakeFolderService(IEnumerable<(string Id, string Name)> folders) => _folders.AddRange(folders);

        public Task<List<FolderView>> GetAllDecryptedAsync()
        {
            var views = _folders
                .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(f => new FolderView { Id = f.Id, Name = f.Name })
                .ToList();
            // The real service ends the list with "No Folder" (Id null); FoldersPageViewModel removes the last item.
            views.Add(new FolderView { Id = null, Name = "No Folder" });
            return Task.FromResult(views);
        }

        public Task<Folder> GetAsync(string id)
        {
            var match = _folders.FirstOrDefault(f => f.Id == id);
            return Task.FromResult(match.Id == null ? null : new Folder { Id = match.Id, Name = FakeEncString(match.Name) });
        }

        public Task<Folder> EncryptAsync(FolderView model, SymmetricCryptoKey key = null) =>
            Task.FromResult(new Folder { Id = model.Id, Name = FakeEncString(model.Name), RevisionDate = DateTime.UtcNow });

        public async Task SaveWithServerAsync(Folder folder)
        {
            await Task.Delay(300); // a visible "Saving..." overlay, as a network call shows
            var name = (string)DecryptedValueField.GetValue(folder.Name);
            var index = _folders.FindIndex(f => f.Id == folder.Id);
            if (index >= 0)
            {
                _folders[index] = (folder.Id, name);
            }
            else
            {
                folder.Id = $"fake-{_nextId++}";
                _folders.Add((folder.Id, name));
            }
        }

        public async Task DeleteWithServerAsync(string id)
        {
            await Task.Delay(300);
            _folders.RemoveAll(f => f.Id == id);
        }

        public Task DeleteAsync(string id)
        {
            _folders.RemoveAll(f => f.Id == id);
            return Task.CompletedTask;
        }

        private static EncString FakeEncString(string name)
        {
            var enc = new EncString(EncryptionType.AesCbc256_B64,
                Convert.ToBase64String(Encoding.UTF8.GetBytes(string.IsNullOrEmpty(name) ? " " : name)));
            DecryptedValueField.SetValue(enc, name);
            return enc;
        }

        public Task ClearAsync(string userId) => Task.CompletedTask;
        public void ClearCache() { }
        public Task<List<Folder>> GetAllAsync() => throw new NotSupportedException("Not used by the folder pages.");
        // As FolderService: "Work/Clients" nests under "Work".
        public async Task<List<TreeNode<FolderView>>> GetAllNestedAsync(List<FolderView> folders = null)
        {
            folders ??= await GetAllDecryptedAsync();
            var nodes = new List<TreeNode<FolderView>>();
            foreach (var f in folders)
            {
                var copy = new FolderView { Id = f.Id, RevisionDate = f.RevisionDate };
                var parts = f.Name != null
                    ? System.Text.RegularExpressions.Regex.Replace(f.Name, "^\\/+|\\/+$", string.Empty).Split('/')
                    : new string[0];
                Bit.Core.Utilities.CoreHelpers.NestedTraverse(nodes, 0, parts, copy, null, '/');
            }
            return nodes;
        }

        public async Task<TreeNode<FolderView>> GetNestedAsync(string id) =>
            Bit.Core.Utilities.CoreHelpers.GetTreeNodeObject(await GetAllNestedAsync(), id);
        public Task ReplaceAsync(Dictionary<string, FolderData> folders) => Task.CompletedTask;
        public Task UpsertAsync(FolderData folder) => Task.CompletedTask;
        public Task UpsertAsync(List<FolderData> folder) => Task.CompletedTask;
    }
}
#endif
