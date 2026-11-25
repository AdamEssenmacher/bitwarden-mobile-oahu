#nullable enable
using System.Collections.Specialized;
using System.ComponentModel;
using Bit.App.Contracts;
using Bit.App.Utilities;
using Bit.Core.Resources.Localization;
using Bit.Core.Utilities;
using Oahu;

namespace Bit.App.Pages
{
    // The OAHU contract over the existing view model (docs/oahu/03-vault.md). Each member reads what the XAML page bound;
    // navigation and dialogs still run through the view model's and page's own code.
    public partial class GroupingsPageViewModel : IGroupingsViewModel
    {
        private IOahuState<GroupingsCopy>? _copy;
        private IOahuState<string?>? _vaultFilter;
        private IOahuState<GroupingsEmpty?>? _empty;
        private IOahuState<bool>? _isLoading;
        private IOahuState<bool>? _isRefreshing;
        private GroupingsRowsProjection? _rows;

        /// <summary>Raised when the native screen selects a row; the page runs its old RowSelected code.</summary>
        public event Func<IGroupingsPageListItem, Task>? ItemSelected;

        /// <summary>Raised by the empty state's "Add an Item" button; the page runs AddButton_Clicked.</summary>
        public event Action? AddRequested;

        IOahuState<GroupingsCopy> IGroupingsViewModel.Copy => _copy ??= new StateSource<GroupingsCopy>(new GroupingsCopy(
            AddAnItem: AppResources.AddAnItem,
            Filter: AppResources.FilterByVault,
            Options: AppResources.Options,
            Shared: AppResources.Shared,
            Attachments: AppResources.Attachments,
            CopyTotp: AppResources.CopyTotp,
            View: AppResources.View,
            Edit: AppResources.Edit,
            Loading: AppResources.Loading));

        IOahuState<AppPalette> IGroupingsViewModel.Palette => OahuPalette.Current;

        IOahuState<string?> IGroupingsViewModel.VaultFilter => _vaultFilter ??= OahuState.FromProperty<string?>(
            this, [nameof(ShowVaultFilter), nameof(VaultFilterDescription)],
            () => ShowVaultFilter ? VaultFilterDescription : null);

        IOahuState<GroupingsEmpty?> IGroupingsViewModel.Empty => _empty ??= OahuState.FromProperty<GroupingsEmpty?>(
            this, [nameof(ShowNoData), nameof(NoDataText), nameof(ShowAddCipherButton)],
            () => ShowNoData ? new GroupingsEmpty(NoDataText ?? string.Empty, ShowAddCipherButton) : null);

        IOahuState<bool> IGroupingsViewModel.IsLoading => _isLoading ??= OahuState.FromProperty(
            this, nameof(Loading), () => Loading);

        IOahuState<bool> IGroupingsViewModel.IsRefreshing => _isRefreshing ??= OahuState.FromProperty(
            this, nameof(Refreshing), () => Refreshing);

        private GroupingsRowsProjection RowsProjection => _rows ??= new GroupingsRowsProjection(GroupedItems);

        IOahuKeyedList<GroupingsRow> IGroupingsViewModel.Rows => RowsProjection.Rows;

        void IGroupingsViewModel.Select(string key)
        {
            if (RowsProjection.TryGetItem(key, out var item))
            {
                ItemSelected?.Invoke(item);
            }
        }

        void IGroupingsViewModel.ShowOptions(string key)
        {
            if (RowsProjection.TryGetItem(key, out var item) && item is CipherItemViewModel cipherItem)
            {
                CipherOptionsCommand.Execute(cipherItem.Cipher);
            }
        }

        void IGroupingsViewModel.Edit(string key)
        {
            if (RowsProjection.TryGetItem(key, out var item) && item is CipherItemViewModel cipherItem
                && !cipherItem.Cipher.IsDeleted)
            {
                EditCipherAsync(cipherItem.Cipher).FireAndForget();
            }
        }

        void IGroupingsViewModel.CopyTotp(string key)
        {
            if (RowsProjection.TryGetItem(key, out var item) && item is GroupingsPageTOTPListItem totpItem)
            {
                totpItem.CopyCommand.ExecuteAsync(null).FireAndForget();
            }
        }

        // What RefreshCommand did for the RefreshView. Refreshing is set before the first await, as OahuWait needs.
        // With "sync on refresh" on, LoadAsync starts a sync and returns; Refreshing stays true until the sync's
        // "syncCompleted" broadcast reloads the page.
        Task IGroupingsViewModel.Refresh()
        {
            if (Refreshing)
            {
                return Task.CompletedTask;
            }
            Refreshing = true;
            return LoadAsync();
        }

        void IGroupingsViewModel.ChooseVaultFilter() => VaultFilterCommand.Execute(null);

        void IGroupingsViewModel.Add() => AddRequested?.Invoke();

        private async Task EditCipherAsync(Core.Models.View.CipherView cipher)
        {
            if (await _passwordRepromptService.PromptAndCheckPasswordIfNeededAsync(cipher.Reprompt))
            {
                await Page.Navigation.PushModalAsync(new NavigationPage(new CipherAddEditPage(cipher.Id)));
            }
        }
    }

