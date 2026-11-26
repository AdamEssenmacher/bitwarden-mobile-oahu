#nullable enable
using Bit.App.Contracts;
using Bit.App.Lists.ItemViewModels.CustomFields;
using Bit.App.Utilities;
using Bit.Core.Enums;
using Bit.Core.Models.View;
using Bit.Core.Resources.Localization;
using Oahu;

namespace Bit.App.Pages
{
    // The OAHU contract over the existing add/edit view model (docs/oahu/05-cipher-edit.md). The XAML bound two-way
    // into the mutable CipherView (Cipher.Login.Username, Cipher.Card.Number, ...); each of those is now an editable
    // whose getter and setter read and write the same path, watched through the view model's "Cipher" notification
    // (TriggerCipherChanged after a type change, a generated password or a scanned key). Pickers are editables over
    // the existing *SelectedIndex properties, so TypeChanged, OrganizationChanged and the others run as before. The URI,
    // custom field and collection lists are FromCollection adapters over the view model's own collections, keyed by
    // object identity (rows are unsaved and have no ids). SubmitAsync, the dialogs and the action sheets are unchanged.
    public partial class CipherAddEditPageViewModel : ICipherAddEditViewModel
    {
        private IOahuState<CipherAddEditCopy>? _oahuCopy;
        private IOahuState<CipherAddEditLayout>? _layout;
        private IOahuState<CipherAddEditChoices>? _choices;
        private PropertyEditable<int>? _type, _folder, _owner, _cardBrand, _cardExpMonth, _identityTitle;
        private PropertyEditable<string>? _name, _username, _password, _totp, _cardholderName, _cardNumber, _cardExpYear,
            _cardCode, _notes;
        private PropertyEditable<IdentityFields>? _identity;
        private PropertyEditable<bool>? _favorite, _reprompt;
        private CollectionKeyedList<EditUriRow>? _uriRows;
        private CollectionKeyedList<EditFieldRow>? _fieldRows;
        private CollectionKeyedList<CollectionCheckRow>? _collectionRows;
        private EventSource<FormFocus>? _focus;
        private bool _showPasswordPromptOption;

        /// <summary>The page's TOTP scan action (ScanTotp_Clicked) runs from the screen's camera buttons.</summary>
        public event Action? ScanTotpRequested;

        /// <summary>The re-prompt toggle shows only for an account with a master password (the page checks).</summary>
        public bool ShowPasswordPromptOption
        {
            get => _showPasswordPromptOption;
            set => SetProperty(ref _showPasswordPromptOption, value);
        }

        IOahuState<CipherAddEditCopy> ICipherAddEditViewModel.Copy => _oahuCopy ??= new StateSource<CipherAddEditCopy>(
            new CipherAddEditCopy(
                ItemInformation: AppResources.ItemInformation,
                Type: AppResources.Type,
                Name: AppResources.Name,
                Username: AppResources.Username,
                Password: AppResources.Password,
                Passkey: AppResources.Passkey,
                AuthenticatorKey: AppResources.AuthenticatorKey,
                SetupTotp: AppResources.SetupTotp,
                Uris: AppResources.URIs,
                Uri: AppResources.URI,
                NewUri: AppResources.NewUri,
                Miscellaneous: AppResources.Miscellaneous,
                Folder: AppResources.Folder,
                Favorite: AppResources.Favorite,
                PasswordPrompt: AppResources.PasswordPrompt,
                Notes: AppResources.Notes,
                CustomFields: AppResources.CustomFields,
                NewCustomField: AppResources.NewCustomField,
                Ownership: AppResources.Ownership,
                WhoOwnsThisItem: AppResources.WhoOwnsThisItem,
                Collections: AppResources.Collections,
                NoCollectionsToList: AppResources.NoCollectionsToList,
                PersonalOwnershipPolicyInEffect: AppResources.PersonalOwnershipPolicyInEffect,
                CardholderName: AppResources.CardholderName,
                Number: AppResources.Number,
                Brand: AppResources.Brand,
                ExpirationMonth: AppResources.ExpirationMonth,
                ExpirationYear: AppResources.ExpirationYear,
                SecurityCode: AppResources.SecurityCode,
                Title: AppResources.Title,
                FirstName: AppResources.FirstName,
                MiddleName: AppResources.MiddleName,
                LastName: AppResources.LastName,
                Company: AppResources.Company,
                Ssn: AppResources.SSN,
                PassportNumber: AppResources.PassportNumber,
                LicenseNumber: AppResources.LicenseNumber,
                Email: AppResources.Email,
                Phone: AppResources.Phone,
                Address1: AppResources.Address1,
                Address2: AppResources.Address2,
                Address3: AppResources.Address3,
                City: AppResources.CityTown,
                State: AppResources.StateProvince,
                PostalCode: AppResources.ZipPostalCode,
                Country: AppResources.Country,
                Options: AppResources.Options,
                ToggleVisibility: AppResources.ToggleVisibility,
                GenerateUsername: AppResources.GenerateUsername,
                GeneratePassword: AppResources.GeneratePassword,
                CheckPassword: AppResources.CheckPassword,
                CopyTotp: AppResources.CopyTotp,
                ScanQr: AppResources.ScanQrTitle,
                PasswordPromptHelp: AppResources.MasterPasswordRePromptHelp,
                TypeOptions: TypeOptions.Select(o => o.Key).ToList(),
                CardBrandOptions: CardBrandOptions.Select(o => o.Key).ToList(),
                CardExpMonthOptions: CardExpMonthOptions.Select(o => o.Key).ToList(),
                IdentityTitleOptions: IdentityTitleOptions.Select(o => o.Key).ToList()));

