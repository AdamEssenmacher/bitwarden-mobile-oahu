using Oahu;

namespace Bit.App.Contracts;

/// <summary>
/// The password and username generator (GeneratorPage), as the Generator tab and as the modal that add/edit opens to
/// pick a value. MAUI keeps the title, the toolbar items (Cancel and Select in the modal, password history or the
/// options menu on the tab) and every dialog. Each option is an editable value over the existing view-model property,
/// so the view model applies the password policy and the screen shows what it decided (docs/oahu/04-generator-lock.md).
/// </summary>
[OahuContract]
public interface IGeneratorViewModel
{
    /// <summary>Static labels and the localized option lists of the pickers.</summary>
    IOahuState<GeneratorCopy> Copy { get; }

    IOahuState<AppPalette> Palette { get; }

    /// <summary>What the screen shows: which sections, the policy banner, labels that depend on the choices.</summary>
    IOahuState<GeneratorLayout> Layout { get; }

    /// <summary>Which options the organization's password policy fixes; those controls are disabled.</summary>
    IOahuState<GeneratorPolicyLocks> Locks { get; }

    /// <summary>The generated password or username, in runs of letters, digits and symbols for coloring.</summary>
    IOahuState<IReadOnlyList<TextRun>> Generated { get; }

    // Password and passphrase options.
    IOahuEditable<GeneratorKind> Kind { get; }
    IOahuEditable<PasswordKind> PasswordType { get; }
    IOahuEditable<int> Length { get; }
    IOahuEditable<bool> Uppercase { get; }
    IOahuEditable<bool> Lowercase { get; }
    IOahuEditable<bool> Numbers { get; }
    IOahuEditable<bool> Special { get; }
    IOahuEditable<int> MinNumbers { get; }
    IOahuEditable<int> MinSpecial { get; }
    IOahuEditable<bool> AvoidAmbiguous { get; }
    IOahuEditable<int> NumberOfWords { get; }
    IOahuEditable<string> WordSeparator { get; }
    IOahuEditable<bool> Capitalize { get; }
    IOahuEditable<bool> IncludeNumber { get; }

    // Username options.
    IOahuEditable<UsernameKind> UsernameType { get; }
    IOahuEditable<string> PlusAddressedEmail { get; }
    IOahuEditable<EmailKind> PlusAddressedEmailType { get; }
    IOahuEditable<string> CatchAllDomain { get; }
    IOahuEditable<EmailKind> CatchAllEmailType { get; }
    IOahuEditable<ForwardedService> Service { get; }
    IOahuEditable<string> ApiSecret { get; }
    IOahuEditable<string> AddyIoDomain { get; }
    IOahuEditable<string> ForwardEmailDomain { get; }
    IOahuEditable<bool> CapitalizeRandomWord { get; }
    IOahuEditable<bool> IncludeNumberRandomWord { get; }

    /// <summary>The value was copied: the screen plays a haptic (MAUI's toast still shows).</summary>
    IOahuEvents<Unit> Copied { get; }

    /// <summary>The copy button next to the value.</summary>
    Task CopyValue();

    /// <summary>The regenerate button next to the value (password or username).</summary>
    Task Regenerate();

    /// <summary>The length slider was released: save the options (MAUI's DragCompleted).</summary>
    Task LengthEditingEnded();

    /// <summary>The eye button of the forwarded-email API key.</summary>
    void ToggleApiSecret();

    /// <summary>The help button next to "Username type".</summary>
    void ShowUsernameTypeHelp();
}

public enum GeneratorKind { Password, Username }

public enum PasswordKind { Password, Passphrase }

public enum UsernameKind { PlusAddressedEmail, CatchAllEmail, ForwardedEmailAlias, RandomWord }

public enum EmailKind { Random, Website }

public enum ForwardedService { None, AnonAddy, FirefoxRelay, SimpleLogin, DuckDuckGo, Fastmail, ForwardEmail }

// Contract records can't be generic, so each picker has its own option record (docs/oahu/gaps.md, G25).
public sealed record GeneratorKindOption(GeneratorKind Value, string Label);
public sealed record PasswordKindOption(PasswordKind Value, string Label);
public sealed record UsernameKindOption(UsernameKind Value, string Label);
public sealed record EmailKindOption(EmailKind Value, string Label);
public sealed record ForwardedServiceOption(ForwardedService Value, string Label);

public sealed record GeneratorCopy(
    string WhatToGenerate,
    string Options,
    string PolicyInEffect,
    string PasswordType,
    string Length,
    string UppercaseLabel,
    string UppercaseDescription,
    string LowercaseLabel,
    string LowercaseDescription,
    string NumbersLabel,
    string NumbersDescription,
    string SpecialLabel,
    string SpecialDescription,
    string MinNumbers,
    string MinSpecial,
    string AvoidAmbiguous,
    string NumberOfWords,
    string WordSeparator,
    string Capitalize,
    string IncludeNumber,
    string UsernameType,
    string UsernameTypeHelp,
    string EmailRequired,
    string EmailType,
    string Website,
    string DomainRequired,
    string Service,
    string CopyPassword,
    string CopyUsername,
    string GeneratePassword,
    string GenerateUsername,
    string ToggleVisibility,
    IReadOnlyList<GeneratorKindOption> Kinds,
    IReadOnlyList<PasswordKindOption> PasswordKinds,
    IReadOnlyList<UsernameKindOption> UsernameKinds,
    IReadOnlyList<EmailKindOption> EmailKinds,
    IReadOnlyList<ForwardedServiceOption> Services);

/// <param name="ShowKindPicker">"What would you like to generate?": only on the tab.</param>
/// <param name="IsUsername">Username options (the tab's choice, or the modal add/edit opens for a username).</param>
/// <param name="EmailWebsite">The add/edit item's website, shown under "Website" when the email type can use it.</param>
/// <param name="ApiSecretLabel">"API access token" or "API key (required)", by service; null for none.</param>
public sealed record GeneratorLayout(
    bool ShowKindPicker,
    bool IsUsername,
    bool PolicyInEffect,
    string UsernameTypeDescription,
    bool ShowEmailType,
    string? EmailWebsite,
    string? ApiSecretLabel,
    bool ShowApiSecret);

public sealed record GeneratorPolicyLocks(
    bool Uppercase,
    bool Lowercase,
    bool Numbers,
    bool Special,
    bool Capitalize,
    bool IncludeNumber);
