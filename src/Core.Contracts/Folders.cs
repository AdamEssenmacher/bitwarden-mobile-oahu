using Oahu;

namespace Bit.App.Contracts;

/// <summary>The folder list (Settings > Folders). MAUI keeps the page's title, toolbar items and navigation.</summary>
[OahuContract]
public interface IFoldersViewModel
{
    /// <summary>Static labels, localized in C# (AppResources).</summary>
    IOahuState<FoldersCopy> Copy { get; }

    /// <summary>True when there are no folders: the screen shows the empty state.</summary>
    IOahuState<bool> ShowNoData { get; }

    /// <summary>The user's folders, without "No Folder".</summary>
    IOahuKeyedList<FolderRow> Folders { get; }

    /// <summary>A row was tapped: open the folder for editing.</summary>
    void Open(string key);

    /// <summary>The add action drawn by the screen itself (Android's floating action button).</summary>
    void Add();
}

public sealed record FolderRow(string Key, string Name) : IOahuKeyed;

public sealed record FoldersCopy(string NoFolders, string AddFolder);

/// <summary>Adding or editing one folder. Save, Cancel and Delete stay MAUI toolbar items.</summary>
[OahuContract]
public interface IFolderAddEditViewModel
{
    IOahuState<FolderAddEditCopy> Copy { get; }

    /// <summary>The folder's name.</summary>
    IOahuEditable<string> Name { get; }

    /// <summary>True when adding a folder: the screen focuses the name field when it appears.</summary>
    IOahuState<bool> FocusName { get; }

    /// <summary>The keyboard's Go/Done key: the same as the Save toolbar item.</summary>
    Task Submit();
}

public sealed record FolderAddEditCopy(string NameLabel);