        IOahuState<AppPalette> ICipherAddEditViewModel.Palette => OahuPalette.Current;

        IOahuState<CipherAddEditLayout> ICipherAddEditViewModel.Layout => _layout ??= OahuState.FromProperty(
            this,
            [nameof(Cipher), nameof(ShowPassword), nameof(ShowCardNumber), nameof(ShowCardCode), nameof(HasCollections),
                nameof(OwnershipOptions), nameof(ShowPasswordPromptOption)],
            () => new CipherAddEditLayout(
                Kind: Cipher?.Type switch
                {
                    CipherType.Card => EditItemKind.Card,
                    CipherType.Identity => EditItemKind.Identity,
                    CipherType.SecureNote => EditItemKind.SecureNote,
                    _ => EditItemKind.Login,
                },
                ShowTypePicker: !TypeEditMode,
                ViewPassword: Cipher?.ViewPassword ?? false,
                ShowPassword: ShowPassword,
                ShowCardNumber: ShowCardNumber,
                ShowCardCode: ShowCardCode,
                HasTotp: HasTotpValue,
                AllowTotpCopy: AllowTotpCopy,
                PasskeyCreated: ShowPasskeyInfo && Cipher?.Login?.MainFido2Credential is { } passkey
                    ? string.Format(AppResources.CreatedXY, passkey.CreationDate.ToLocalTime().ToShortDateString(),
                        passkey.CreationDate.ToLocalTime().ToShortTimeString())
                    : null,
                OwnershipPolicyInEffect: OwnershipPolicyInEffect,
                ShowOwnership: ShowOwnershipOptions,
                ShowCollections: ShowCollections,
                HasCollections: HasCollections,
                ShowPasswordPrompt: ShowPasswordPromptOption,
                UrisLocked: IsFromFido2Framework,
                CanLinkFields: Cipher?.LinkedFieldOptions != null));

        // FolderOptions and OwnershipOptions are filled in LoadAsync before Cipher is set; OwnershipOptions is announced.
        IOahuState<CipherAddEditChoices> ICipherAddEditViewModel.Choices => _choices ??= OahuState.FromProperty(
            this,
            [nameof(Cipher), nameof(OwnershipOptions)],
            () => new CipherAddEditChoices(
                Folders: FolderOptions.Select(o => o.Key).ToList(),
                Owners: OwnershipOptions.Select(o => o.Key ?? string.Empty).ToList()));

        IOahuEditable<int> ICipherAddEditViewModel.Type => _type ??= OahuEditable.FromProperty(
            this, nameof(TypeSelectedIndex), () => TypeSelectedIndex, v => TypeSelectedIndex = v);

