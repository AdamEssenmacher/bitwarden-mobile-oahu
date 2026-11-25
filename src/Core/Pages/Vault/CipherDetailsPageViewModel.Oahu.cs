#nullable enable
using Bit.App.Contracts;
using Bit.App.Lists.ItemViewModels.CustomFields;
using Bit.App.Utilities;
using Bit.Core.Enums;
using Bit.Core.Models.View;
using Bit.Core.Resources.Localization;
using Bit.Core.Utilities;
using Oahu;

namespace Bit.App.Pages
{
    // The OAHU contract over the existing view model (docs/oahu/03-vault.md). The page's XAML bound ~150 paths into a
    // mutable CipherView; here the same values are projected into one immutable record, rebuilt whenever the view
    // model announces a change (OAHU publishes it only when it differs). Hidden values cross only while shown.
    public partial class CipherDetailsPageViewModel : ICipherDetailsViewModel
    {
        private IOahuState<CipherDetailsCopy>? _copy;
        private IOahuState<CipherDetails?>? _details;
        private IOahuState<TotpDisplay?>? _totp;

        /// <summary>Raised by the footer's password history link; the page runs PasswordHistory_Tapped.</summary>
        public event Action? PasswordHistoryRequested;

        IOahuState<CipherDetailsCopy> ICipherDetailsViewModel.Copy => _copy ??= new StateSource<CipherDetailsCopy>(
            new CipherDetailsCopy(
                ItemInformation: AppResources.ItemInformation,
                Name: AppResources.Name,
                Username: AppResources.Username,
                Password: AppResources.Password,
                Passkey: AppResources.Passkey,
                VerificationCode: AppResources.VerificationCodeTotp,
                PremiumRequired: AppResources.PremiumSubscriptionRequired,
                CardholderName: AppResources.CardholderName,
                Number: AppResources.Number,
                Brand: AppResources.Brand,
                Expiration: AppResources.Expiration,
                SecurityCode: AppResources.SecurityCode,
                IdentityName: AppResources.IdentityName,
                Company: AppResources.Company,
                Ssn: AppResources.SSN,
                PassportNumber: AppResources.PassportNumber,
                LicenseNumber: AppResources.LicenseNumber,
                Email: AppResources.Email,
                Phone: AppResources.Phone,
                Address: AppResources.Address,
                Uris: AppResources.URIs,
                Uri: AppResources.URI,
                Website: AppResources.Website,
                Notes: AppResources.Notes,
                CustomFields: AppResources.CustomFields,
                Attachments: AppResources.Attachments,
                Copy: AppResources.Copy,
                Launch: AppResources.Launch,
                Download: AppResources.Download,
                CheckPassword: AppResources.CheckPassword,
                Show: AppResources.ToggleVisibility,
                Hide: AppResources.ToggleVisibility));

        IOahuState<AppPalette> ICipherDetailsViewModel.Palette => OahuPalette.Current;

        IOahuState<CipherDetails?> ICipherDetailsViewModel.Details => _details ??= OahuState.FromProperty<CipherDetails?>(
            this,
            [nameof(Cipher), nameof(ShowPassword), nameof(ShowCardNumber), nameof(ShowCardCode), nameof(Fields),
                nameof(CanAccessPremium)],
            BuildDetails);

        IOahuState<TotpDisplay?> ICipherDetailsViewModel.Totp => _totp ??= OahuState.FromProperty<TotpDisplay?>(
            this, [nameof(Cipher), nameof(TotpSec), nameof(TotpCodeFormatted), nameof(CanAccessPremium)], BuildTotp);

        void ICipherDetailsViewModel.CopyValue(string id) => CopyCommand.Execute(id);

        void ICipherDetailsViewModel.TogglePassword() => TogglePassword();

        void ICipherDetailsViewModel.ToggleCardNumber() => ToggleCardNumber();

        void ICipherDetailsViewModel.ToggleCardCode() => ToggleCardCode();

