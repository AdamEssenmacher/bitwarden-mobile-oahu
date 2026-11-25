#nullable enable
using Bit.App.Contracts;
using Bit.Core.Models.View;
using Bit.Core.Resources.Localization;
using Oahu;

namespace Bit.App.Pages
{
    // The OAHU contract over the existing view model (docs/oahu/02-folders.md). Nothing here changes what the MAUI
    // bindings see: each member reads the properties the XAML page used.
    public partial class FoldersPageViewModel : IFoldersViewModel
    {
        private IOahuState<FoldersCopy>? _copy;
        private IOahuState<bool>? _showNoDataState;
        private CollectionKeyedList<FolderRow>? _folderRows;

        /// <summary>Raised when the native screen asks to open a folder; the page navigates, as its row tap did.</summary>
        public event Action<FolderView>? FolderSelected;

        /// <summary>Raised by the native screen's own add action (Android's floating button).</summary>
        public event Action? AddRequested;

        IOahuState<FoldersCopy> IFoldersViewModel.Copy => _copy ??=
            new StateSource<FoldersCopy>(new FoldersCopy(AppResources.NoFoldersToList, AppResources.AddFolder));

        IOahuState<bool> IFoldersViewModel.ShowNoData => _showNoDataState ??=
            OahuState.FromProperty(this, nameof(ShowNoData), () => ShowNoData);

        private CollectionKeyedList<FolderRow> FolderRows => _folderRows ??= OahuKeyedList.FromCollection(
            this, nameof(Folders), () => Folders,
            folder => new FolderRow(OahuKeys.IdOrIdentity(folder, folder.Id), folder.Name ?? string.Empty));

        IOahuKeyedList<FolderRow> IFoldersViewModel.Folders => FolderRows;

        void IFoldersViewModel.Open(string key)
        {
            if (FolderRows.TryGetItem<FolderView>(key, out var folder))
            {
                FolderSelected?.Invoke(folder);
            }
        }

        void IFoldersViewModel.Add() => AddRequested?.Invoke();
    }
}
