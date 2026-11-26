#nullable enable
using Bit.App.Contracts;
using Bit.Core.Resources.Localization;
using Oahu;

namespace Bit.App.Pages
{
    // The OAHU contract over the existing view model (docs/oahu/02-folders.md). The MAUI page bound its Entry to
    // Folder.Name; FolderView raises no PropertyChanged, so the adapter watches Folder (replaced by LoadAsync) and reads
    // the name back after each edit.
    public partial class FolderAddEditPageViewModel : IFolderAddEditViewModel
    {
        private IOahuState<FolderAddEditCopy>? _copy;
        private PropertyEditable<string>? _name;
        private IOahuState<bool>? _focusName;

        IOahuState<FolderAddEditCopy> IFolderAddEditViewModel.Copy => _copy ??=
            new StateSource<FolderAddEditCopy>(new FolderAddEditCopy(AppResources.Name));

        IOahuEditable<string> IFolderAddEditViewModel.Name => _name ??= OahuEditable.FromProperty(
            this, nameof(Folder), () => Folder?.Name ?? string.Empty, value =>
            {
                if (Folder != null)
                {
                    Folder.Name = value;
                }
            });

        // EditMode follows FolderId, which the page sets once, before the screen exists.
        IOahuState<bool> IFolderAddEditViewModel.FocusName => _focusName ??= new StateSource<bool>(!EditMode);

        IOahuState<AppPalette> IFolderAddEditViewModel.Palette => Bit.App.Utilities.OahuPalette.Current;

        Task IFolderAddEditViewModel.Submit() => SubmitAsync();
    }
}