        void ICipherDetailsViewModel.CheckPassword() => CheckPasswordCommand.Execute(null);

        void ICipherDetailsViewModel.LaunchUri(int index)
        {
            if (UriAt(index) is { } uri)
            {
                LaunchUriCommand.Execute(uri);
            }
        }

        void ICipherDetailsViewModel.CopyUri(int index)
        {
            if (UriAt(index) is { } uri)
            {
                CopyUriCommand.Execute(uri);
            }
        }

        void ICipherDetailsViewModel.CopyField(int index)
        {
            if (Fields is { } fields && index >= 0 && index < fields.Count && fields[index].ShowCopyButton)
            {
                CopyFieldCommand.Execute(fields[index].Field);
            }
        }

        void ICipherDetailsViewModel.ToggleField(int index)
        {
            if (Fields is { } fields && index >= 0 && index < fields.Count
                && fields[index] is HiddenCustomFieldItemViewModel hidden)
            {
                ToggleFieldAsync(hidden).FireAndForget();
            }
        }

        void ICipherDetailsViewModel.DownloadAttachment(string id)
        {
            var attachment = Cipher?.Attachments?.FirstOrDefault(a => a.Id == id);
            if (attachment != null)
            {
                DownloadAttachmentCommand.Execute(attachment);
            }
        }

        void ICipherDetailsViewModel.OpenPasswordHistory() => PasswordHistoryRequested?.Invoke();

        private async Task ToggleFieldAsync(HiddenCustomFieldItemViewModel hidden)
        {
            await hidden.ToggleHiddenValueAsync();
            // The field's own view model raised ShowHiddenValue; the details state listens to this view model only.
            TriggerPropertyChanged(nameof(Fields));
        }

        private LoginUriView? UriAt(int index)
        {
            var uris = Cipher?.Login?.Uris;
            return uris != null && index >= 0 && index < uris.Count ? uris[index] : null;
        }

        private CipherDetails? BuildDetails()
        {
            var cipher = Cipher;
            if (cipher == null)
            {
                return null;
            }

            LoginDetails? login = null;
            CardDetails? card = null;
            IdentityDetails? identity = null;
            switch (cipher.Type)
            {
                case CipherType.Login when cipher.Login != null:
                    var l = cipher.Login;
                    login = new LoginDetails(
                        Username: NullIfEmpty(l.Username),
                        Password: string.IsNullOrEmpty(l.Password) ? null : Secret(l.MaskedPassword, l.Password,
                            ShowPassword, canReveal: cipher.ViewPassword, canCopy: cipher.ViewPassword),
                        CanCheckPassword: cipher.ViewPassword && !string.IsNullOrEmpty(l.Password),
                        PasskeyCreated: l.MainFido2Credential == null ? null : DateText(l.MainFido2Credential.CreationDate),
                        HasTotp: ShowTotp);
                    break;
                case CipherType.Card when cipher.Card != null:
                    var c = cipher.Card;
                    card = new CardDetails(
                        CardholderName: NullIfEmpty(c.CardholderName),
                        Number: string.IsNullOrEmpty(c.Number) ? null
                            : Secret(c.MaskedNumber, c.Number, ShowCardNumber, canReveal: true, canCopy: true),
                        Brand: NullIfEmpty(c.Brand),
                        Expiration: NullIfEmpty(c.Expiration),
                        Code: string.IsNullOrEmpty(c.Code) ? null
                            : Secret(c.MaskedCode, c.Code, ShowCardCode, canReveal: true, canCopy: true));
                    break;
                case CipherType.Identity when cipher.Identity != null:
                    var i = cipher.Identity;
                    var address = new List<string>();
                    if (ShowIdentityAddress)
                    {
                        foreach (var line in new[] { i.Address1, i.Address2, i.Address3, i.FullAddressPart2, i.Country })
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                address.Add(line);
                            }
                        }
                    }
                    identity = new IdentityDetails(NullIfEmpty(i.FullName), NullIfEmpty(i.Username),
                        NullIfEmpty(i.Company), NullIfEmpty(i.SSN), NullIfEmpty(i.PassportNumber),
                        NullIfEmpty(i.LicenseNumber), NullIfEmpty(i.Email), NullIfEmpty(i.Phone), address);
                    break;
            }