    /// <summary>
    /// Projects GroupedItems (header items followed by their rows, rebuilt on every load) into keyed rows. Not
    /// OahuKeyedList.FromCollection: a row's key needs its section (a cipher can be both a favorite and in "No Folder"),
    /// which the item alone doesn't know, and the items are new objects on every load, so identity keys would
    /// rebuild the whole list each time (docs/oahu/gaps.md, G15).
    /// </summary>
    internal sealed class GroupingsRowsProjection
    {
        private readonly ObservableRangeCollection<IGroupingsPageListItem> _items;
        private readonly KeyedListSource<GroupingsRow> _rows = new();
        private readonly Dictionary<string, IGroupingsPageListItem> _byKey = new(StringComparer.Ordinal);
        private readonly Dictionary<GroupingsPageTOTPListItem, string> _totpKeys = new();
        private bool _pending;

        public GroupingsRowsProjection(ObservableRangeCollection<IGroupingsPageListItem> items)
        {
            _items = items;
            _items.CollectionChanged += OnCollectionChanged;
            Rebuild();
        }

        public IOahuKeyedList<GroupingsRow> Rows => _rows;

        public bool TryGetItem(string key, out IGroupingsPageListItem item) => _byKey.TryGetValue(key, out item!);

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (MainThread.IsMainThread)
            {
                Rebuild();
                return;
            }
            // OAHU refuses list changes off the UI thread; the view model replaces GroupedItems on the main thread,
            // but be safe and coalesce a burst into one rebuild there.
            if (_pending)
            {
                return;
            }
            _pending = true;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _pending = false;
                Rebuild();
            });
        }

        private void Rebuild()
        {
            foreach (var totp in _totpKeys.Keys)
            {
                totp.PropertyChanged -= OnTotpChanged;
            }
            _totpKeys.Clear();
            _byKey.Clear();

            var rows = new List<GroupingsRow>(_items.Count);
            var section = string.Empty;
            foreach (var item in _items.ToList())
            {
                string key;
                switch (item)
                {
                    case GroupingsPageHeaderListItem header:
                        section = header.Title ?? "-";
                        key = "h|" + section;
                        break;
                    case GroupingsPageListItem group:
                        key = "g|" + GroupKey(group);
                        break;
                    case GroupingsPageTOTPListItem totp:
                        key = section + "|t|" + totp.Cipher.Id;
                        _totpKeys[totp] = key;
                        totp.PropertyChanged += OnTotpChanged;
                        break;
                    case CipherItemViewModel cipher:
                        key = section + "|c|" + cipher.Cipher.Id;
                        break;
                    default:
                        continue;
                }
                if (!_byKey.TryAdd(key, item))
                {
                    continue; // a duplicate would fault the list; MAUI would show it twice
                }
                rows.Add(Map(key, item));
            }
            _rows.Reconcile(rows);
        }

        private void OnTotpChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is GroupingsPageTOTPListItem totp && _totpKeys.TryGetValue(totp, out var key))
            {
                // One tick sets three properties; Update publishes only when the row really changed.
                _rows.Update(key, _ => Map(key, totp));
            }
        }

        private static string GroupKey(GroupingsPageListItem group)
        {
            if (group.IsTrash)
            {
                return "trash";
            }
            if (group.IsTotpCode)
            {
                return "totp";
            }
            if (group.Folder != null)
            {
                return "folder:" + (group.Folder.Id ?? "none");
            }
            if (group.Collection != null)
            {
                return "collection:" + group.Collection.Id;
            }
            return "type:" + group.Type;
        }

        private static GroupingsRow Map(string key, IGroupingsPageListItem item)
        {
            switch (item)
            {
                case GroupingsPageHeaderListItem header:
                    return new GroupingsRow.SectionHeader(key, header.Title ?? "-", header.ItemCount ?? string.Empty);
                case GroupingsPageListItem group:
                    return new GroupingsRow.Grouping(key, group.Icon ?? string.Empty, group.Name ?? string.Empty,
                        group.ItemCount ?? string.Empty);
                case GroupingsPageTOTPListItem totp:
                    return new GroupingsRow.TotpItem(key, totp.Cipher.Name ?? string.Empty, totp.Cipher.SubTitle,
                        totp.Cipher.GetIcon(totp.UsePasskeyIconAsPlaceholderFallback) ?? string.Empty,
                        totp.ShowIconImage ? totp.IconImageSource : null,
                        totp.TotpCodeFormatted, totp.TotpSec ?? string.Empty, totp.Progress);
                case CipherItemViewModel cipher:
                    return new GroupingsRow.CipherItem(key, cipher.Cipher.Name ?? string.Empty, cipher.Cipher.SubTitle,
                        cipher.Cipher.GetIcon(cipher.UsePasskeyIconAsPlaceholderFallback) ?? string.Empty,
                        cipher.ShowIconImage ? cipher.IconImageSource : null,
                        cipher.Cipher.Shared, cipher.Cipher.HasAttachments, !cipher.Cipher.IsDeleted);
                default:
                    throw new InvalidOperationException($"Unexpected list item {item.GetType().Name}.");
            }
        }
    }
}
