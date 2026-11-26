using Oahu;

namespace Bit.App.Contracts;

/// <summary>
/// The item add/edit/clone form (CipherAddEditPage), the app's largest page. MAUI keeps the title, the toolbar items
/// (Save, Cancel on iOS, the overflow menu or Android's secondary items), every dialog and action sheet (URI and custom
/// field options, the field type and name prompts, validation errors), the loading overlay and the pages it pushes
/// (generator, scan, attachments, collections, move to organization, password history). Each value the XAML bound
/// two-way is an editable over the existing view model, so Save reads the same CipherView it always did
/// (docs/oahu/05-cipher-edit.md).
/// </summary>
[OahuContract]
public interface ICipherAddEditViewModel
{
    /// <summary>Static labels and the localized option lists of the fixed pickers.</summary>
    IOahuState<CipherAddEditCopy> Copy { get; }

    IOahuState<AppPalette> Palette { get; }

    /// <summary>Which sections and buttons show (type, view-password rights, eye states, ownership, collections).</summary>
    IOahuState<CipherAddEditLayout> Layout { get; }

    /// <summary>The folder and owner pickers' options, read when the page loads.</summary>
    IOahuState<CipherAddEditChoices> Choices { get; }

    // Pickers bound to the view model's existing *SelectedIndex properties, so its own change handlers run.
    IOahuEditable<int> Type { get; }
    IOahuEditable<int> Folder { get; }
    IOahuEditable<int> Owner { get; }
    IOahuEditable<int> CardBrand { get; }
    IOahuEditable<int> CardExpMonth { get; }
    IOahuEditable<int> IdentityTitle { get; }

    // Text fields, each over the CipherView path the XAML bound.
    IOahuEditable<string> Name { get; }
    IOahuEditable<string> Username { get; }
    IOahuEditable<string> Password { get; }
    IOahuEditable<string> Totp { get; }
    IOahuEditable<string> CardholderName { get; }
    IOahuEditable<string> CardNumber { get; }
    IOahuEditable<string> CardExpYear { get; }
    IOahuEditable<string> CardCode { get; }
    IOahuEditable<string> Notes { get; }

    /// <summary>The identity section's 17 text fields as one record: one editable for the whole section.</summary>
    IOahuEditable<IdentityFields> Identity { get; }

    IOahuEditable<bool> Favorite { get; }
    IOahuEditable<bool> Reprompt { get; }

    /// <summary>The login's URIs, in order. Rows are keyed by the LoginUriView object (saved or not).</summary>
    IOahuKeyedList<EditUriRow> Uris { get; }

    /// <summary>The custom fields, in order, keyed by their item view model.</summary>
    IOahuKeyedList<EditFieldRow> Fields { get; }

    /// <summary>The collections checklist of the chosen organization (add and clone only).</summary>
    IOahuKeyedList<CollectionCheckRow> Collections { get; }

    /// <summary>
    /// Focus a field: the name when a new item opens, or the field Save found invalid (one-shot, from C#).
    /// </summary>
    IOahuEvents<FormFocus> Focus { get; }

    /// <summary>
    /// Step 7, D25 trial: the mount ids of the identity and URI sections, or 0 while the flat members are drawn
    /// (CipherAddEditSections.cs).
    /// </summary>
    IOahuState<CipherAddEditSections> Sections { get; }

    /// <summary>
    /// Step 7, D25 trial: the parent screen's Return moving focus into a mounted section. A screen can't reach a
    /// section's fields, so the request goes through C#, which forwards it to the section's own focus event.
    /// </summary>
    void MoveFocus(FormFocus target);

    // Rows.
    void AddUri();
    void SetUriText(RowText edit);
    Task UriOptions(string key);
    Task AddField();
    void SetFieldText(RowText edit);
    void SetFieldBool(RowBool edit);
    void SetFieldLinked(RowIndex edit);
    Task ToggleFieldHidden(string key);
    Task FieldOptions(string key);
    void ToggleCollection(string key);

    // Buttons.
    void TogglePassword();
    void ToggleCardNumber();
    void ToggleCardCode();
    Task GeneratePassword();
    Task GenerateUsername();
    Task CheckPassword();
    Task ScanTotp();
    Task CopyTotp();
    void PasswordPromptHelp();
}

public sealed record RowText(string Key, string Text);

public sealed record RowBool(string Key, bool Value);

public sealed record RowIndex(string Key, int Index);

