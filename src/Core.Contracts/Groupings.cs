using Oahu;

namespace Bit.App.Contracts;

/// <summary>
/// The vault list (GroupingsPage): the main "My Vault" tab and every drill-down (a type, a folder, a collection, the
/// trash, verification codes), one view model per pushed page. MAUI keeps the page's title, toolbar items, the
/// Android floating button, the account switcher overlay, search and every dialog (docs/oahu/03-vault.md).
/// </summary>
[OahuContract]
public interface IGroupingsViewModel
{
    /// <summary>Static labels and accessibility text, localized in C# (AppResources).</summary>
    IOahuState<GroupingsCopy> Copy { get; }

    /// <summary>The app theme's colors.</summary>
    IOahuState<AppPalette> Palette { get; }

    /// <summary>The vault filter bar ("Vault: All"), or null when the account has no organizations.</summary>
    IOahuState<string?> VaultFilter { get; }

    /// <summary>The empty state, or null while there are rows to show.</summary>
    IOahuState<GroupingsEmpty?> Empty { get; }

    /// <summary>True while a load runs (the list may be stale); the screen keeps the list on screen.</summary>
    IOahuState<bool> IsLoading { get; }

    /// <summary>Pull to refresh is running (it may run a sync first, so it can stay true after Refresh returns).</summary>
    IOahuState<bool> IsRefreshing { get; }

    /// <summary>
    /// The list, flattened: each section is a <see cref="GroupingsRow.SectionHeader"/> followed by its rows. A cipher can
    /// appear in two sections (Favorites and No Folder), so a row's key is its section's id plus the item's id.
    /// </summary>
    IOahuKeyedList<GroupingsRow> Rows { get; }

    /// <summary>A row was tapped: open the cipher, or push the group (type, folder, collection, trash, codes).</summary>
    void Select(string key);

    /// <summary>A cipher row's "more" button: MAUI's options action sheet.</summary>
    void ShowOptions(string key);

    /// <summary>A cipher row's context menu: open the cipher for editing.</summary>
    void Edit(string key);

    /// <summary>A verification code row's copy button.</summary>
    void CopyTotp(string key);

    /// <summary>Pull to refresh. Sets <see cref="IsRefreshing"/> before its first await.</summary>
    Task Refresh();

    /// <summary>The vault filter's menu button: MAUI's "Filter items by vault" action sheet.</summary>
    void ChooseVaultFilter();

    /// <summary>The empty state's "Add an Item" button.</summary>
    void Add();
}

public sealed record GroupingsCopy(
    string AddAnItem,
    string Filter,
    string Options,
    string Shared,
    string Attachments,
    string CopyTotp,
    string View,
    string Edit,
    string Loading);

public sealed record GroupingsEmpty(string Text, bool ShowAddButton);

/// <summary>Glyphs are characters of Bitwarden's icon font (bwi-font.ttf, BitwardenIcons in C#).</summary>
public abstract record GroupingsRow(string Key) : IOahuKeyed
{
    /// <summary>A section's header: its title (upper case on iOS, as MAUI draws it) and item count.</summary>
    public sealed record SectionHeader(string Key, string Title, string Count) : GroupingsRow(Key);

    /// <summary>A type, folder, collection, the trash or the verification codes entry, with its count.</summary>
    public sealed record Grouping(string Key, string Glyph, string Name, string Count) : GroupingsRow(Key);

    /// <summary>A cipher. <c>IconUrl</c> is the website icon (null when there is none or icons are off).</summary>
    public sealed record CipherItem(
        string Key,
        string Name,
        string? Subtitle,
        string Glyph,
        string? IconUrl,
        bool Shared,
        bool HasAttachments,
        bool CanEdit) : GroupingsRow(Key);

    /// <summary>A login with a verification code (the "Verification codes" drill-down), ticking every second.</summary>
    public sealed record TotpItem(
        string Key,
        string Name,
        string? Subtitle,
        string Glyph,
        string? IconUrl,
        string? Code,
        string Seconds,
        double Progress) : GroupingsRow(Key);
}