        IOahuEditable<int> ICipherAddEditViewModel.Folder => _folder ??= OahuEditable.FromProperty(
            this, nameof(FolderSelectedIndex), () => FolderSelectedIndex, v => FolderSelectedIndex = v);

        IOahuEditable<int> ICipherAddEditViewModel.Owner => _owner ??= OahuEditable.FromProperty(
            this, nameof(OwnershipSelectedIndex), () => OwnershipSelectedIndex, v => OwnershipSelectedIndex = v);

        IOahuEditable<int> ICipherAddEditViewModel.CardBrand => _cardBrand ??= OahuEditable.FromProperty(
            this, nameof(CardBrandSelectedIndex), () => CardBrandSelectedIndex, v => CardBrandSelectedIndex = v);

        IOahuEditable<int> ICipherAddEditViewModel.CardExpMonth => _cardExpMonth ??= OahuEditable.FromProperty(
            this, nameof(CardExpMonthSelectedIndex), () => CardExpMonthSelectedIndex, v => CardExpMonthSelectedIndex = v);

        IOahuEditable<int> ICipherAddEditViewModel.IdentityTitle => _identityTitle ??= OahuEditable.FromProperty(
            this, nameof(IdentityTitleSelectedIndex), () => IdentityTitleSelectedIndex, v => IdentityTitleSelectedIndex = v);

        IOahuEditable<string> ICipherAddEditViewModel.Name => _name ??= CipherText(
            nameof(ICipherAddEditViewModel.Name), () => Cipher?.Name, v => Cipher!.Name = v);

        IOahuEditable<string> ICipherAddEditViewModel.Username => _username ??= CipherText(
            nameof(ICipherAddEditViewModel.Username), () => Cipher?.Login?.Username, v => Cipher!.Login.Username = v);

        // MAUI showed a password the user may not view as a disabled, masked Entry holding the real value. Here it
        // doesn't cross at all: the screen gets a fixed mask and the edit is refused.
        IOahuEditable<string> ICipherAddEditViewModel.Password => _password ??= CipherText(
            nameof(ICipherAddEditViewModel.Password),
            () => Cipher?.ViewPassword == false ? HiddenMask(Cipher.Login?.Password) : Cipher?.Login?.Password,
            v =>
            {
                if (Cipher!.ViewPassword)
                {
                    Cipher.Login.Password = v;
                }
            });

        IOahuEditable<string> ICipherAddEditViewModel.Totp => _totp ??= CipherText(
            nameof(ICipherAddEditViewModel.Totp),
            () => Cipher?.ViewPassword == false ? HiddenMask(Cipher.Login?.Totp) : Cipher?.Login?.Totp,
            v =>
            {
                if (Cipher!.ViewPassword)
                {
                    Cipher.Login.Totp = v;
                }
            });

        IOahuEditable<string> ICipherAddEditViewModel.CardholderName => _cardholderName ??= CipherText(
            nameof(ICipherAddEditViewModel.CardholderName), () => Cipher?.Card?.CardholderName,
            v => Cipher!.Card.CardholderName = v);

        IOahuEditable<string> ICipherAddEditViewModel.CardNumber => _cardNumber ??= CipherText(
            nameof(ICipherAddEditViewModel.CardNumber), () => Cipher?.Card?.Number, v => Cipher!.Card.Number = v);

        IOahuEditable<string> ICipherAddEditViewModel.CardExpYear => _cardExpYear ??= CipherText(
            nameof(ICipherAddEditViewModel.CardExpYear), () => Cipher?.Card?.ExpYear, v => Cipher!.Card.ExpYear = v);

        IOahuEditable<string> ICipherAddEditViewModel.CardCode => _cardCode ??= CipherText(
            nameof(ICipherAddEditViewModel.CardCode), () => Cipher?.Card?.Code, v => Cipher!.Card.Code = v);

        IOahuEditable<string> ICipherAddEditViewModel.Notes => _notes ??= CipherText(
            nameof(ICipherAddEditViewModel.Notes), () => Cipher?.Notes, v => Cipher!.Notes = v);

        IOahuEditable<IdentityFields> ICipherAddEditViewModel.Identity => _identity ??= OahuEditable.FromProperty(
            this, nameof(Cipher), ReadIdentity, WriteIdentity);

