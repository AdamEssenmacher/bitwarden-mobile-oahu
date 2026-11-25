using Oahu;

namespace Bit.App.Contracts;

/// <summary>
/// Viewing one cipher (CipherDetailsPage), read-only. MAUI keeps the title, toolbar items (Close, Edit/Restore, the
/// options sheet on iOS; Attachments, Clone, Move to organization, Collections, Delete on Android), the Android
/// floating edit button, every dialog and the password re-prompt.
///
/// Hidden values (a password, a card number or code, a hidden custom field) cross to the native screen only while
/// they are shown: until then a <see cref="SecretValue"/> carries only its mask. Copying happens in C#.
/// </summary>
[OahuContract]
public interface ICipherDetailsViewModel
{
    IOahuState<CipherDetailsCopy> Copy { get; }

    IOahuState<AppPalette> Palette { get; }

    /// <summary>The cipher, or null until it has loaded.</summary>
    IOahuState<CipherDetails?> Details { get; }

    /// <summary>The verification code and its countdown, or null when the cipher has none. Changes every second.</summary>
    IOahuState<TotpDisplay?> Totp { get; }

    /// <summary>
    /// Copies a value by the page's own ids: LoginUsername, LoginPassword, LoginTotp, CardNumber, CardCode. A protected
    /// value asks for the master password first when the cipher has re-prompt on.
    /// </summary>
    void CopyValue(string id);

    void TogglePassword();

    void ToggleCardNumber();

    void ToggleCardCode();

    /// <summary>"Check if password has been exposed".</summary>
    void CheckPassword();

    /// <summary>Opens the URI at this index of <see cref="CipherDetails.Uris"/>.</summary>
    void LaunchUri(int index);

    void CopyUri(int index);

    /// <summary>Copies the custom field at this index of <see cref="CipherDetails.Fields"/>.</summary>
    void CopyField(int index);

    /// <summary>Shows or hides the hidden custom field at this index.</summary>
    void ToggleField(int index);

    void DownloadAttachment(string id);

    /// <summary>The password history link in the footer.</summary>
    void OpenPasswordHistory();
}

public sealed record CipherDetailsCopy(
    string ItemInformation,
    string Name,
    string Username,
    string Password,
    string Passkey,
    string VerificationCode,
    string PremiumRequired,
    string CardholderName,
    string Number,
    string Brand,
    string Expiration,
    string SecurityCode,
    string IdentityName,
    string Company,
    string Ssn,
    string PassportNumber,
    string LicenseNumber,
    string Email,
    string Phone,
    string Address,
    string Uris,
    string Uri,
    string Website,
    string Notes,
    string CustomFields,
    string Attachments,
    string Copy,
    string Launch,
    string Download,
    string CheckPassword,
    string Show,
    string Hide);

public sealed record CipherDetails(
    string Name,
    LoginDetails? Login,
    CardDetails? Card,
    IdentityDetails? Identity,
    IReadOnlyList<UriRow> Uris,
    string? Notes,
    IReadOnlyList<FieldRow> Fields,
    IReadOnlyList<AttachmentRow> Attachments,
    IReadOnlyList<FooterLine> Footer);

/// <summary>
/// A value that is hidden until the user reveals it. <c>Revealed</c> is null while it is hidden, so the value itself
/// is not on the native side; when shown it is split into runs of letters, digits and symbols for coloring.
/// </summary>
public sealed record SecretValue(string Masked, IReadOnlyList<TextRun>? Revealed, bool CanReveal, bool CanCopy);

public enum TextRunKind
{
    Letters,
    Digits,
    Symbols,
}

public sealed record TextRun(string Text, TextRunKind Kind);

public sealed record LoginDetails(
    string? Username,
    SecretValue? Password,
    bool CanCheckPassword,
    string? PasskeyCreated,
    bool HasTotp);

public sealed record CardDetails(
    string? CardholderName,
    SecretValue? Number,
    string? Brand,
    string? Expiration,
    SecretValue? Code);

public sealed record IdentityDetails(
    string? Name,
    string? Username,
    string? Company,
    string? Ssn,
    string? PassportNumber,
    string? LicenseNumber,
    string? Email,
    string? Phone,
    IReadOnlyList<string> Address);

/// <summary>A login URI: "Website" or "URI", what to show (the host for a website), and whether it can be opened.</summary>
public sealed record UriRow(string Label, string Value, bool CanLaunch);

public abstract record FieldRow(string Name)
{
    public sealed record TextField(string Name, string? Value, bool CanCopy) : FieldRow(Name);

    public sealed record HiddenField(string Name, SecretValue Value) : FieldRow(Name);

    public sealed record BooleanField(string Name, bool Value) : FieldRow(Name);

    /// <summary>A field linked to one of the cipher's own values ("Username", "Password").</summary>
    public sealed record LinkedField(string Name, string LinkedTo) : FieldRow(Name);
}

public sealed record AttachmentRow(string Id, string FileName, string Size);

/// <summary>A footer line ("Updated: 11/25/26 3:04 PM"); <c>Link</c> lines open the password history.</summary>
public sealed record FooterLine(string Label, string Value, bool Link);

/// <summary>The code split in two halves as MAUI shows it ("123 456"), seconds left and the countdown's 0–100 progress.</summary>
public sealed record TotpDisplay(
    string? Code,
    string Seconds,
    double Progress,
    bool Low,
    bool CanCopy,
    bool PremiumRequired);