            var uris = ShowUris
                ? cipher.Login!.Uris.Select(u => new UriRow(u.IsWebsite ? AppResources.Website : AppResources.URI,
                    u.HostOrUri ?? string.Empty, u.CanLaunch)).ToList()
                : new List<UriRow>();

            var fields = (Fields ?? new List<ICustomFieldItemViewModel>()).Select(BuildField).ToList();

            var attachments = ShowAttachments
                ? cipher.Attachments.Select(a => new AttachmentRow(a.Id, a.FileName ?? string.Empty, a.SizeName ?? string.Empty))
                    .ToList()
                : new List<AttachmentRow>();

            var footer = new List<FooterLine>
            {
                new($"{AppResources.DateUpdated}:", DateText(cipher.RevisionDate), Link: false),
            };
            if (cipher.PasswordRevisionDisplayDate != null)
            {
                footer.Add(new($"{AppResources.DatePasswordUpdated}:", DateText(cipher.PasswordRevisionDisplayDate.Value),
                    Link: false));
            }
            if (cipher.HasPasswordHistory)
            {
                footer.Add(new($"{AppResources.PasswordHistory}:", (cipher.PasswordHistory?.Count ?? 0).ToString(),
                    Link: true));
            }

            return new CipherDetails(cipher.Name ?? string.Empty, login, card, identity, uris,
                NullIfEmpty(cipher.Notes), fields, attachments, footer);
        }

        private FieldRow BuildField(ICustomFieldItemViewModel item)
        {
            var name = item.Field?.Name ?? string.Empty;
            switch (item)
            {
                case HiddenCustomFieldItemViewModel hidden:
                    var value = hidden.Field?.Value;
                    return new FieldRow.HiddenField(name, Secret(hidden.Field?.MaskedValue ?? string.Empty,
                        value ?? string.Empty, hidden.ShowHiddenValue, canReveal: hidden.ShowViewHidden,
                        canCopy: hidden.ShowCopyButton));
                case BooleanCustomFieldItemViewModel:
                    return new FieldRow.BooleanField(name, item.Field?.BoolValue ?? false);
                case LinkedCustomFieldItemViewModel linked:
                    // ValueText starts with the link glyph from the icon font ("\ue9xx Username"); keep only the text.
                    var text = linked.ValueText ?? string.Empty;
                    var space = text.IndexOf(' ');
                    return new FieldRow.LinkedField(name, space >= 0 ? text[(space + 1)..] : text);
                default:
                    return new FieldRow.TextField(name, NullIfEmpty((item as BaseCustomFieldItemViewModel)?.ValueText ?? item.Field?.Value), item.ShowCopyButton);
            }
        }

        private TotpDisplay? BuildTotp()
        {
            var cipher = Cipher;
            if (cipher == null || !ShowTotp)
            {
                return null;
            }
            var premiumRequired = ShowUpgradePremiumTotpText;
            var seconds = TotpSec ?? string.Empty;
            var low = int.TryParse(seconds, out var s) && s < 7;
            return new TotpDisplay(premiumRequired ? null : TotpCodeFormatted, seconds, TotpProgress, low,
                CanCopy: CanAccessPremium, PremiumRequired: premiumRequired);
        }

        /// <summary>A value with its mask; the value itself only while <paramref name="shown"/>.</summary>
        private static SecretValue Secret(string? masked, string value, bool shown, bool canReveal, bool canCopy) =>
            new(masked ?? string.Empty, shown && canReveal ? OahuText.Runs(value) : null, canReveal, canCopy);

        private string DateText(DateTime date) => string.Format("{0} {1}",
            _localizeService.GetLocaleShortDate(date.ToLocalTime()), _localizeService.GetLocaleShortTime(date.ToLocalTime()));

        private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
