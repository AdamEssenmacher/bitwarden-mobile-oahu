#nullable enable
using Bit.App.Contracts;
using Bit.Core.Models.View;
using Bit.Core.Resources.Localization;
using Oahu;

namespace Bit.App.Pages
{
    // Step 7, the D25 trial (docs/oahu/07-retest.md): the identity fields and the URI rows as child contracts with their
    // own view models, mounted by this view model and drawn by the parent screen at the mount ids it publishes in
    // Sections (OAHU contracts.md, "Splitting a large screen into sections"). The section view models read and write
    // the same CipherView and the same Uris collection as the flat members, so Save is unchanged. Without
    // MountSections (BW_OAHU_SECTIONS unset) Sections stays (0, 0) and the parent draws the flat members.
    public partial class CipherAddEditPageViewModel
    {
        private StateSource<CipherAddEditSections>? _sections;
        private IdentitySectionViewModel? _identitySection;
        private UriSectionViewModel? _uriSection;
        private OahuMount? _identityMount, _uriMount;

        IOahuState<CipherAddEditSections> ICipherAddEditViewModel.Sections => Sections;

        private StateSource<CipherAddEditSections> Sections =>
            _sections ??= new StateSource<CipherAddEditSections>(new CipherAddEditSections(0, 0));

        /// <summary>
        /// Mounts the two sections (main thread). <paramref name="mount"/> is the platform's
        /// OahuScreens.Mount&lt;T&gt;, passed as the doc's example passes it.
        /// </summary>
        public void MountSections(
            Func<IIdentitySectionViewModel, OahuMount> mountIdentity, Func<IUriSectionViewModel, OahuMount> mountUris)
        {
            if (_identityMount != null)
            {
                return;
            }
            var started = System.Diagnostics.Stopwatch.StartNew();
            _identitySection = new IdentitySectionViewModel(this);
            _identityMount = mountIdentity(_identitySection);
            var identityMs = started.Elapsed.TotalMilliseconds;
            _uriSection = new UriSectionViewModel(this);
            _uriMount = mountUris(_uriSection);
            Sections.Set(new CipherAddEditSections(_identityMount.Id, _uriMount.Id));
            Console.WriteLine($"OAHUDEBUG sections mounted: identity {_identityMount.Id} in {identityMs:0.0} ms, " +
                $"uris {_uriMount.Id}, both in {started.Elapsed.TotalMilliseconds:0.0} ms");
        }

        /// <summary>Disposes the sections' mounts after the parent screen has closed (OahuView.TornDown).</summary>
        public void DisposeSections()
        {
            _identityMount?.Dispose(() => Console.WriteLine("OAHUDEBUG identity section torn down"));
            _uriMount?.Dispose(() => Console.WriteLine("OAHUDEBUG uri section torn down"));
            _identityMount = _uriMount = null;
        }

        /// <summary>Focus requests the parent forwards into a section.</summary>
        internal bool ForwardFocusToSection(FormField field, string? rowKey)
        {
#if DEBUG
            // Step 8: BW_OAHU_CROSS=native leaves cross-section focus to the screens (the parent routes the request
            // into the section through a shared SwiftUI focus binding or a Compose focus registry), so C# forwards
            // nothing and the request goes to the parent's own Focus event.
            if (Bit.App.Pages.OahuDebug.OahuDebugRoute.Option("BW_OAHU_CROSS", "bwOahuCross") == "native")
            {
                return false;
            }
#endif
            if (field == FormField.IdentityEmail && _identitySection != null)
            {
                _identitySection.RequestFocus(8);   // the section's line index for Email
                return true;
            }
            if (field == FormField.IdentityFirstName && _identitySection != null)
            {
                _identitySection.RequestFocus(0);
                return true;
            }
            if (field == FormField.Uri && rowKey != null && _uriSection != null)
            {
                _uriSection.RequestFocus(rowKey);
                return true;
            }
            return false;
        }

        /// <summary>The URI section's rows, refreshed wherever the flat rows are (LoginUriView raises nothing).</summary>
        internal void RefreshSectionUris() => _uriSection?.Rows.Refresh();

        private sealed class IdentitySectionViewModel : IIdentitySectionViewModel
        {
            private readonly CipherAddEditPageViewModel _page;
            private readonly EventSource<int> _focus = new();