        IOahuEditable<bool> ICipherAddEditViewModel.Favorite => _favorite ??= OahuEditable.FromProperty(
            this, nameof(Cipher), () => Cipher?.Favorite ?? false, v =>
            {
                if (Cipher != null)
                {
                    Cipher.Favorite = v;
                }
            });

        // MAUI's switch was OneWay with a Toggled handler in the page (PasswordPrompt_Toggled).
        IOahuEditable<bool> ICipherAddEditViewModel.Reprompt => _reprompt ??= OahuEditable.FromProperty(
            this, nameof(Cipher), () => PasswordPrompt, v =>
            {
                if (Cipher != null)
                {
                    Cipher.Reprompt = v ? CipherRepromptType.Password : CipherRepromptType.None;
                }
            });

        // LoginUriView raises nothing, so SetUriText and UriOptions call Refresh(). OahuKeys.Identity: a new URI has no
        // id, and the same object stays in the collection for the page's life.
        private CollectionKeyedList<EditUriRow> UriRows => _uriRows ??= OahuKeyedList.FromCollection(
            Uris, (LoginUriView uri) => new EditUriRow(OahuKeys.Identity(uri), uri.Uri ?? string.Empty,
                uri.Match == null ? null : _matchDetectionOptions.FirstOrDefault(o => o.Key == uri.Match).Value));

        IOahuKeyedList<EditUriRow> ICipherAddEditViewModel.Uris => UriRows;

        // The field item view models raise PropertyChanged for Field (rename), ShowHiddenValue, BooleanValue and
        // LinkedFieldOptionSelectedIndex, which FromCollection follows; a value typed into FieldView.Value raises
        // nothing, so SetFieldText calls Refresh().
        private CollectionKeyedList<EditFieldRow> FieldRows => _fieldRows ??= OahuKeyedList.FromCollection(
            Fields, (ICustomFieldItemViewModel item) => FieldRow(item));

        IOahuKeyedList<EditFieldRow> ICipherAddEditViewModel.Fields => FieldRows;

        private CollectionKeyedList<CollectionCheckRow> CollectionRows => _collectionRows ??= OahuKeyedList.FromCollection(
            Collections, (CollectionViewModel c) => new CollectionCheckRow(c.Collection.Id, c.Collection.Name, c.Checked));

        IOahuKeyedList<CollectionCheckRow> ICipherAddEditViewModel.Collections => CollectionRows;

        IOahuEvents<FormFocus> ICipherAddEditViewModel.Focus => _focus ??= new EventSource<FormFocus>();

        /// <summary>Asks the screen to focus a field (held until the screen listens).</summary>
        public void RequestFocus(FormField field, string? rowKey = null)
        {
            if (ForwardFocusToSection(field, rowKey))
            {
                return;
            }
            _ = ((ICipherAddEditViewModel)this).Focus;
            _focus!.Publish(new FormFocus(field, rowKey));
        }

        void ICipherAddEditViewModel.MoveFocus(FormFocus target) => RequestFocus(target.Field, target.RowKey);

        void ICipherAddEditViewModel.AddUri()
        {
            AddUri();
            if (Uris.LastOrDefault() is { } added)
            {
                RequestFocus(FormField.Uri, OahuKeys.Identity(added));
            }
        }

        void ICipherAddEditViewModel.SetUriText(RowText edit)
        {
            if (UriRows.TryGetItem<LoginUriView>(edit.Key, out var uri))
            {
                uri.Uri = edit.Text;
                UriRows.Refresh();
            }
        }

        Task ICipherAddEditViewModel.UriOptions(string key)
        {
            // UriOptions (async void) refreshes the rows itself once it sets Match (LoginUriView raises nothing).
            if (UriRows.TryGetItem<LoginUriView>(key, out var uri))
            {
                UriOptions(uri);
            }
            return Task.CompletedTask;
        }

        Task ICipherAddEditViewModel.AddField()
        {
            AddField();
            return Task.CompletedTask;
        }

        void ICipherAddEditViewModel.SetFieldText(RowText edit)
        {
            if (FieldRows.TryGetItem<ICustomFieldItemViewModel>(edit.Key, out var item))
            {
                if (item is HiddenCustomFieldItemViewModel hidden && !hidden.ShowViewHidden)
                {
                    return;
                }
                item.Field.Value = edit.Text;
                FieldRows.Refresh();
            }
        }