/// <param name="Field">Which field.</param>
/// <param name="RowKey">For a URI or custom field, the row's key.</param>
public sealed record FormFocus(FormField Field, string? RowKey);

public enum FormField { Name, Username, Password, Totp, CardholderName, IdentityFirstName, Notes, Uri, CustomField }

public enum EditItemKind { Login, Card, Identity, SecureNote }

/// <param name="Kind">The item type (sections follow it).</param>
/// <param name="ShowTypePicker">Only a new item (not edit, clone or a passkey save) can change its type.</param>
/// <param name="ViewPassword">The user may see and change the password and TOTP key (false: masked, disabled).</param>
/// <param name="PasskeyCreated">"Created …" of the login's passkey, or null.</param>
/// <param name="UrisLocked">Saving a passkey: URIs can't be changed or added.</param>
/// <param name="CanLinkFields">The item type has linked-field targets (the "Linked" field type is offered).</param>
public sealed record CipherAddEditLayout(
    EditItemKind Kind,
    bool ShowTypePicker,
    bool ViewPassword,
    bool ShowPassword,
    bool ShowCardNumber,
    bool ShowCardCode,
    bool HasTotp,
    bool AllowTotpCopy,
    string? PasskeyCreated,
    bool OwnershipPolicyInEffect,
    bool ShowOwnership,
    bool ShowCollections,
    bool HasCollections,
    bool ShowPasswordPrompt,
    bool UrisLocked,
    bool CanLinkFields);

public sealed record CipherAddEditChoices(IReadOnlyList<string> Folders, IReadOnlyList<string> Owners);

/// <summary>The identity section. Strings are never null here (null in the CipherView is "").</summary>
public sealed record IdentityFields(
    string FirstName,
    string MiddleName,
    string LastName,
    string Username,
    string Company,
    string Ssn,
    string PassportNumber,
    string LicenseNumber,
    string Email,
    string Phone,
    string Address1,
    string Address2,
    string Address3,
    string City,
    string State,
    string PostalCode,
    string Country);

/// <param name="Match">The match detection label, or null for the default.</param>
public sealed record EditUriRow(string Key, string Uri, string? Match) : IOahuKeyed;

/// <summary>A custom field row, by its type.</summary>
public abstract record EditFieldRow(string Key) : IOahuKeyed
{
    public sealed record Text(string Key, string Name, string Value) : EditFieldRow(Key);

    /// <param name="Value">The value, or "" when the user may not see it (CanReveal false).</param>
    public sealed record Hidden(string Key, string Name, string Value, bool Shown, bool CanReveal) : EditFieldRow(Key);

    public sealed record Toggle(string Key, string Name, bool Value) : EditFieldRow(Key);

    /// <param name="Selected">Index into Options.</param>
    public sealed record Linked(string Key, string Name, int Selected, IReadOnlyList<string> Options) : EditFieldRow(Key);
}

public sealed record CollectionCheckRow(string Key, string Name, bool Checked) : IOahuKeyed;

public sealed record CipherAddEditCopy(
    string ItemInformation,
    string Type,
    string Name,
    string Username,
    string Password,
    string Passkey,
    string AuthenticatorKey,
    string SetupTotp,
    string Uris,
    string Uri,
    string NewUri,
    string Miscellaneous,
    string Folder,
    string Favorite,
    string PasswordPrompt,
    string Notes,
    string CustomFields,
    string NewCustomField,
    string Ownership,
    string WhoOwnsThisItem,
    string Collections,
    string NoCollectionsToList,
    string PersonalOwnershipPolicyInEffect,
    string CardholderName,
    string Number,
    string Brand,
    string ExpirationMonth,
    string ExpirationYear,
    string SecurityCode,
    string Title,
    string FirstName,
    string MiddleName,
    string LastName,
    string Company,
    string Ssn,
    string PassportNumber,
    string LicenseNumber,
    string Email,
    string Phone,
    string Address1,
    string Address2,
    string Address3,
    string City,
    string State,
    string PostalCode,
    string Country,
    string Options,
    string ToggleVisibility,
    string GenerateUsername,
    string GeneratePassword,
    string CheckPassword,
    string CopyTotp,
    string ScanQr,
    string PasswordPromptHelp,
    IReadOnlyList<string> TypeOptions,
    IReadOnlyList<string> CardBrandOptions,
    IReadOnlyList<string> CardExpMonthOptions,
    IReadOnlyList<string> IdentityTitleOptions);
