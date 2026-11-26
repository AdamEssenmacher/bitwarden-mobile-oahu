using Oahu;

namespace Bit.App.Contracts;

// Step 7 (docs/oahu/07-retest.md, "D25 trial"): two sections of the add/edit form as child contracts, each placed in
// the parent screen with a mount, as OAHU's contracts.md "Splitting a large screen into sections" describes. The parent
// (ICipherAddEditViewModel) carries the mount ids in Sections; while the ids are 0 the parent draws its own flat
// identity and URI members (the step 5 form, kept for comparison: BW_OAHU_SECTIONS=1 turns the sections on).

/// <summary>The identity section: the title picker and 17 text fields, each its own editable.</summary>
[OahuContract]
public interface IIdentitySectionViewModel
{
    IOahuState<IdentitySectionCopy> Copy { get; }

    IOahuEditable<int> Title { get; }
    IOahuEditable<string> FirstName { get; }
    IOahuEditable<string> MiddleName { get; }
    IOahuEditable<string> LastName { get; }
    IOahuEditable<string> Username { get; }
    IOahuEditable<string> Company { get; }
    IOahuEditable<string> Ssn { get; }
    IOahuEditable<string> PassportNumber { get; }
    IOahuEditable<string> LicenseNumber { get; }
    IOahuEditable<string> Email { get; }
    IOahuEditable<string> Phone { get; }
    IOahuEditable<string> Address1 { get; }
    IOahuEditable<string> Address2 { get; }
    IOahuEditable<string> Address3 { get; }
    IOahuEditable<string> City { get; }
    IOahuEditable<string> State { get; }
    IOahuEditable<string> PostalCode { get; }
    IOahuEditable<string> Country { get; }

    /// <summary>Focus the first field (the parent's Return on the name field, or the parent's own focus request).</summary>
    IOahuEvents<int> Focus { get; }
}

public sealed record IdentitySectionCopy(
    string Title,
    IReadOnlyList<string> TitleOptions,
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

/// <summary>The login's URIs as an editable list section.</summary>
[OahuContract]
public interface IUriSectionViewModel
{
    IOahuState<UriSectionCopy> Copy { get; }

    /// <summary>Saving a passkey: URIs can't be changed or added.</summary>
    IOahuState<bool> Locked { get; }

    IOahuKeyedList<EditUriRow> Uris { get; }

    /// <summary>Focus a row (a URI just added), by key.</summary>
    IOahuEvents<string> FocusRow { get; }

    void AddUri();
    void SetUriText(RowText edit);
    Task UriOptions(string key);
}

public sealed record UriSectionCopy(string Uris, string Uri, string NewUri, string Options);

/// <param name="Identity">The identity section's mount id, or 0: the parent draws its flat identity fields.</param>
/// <param name="Uris">The URI section's mount id, or 0: the parent draws its flat URI rows.</param>
public sealed record CipherAddEditSections(long Identity, long Uris);