            public IdentitySectionViewModel(CipherAddEditPageViewModel page)
            {
                _page = page;
                Copy = new StateSource<IdentitySectionCopy>(new IdentitySectionCopy(
                    AppResources.Title, page.IdentityTitleOptions.Select(o => o.Key).ToList(),
                    AppResources.FirstName, AppResources.MiddleName, AppResources.LastName, AppResources.Username,
                    AppResources.Company, AppResources.SSN, AppResources.PassportNumber, AppResources.LicenseNumber,
                    AppResources.Email, AppResources.Phone, AppResources.Address1, AppResources.Address2,
                    AppResources.Address3, AppResources.CityTown, AppResources.StateProvince,
                    AppResources.ZipPostalCode, AppResources.Country));
                Title = OahuEditable.FromProperty(page, nameof(IdentityTitleSelectedIndex),
                    () => page.IdentityTitleSelectedIndex, v => page.IdentityTitleSelectedIndex = v);
                FirstName = Text(nameof(FirstName), i => i.FirstName, (i, v) => i.FirstName = v);
                MiddleName = Text(nameof(MiddleName), i => i.MiddleName, (i, v) => i.MiddleName = v);
                LastName = Text(nameof(LastName), i => i.LastName, (i, v) => i.LastName = v);
                Username = Text(nameof(Username), i => i.Username, (i, v) => i.Username = v);
                Company = Text(nameof(Company), i => i.Company, (i, v) => i.Company = v);
                Ssn = Text(nameof(Ssn), i => i.SSN, (i, v) => i.SSN = v);
                PassportNumber = Text(nameof(PassportNumber), i => i.PassportNumber, (i, v) => i.PassportNumber = v);
                LicenseNumber = Text(nameof(LicenseNumber), i => i.LicenseNumber, (i, v) => i.LicenseNumber = v);
                Email = Text(nameof(Email), i => i.Email, (i, v) => i.Email = v);
                Phone = Text(nameof(Phone), i => i.Phone, (i, v) => i.Phone = v);
                Address1 = Text(nameof(Address1), i => i.Address1, (i, v) => i.Address1 = v);
                Address2 = Text(nameof(Address2), i => i.Address2, (i, v) => i.Address2 = v);
                Address3 = Text(nameof(Address3), i => i.Address3, (i, v) => i.Address3 = v);
                City = Text(nameof(City), i => i.City, (i, v) => i.City = v);
                State = Text(nameof(State), i => i.State, (i, v) => i.State = v);
                PostalCode = Text(nameof(PostalCode), i => i.PostalCode, (i, v) => i.PostalCode = v);
                Country = Text(nameof(Country), i => i.Country, (i, v) => i.Country = v);
            }

            public IOahuState<IdentitySectionCopy> Copy { get; }
            public IOahuEditable<int> Title { get; }
            public IOahuEditable<string> FirstName { get; }
            public IOahuEditable<string> MiddleName { get; }
            public IOahuEditable<string> LastName { get; }
            public IOahuEditable<string> Username { get; }
            public IOahuEditable<string> Company { get; }
            public IOahuEditable<string> Ssn { get; }
            public IOahuEditable<string> PassportNumber { get; }
            public IOahuEditable<string> LicenseNumber { get; }
            public IOahuEditable<string> Email { get; }
            public IOahuEditable<string> Phone { get; }
            public IOahuEditable<string> Address1 { get; }
            public IOahuEditable<string> Address2 { get; }
            public IOahuEditable<string> Address3 { get; }
            public IOahuEditable<string> City { get; }
            public IOahuEditable<string> State { get; }
            public IOahuEditable<string> PostalCode { get; }
            public IOahuEditable<string> Country { get; }
            public IOahuEvents<int> Focus => _focus;

            public void RequestFocus(int line) => _focus.Publish(line);

            // Each field watches the page's "Cipher" notification, as the flat editables do, and writes one string.
            private PropertyEditable<string> Text(string member, Func<IdentityView, string?> get,
                Action<IdentityView, string> set) =>
                OahuEditable.FromProperty(_page, nameof(Cipher), () => _page.Cipher?.Identity is { } i
                    ? get(i) ?? string.Empty
                    : string.Empty, v =>
                {
#if DEBUG
                    _page.OahuDebugEditCount("Identity." + member);
#endif
                    if (_page.Cipher?.Identity is { } i && (get(i) ?? string.Empty) != v)
                    {
                        set(i, v);
                    }
                });
        }

        private sealed class UriSectionViewModel : IUriSectionViewModel
        {
            private readonly CipherAddEditPageViewModel _page;
            private readonly EventSource<string> _focus = new();

            public UriSectionViewModel(CipherAddEditPageViewModel page)
            {
                _page = page;
                Copy = new StateSource<UriSectionCopy>(new UriSectionCopy(
                    AppResources.URIs, AppResources.URI, AppResources.NewUri, AppResources.Options));
                Locked = new StateSource<bool>(page.IsFromFido2Framework);
                Rows = OahuKeyedList.FromCollection(page.Uris, (LoginUriView uri) => new EditUriRow(
                    OahuKeys.Identity(uri), uri.Uri ?? string.Empty,
                    uri.Match == null ? null : page._matchDetectionOptions.FirstOrDefault(o => o.Key == uri.Match).Value));
            }

            public IOahuState<UriSectionCopy> Copy { get; }
            public IOahuState<bool> Locked { get; }
            public CollectionKeyedList<EditUriRow> Rows { get; }
            public IOahuKeyedList<EditUriRow> Uris => Rows;
            public IOahuEvents<string> FocusRow => _focus;

            public void RequestFocus(string key) => _focus.Publish(key);

            public void AddUri()
            {
                _page.AddUri();
                if (_page.Uris.LastOrDefault() is { } added)
                {
                    RequestFocus(OahuKeys.Identity(added));
                }
            }

            public void SetUriText(RowText edit)
            {
#if DEBUG
                _page.OahuDebugEditCount("UriSection.SetUriText");
#endif
                if (Rows.TryGetItem<LoginUriView>(edit.Key, out var uri))
                {
                    uri.Uri = edit.Text;
                    Rows.Refresh();
                }
            }

            public Task UriOptions(string key)
            {
                if (Rows.TryGetItem<LoginUriView>(key, out var uri))
                {
                    _page.UriOptions(uri);
                }
                return Task.CompletedTask;
            }
        }
    }
}