        void ICipherAddEditViewModel.SetFieldBool(RowBool edit)
        {
            if (FieldRows.TryGetItem<ICustomFieldItemViewModel>(edit.Key, out var item) &&
                item is BooleanCustomFieldItemViewModel boolean)
            {
                boolean.BooleanValue = edit.Value;
            }
        }

        void ICipherAddEditViewModel.SetFieldLinked(RowIndex edit)
        {
            if (FieldRows.TryGetItem<ICustomFieldItemViewModel>(edit.Key, out var item) &&
                item is LinkedCustomFieldItemViewModel linked)
            {
                linked.LinkedFieldOptionSelectedIndex = edit.Index;
            }
        }

        Task ICipherAddEditViewModel.ToggleFieldHidden(string key) =>
            FieldRows.TryGetItem<ICustomFieldItemViewModel>(key, out var item) &&
            item is HiddenCustomFieldItemViewModel hidden
                ? hidden.ToggleHiddenValueAsync()
                : Task.CompletedTask;

        Task ICipherAddEditViewModel.FieldOptions(string key)
        {
            if (FieldRows.TryGetItem<ICustomFieldItemViewModel>(key, out var item))
            {
                FieldOptions(item);
            }
            return Task.CompletedTask;
        }

        void ICipherAddEditViewModel.ToggleCollection(string key)
        {
            if (CollectionRows.TryGetItem<CollectionViewModel>(key, out var collection))
            {
                collection.Checked = !collection.Checked;
            }
        }

        void ICipherAddEditViewModel.TogglePassword() => TogglePassword();

        void ICipherAddEditViewModel.ToggleCardNumber() => ToggleCardNumber();

        void ICipherAddEditViewModel.ToggleCardCode() => ToggleCardCode();

        Task ICipherAddEditViewModel.GeneratePassword()
        {
            GeneratePassword();
            return Task.CompletedTask;
        }

        Task ICipherAddEditViewModel.GenerateUsername() => GenerateUsernameCommand.ExecuteAsync(null);

        Task ICipherAddEditViewModel.CheckPassword() => CheckPasswordCommand.ExecuteAsync(null);

        Task ICipherAddEditViewModel.ScanTotp()
        {
            ScanTotpRequested?.Invoke();
            return Task.CompletedTask;
        }

        Task ICipherAddEditViewModel.CopyTotp() => CopyCommand.ExecuteAsync(null);

        void ICipherAddEditViewModel.PasswordPromptHelp() => PasswordPromptHelp();

        private PropertyEditable<string> CipherText(string member, Func<string?> get, Action<string> set) =>
            OahuEditable.FromProperty(this, nameof(Cipher), () => get() ?? string.Empty, v =>
            {
#if DEBUG
                OahuDebugEditCount(member);
#endif
                if (Cipher != null)
                {
                    // MAUI's Entry wrote "" for a cleared field; keep that.
                    set(v);
                }
            });

        private static string HiddenMask(string? value) => string.IsNullOrEmpty(value) ? string.Empty : "••••••••";

        private EditFieldRow FieldRow(ICustomFieldItemViewModel item)
        {
            var key = OahuKeys.Identity(item);
            var name = item.Field.Name ?? string.Empty;
            return item switch
            {
                HiddenCustomFieldItemViewModel hidden => new EditFieldRow.Hidden(key, name,
                    hidden.ShowViewHidden ? item.Field.Value ?? string.Empty : string.Empty,
                    hidden.ShowHiddenValue, hidden.ShowViewHidden),
                BooleanCustomFieldItemViewModel boolean => new EditFieldRow.Toggle(key, name, boolean.BooleanValue),
                LinkedCustomFieldItemViewModel linked => new EditFieldRow.Linked(key, name,
                    linked.LinkedFieldOptionSelectedIndex, linked.LinkedFieldOptions.Select(o => o.Key).ToList()),
                _ => new EditFieldRow.Text(key, name, item.Field.Value ?? string.Empty),
            };
        }

        private IdentityFields ReadIdentity()
        {
            var i = Cipher?.Identity;
            return new IdentityFields(
                FirstName: i?.FirstName ?? string.Empty,
                MiddleName: i?.MiddleName ?? string.Empty,
                LastName: i?.LastName ?? string.Empty,
                Username: i?.Username ?? string.Empty,
                Company: i?.Company ?? string.Empty,
                Ssn: i?.SSN ?? string.Empty,
                PassportNumber: i?.PassportNumber ?? string.Empty,
                LicenseNumber: i?.LicenseNumber ?? string.Empty,
                Email: i?.Email ?? string.Empty,
                Phone: i?.Phone ?? string.Empty,
                Address1: i?.Address1 ?? string.Empty,
                Address2: i?.Address2 ?? string.Empty,
                Address3: i?.Address3 ?? string.Empty,
                City: i?.City ?? string.Empty,
                State: i?.State ?? string.Empty,
                PostalCode: i?.PostalCode ?? string.Empty,
                Country: i?.Country ?? string.Empty);
        }

        private void WriteIdentity(IdentityFields v)
        {
#if DEBUG
            OahuDebugEditCount(nameof(ICipherAddEditViewModel.Identity));
#endif
            var i = Cipher?.Identity;
            if (i == null)
            {
                return;
            }
            // Only the fields that changed are written, so an untouched null stays null (MAUI's binding did the same).
            static string? Keep(string? old, string value) => (old ?? string.Empty) == value ? old : value;
            i.FirstName = Keep(i.FirstName, v.FirstName);
            i.MiddleName = Keep(i.MiddleName, v.MiddleName);
            i.LastName = Keep(i.LastName, v.LastName);
            i.Username = Keep(i.Username, v.Username);
            i.Company = Keep(i.Company, v.Company);
            i.SSN = Keep(i.SSN, v.Ssn);
            i.PassportNumber = Keep(i.PassportNumber, v.PassportNumber);
            i.LicenseNumber = Keep(i.LicenseNumber, v.LicenseNumber);
            i.Email = Keep(i.Email, v.Email);
            i.Phone = Keep(i.Phone, v.Phone);
            i.Address1 = Keep(i.Address1, v.Address1);
            i.Address2 = Keep(i.Address2, v.Address2);
            i.Address3 = Keep(i.Address3, v.Address3);
            i.City = Keep(i.City, v.City);
            i.State = Keep(i.State, v.State);
            i.PostalCode = Keep(i.PostalCode, v.PostalCode);
            i.Country = Keep(i.Country, v.Country);
        }

#if DEBUG
        // G26 at form scale: how many edits cross, per member, while typing (BW_OAHU_DIAG prints OAHU's own events).
        private readonly Dictionary<string, int> _oahuDebugEdits = new();

        private void OahuDebugEditCount(string member)
        {
            _oahuDebugEdits[member] = _oahuDebugEdits.GetValueOrDefault(member) + 1;
        }

        /// <summary>DEBUG: the per-member edit counts since the page opened, for the save log.</summary>
        public string OahuDebugEditSummary() =>
            string.Join(", ", _oahuDebugEdits.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}"));

        /// <summary>DEBUG (BW_OAHU_CHURN): C# changes other rows while the user types in one.</summary>
        public void OahuDebugChurn(int step)
        {
            switch (step % 4)
            {
                case 0 when Fields.Count > 1:
                    Fields.Move(Fields.Count - 1, 0);
                    Console.WriteLine("OAHUDEBUG churn: moved the last custom field to the top");
                    break;
                case 1:
                    Uris.Insert(0, new LoginUriView { Uri = $"https://churn{step}.example.com" });
                    Console.WriteLine("OAHUDEBUG churn: inserted a URI at the top");
                    break;
                case 2 when Uris.Count > 1:
                    Uris.RemoveAt(0);
                    Console.WriteLine("OAHUDEBUG churn: removed the top URI");
                    break;
                case 3 when Fields.FirstOrDefault(f => f is TextCustomFieldItemViewModel) is { } text:
                    text.Field.Value = $"set by C# {step}";
                    FieldRows.Refresh();
                    Console.WriteLine("OAHUDEBUG churn: C# rewrote the first text field's value");
                    break;
            }
        }
#endif
    }
}
