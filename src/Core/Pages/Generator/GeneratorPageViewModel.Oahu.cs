#nullable enable
using Bit.App.Contracts;
using Bit.App.Utilities;
using Bit.Core.Enums;
using Bit.Core.Models.Domain;
using Bit.Core.Resources.Localization;
using Oahu;

namespace Bit.App.Pages
{
    // The OAHU contract over the existing generator view model (docs/oahu/04-generator-lock.md). Every option is an
    // editable over the property the MAUI control was bound to: the setter saves the options, the password service
    // normalizes them against the policy, and LoadFromOptions writes the normalized values back, so the native control
    // shows what the view model decided once the setter returns (or later, when saving yields: G27).
    public partial class GeneratorPageViewModel : IGeneratorViewModel
    {
        private IOahuState<GeneratorCopy>? _oahuCopy;
        private IOahuState<GeneratorLayout>? _layout;
        private IOahuState<GeneratorPolicyLocks>? _locks;
        private IOahuState<IReadOnlyList<TextRun>>? _generated;
        private readonly EventSource<Unit> _copied = new();
        private readonly Dictionary<string, object> _editables = new();

        IOahuState<GeneratorCopy> IGeneratorViewModel.Copy => _oahuCopy ??= new StateSource<GeneratorCopy>(new GeneratorCopy(
            WhatToGenerate: AppResources.WhatWouldYouLikeToGenerate,
            Options: AppResources.Options,
            PolicyInEffect: AppResources.PasswordGeneratorPolicyInEffect,
            PasswordType: AppResources.PasswordType,
            Length: AppResources.Length,
            UppercaseLabel: "A-Z",
            UppercaseDescription: AppResources.UppercaseAtoZ,
            LowercaseLabel: "a-z",
            LowercaseDescription: AppResources.LowercaseAtoZ,
            NumbersLabel: "0-9",
            NumbersDescription: AppResources.NumbersZeroToNine,
            SpecialLabel: "!@#$%^&*",
            SpecialDescription: AppResources.SpecialCharacters,
            MinNumbers: AppResources.MinNumbers,
            MinSpecial: AppResources.MinSpecial,
            AvoidAmbiguous: AppResources.AvoidAmbiguousCharacters,
            NumberOfWords: AppResources.NumberOfWords,
            WordSeparator: AppResources.WordSeparator,
            Capitalize: AppResources.Capitalize,
            IncludeNumber: AppResources.IncludeNumber,
            UsernameType: AppResources.UsernameType,
            // The XAML used {u:I18n UsernamePromptHelpLink}, a key AppResources does not have (MAUI showed the key).
            UsernameTypeHelp: AppResources.LearnMore,
            EmailRequired: AppResources.EmailRequiredParenthesis,
            EmailType: AppResources.EmailType,
            Website: AppResources.Website,
            DomainRequired: AppResources.DomainNameRequiredParenthesis,
            Service: AppResources.Service,
            CopyPassword: AppResources.CopyPassword,
            CopyUsername: AppResources.CopyUsername,
            GeneratePassword: AppResources.GeneratePassword,
            GenerateUsername: AppResources.GenerateUsername,
            ToggleVisibility: AppResources.ToggleVisibility,
            Kinds: GeneratorTypeOptions.Select(o => new GeneratorKindOption(Map<GeneratorKind>(o), Label(o))).ToList(),
            PasswordKinds: PasswordTypeOptions.Select((label, i) => new PasswordKindOption((PasswordKind)i, label)).ToList(),
            UsernameKinds: UsernameTypeOptions.Select(o => new UsernameKindOption(Map<UsernameKind>(o), Label(o))).ToList(),
            EmailKinds: UsernameEmailTypeOptions.Select(o => new EmailKindOption(Map<EmailKind>(o), Label(o))).ToList(),
            Services: ForwardedEmailServiceTypeOptions
                .Select(o => new ForwardedServiceOption(Map<ForwardedService>(o), Label(o))).ToList()));

        IOahuState<AppPalette> IGeneratorViewModel.Palette => OahuPalette.Current;

        IOahuState<GeneratorLayout> IGeneratorViewModel.Layout => _layout ??= OahuState.FromProperty(
            this,
            [nameof(ShowTypePicker), nameof(IsUsername), nameof(EnforcedPolicyOptions), nameof(UsernameTypeSelected),
                nameof(UsernameTypeDescriptionLabel), nameof(EmailWebsite), nameof(EditMode),
                nameof(ForwardedEmailServiceSelected), nameof(ShowForwardedEmailApiSecret)],
            () => new GeneratorLayout(
                ShowKindPicker: ShowTypePicker,
                IsUsername: IsUsername,
                PolicyInEffect: IsPolicyInEffect,
                UsernameTypeDescription: UsernameTypeDescriptionLabel,
                ShowEmailType: ShowUsernameEmailType,
                EmailWebsite: EmailWebsite,
                ApiSecretLabel: ForwardedEmailApiSecretLabel,
                ShowApiSecret: ShowForwardedEmailApiSecret));

        IOahuState<GeneratorPolicyLocks> IGeneratorViewModel.Locks => _locks ??= OahuState.FromProperty(
            this, nameof(EnforcedPolicyOptions), () =>
            {
                var p = EnforcedPolicyOptions ?? new PasswordGeneratorPolicyOptions();
                return new GeneratorPolicyLocks(p.UseUppercase, p.UseLowercase, p.UseNumbers, p.UseSpecial,
                    p.Capitalize, p.IncludeNumber);
            });

        // The MAUI label bound ColoredPassword/ColoredUsername (HTML spans from GeneratedValueFormatter); the screen
        // gets the same split as runs and colors them itself.
        IOahuState<IReadOnlyList<TextRun>> IGeneratorViewModel.Generated => _generated ??=
            OahuState.FromProperty<IReadOnlyList<TextRun>>(
                this, [nameof(Password), nameof(Username), nameof(IsUsername)],
                () => OahuText.Runs(IsUsername ? Username : Password));

        IOahuEditable<GeneratorKind> IGeneratorViewModel.Kind => Editable(nameof(GeneratorTypeSelected),
            () => Map<GeneratorKind>(GeneratorTypeSelected), v => GeneratorTypeSelected = Map<GeneratorType>(v));

        IOahuEditable<PasswordKind> IGeneratorViewModel.PasswordType => Editable(nameof(PasswordTypeSelectedIndex),
            () => (PasswordKind)PasswordTypeSelectedIndex, v => PasswordTypeSelectedIndex = (int)v);

        IOahuEditable<int> IGeneratorViewModel.Length => Editable(nameof(Length), () => Length, SetLength);

        IOahuEditable<bool> IGeneratorViewModel.Uppercase => Editable(nameof(Uppercase), () => Uppercase, v => Uppercase = v);
        IOahuEditable<bool> IGeneratorViewModel.Lowercase => Editable(nameof(Lowercase), () => Lowercase, v => Lowercase = v);
        IOahuEditable<bool> IGeneratorViewModel.Numbers => Editable(nameof(Number), () => Number, v => Number = v);
        IOahuEditable<bool> IGeneratorViewModel.Special => Editable(nameof(Special), () => Special, v => Special = v);
        IOahuEditable<int> IGeneratorViewModel.MinNumbers => Editable(nameof(MinNumber), () => MinNumber, v => MinNumber = v);
        IOahuEditable<int> IGeneratorViewModel.MinSpecial => Editable(nameof(MinSpecial), () => MinSpecial, v => MinSpecial = v);
        IOahuEditable<bool> IGeneratorViewModel.AvoidAmbiguous =>
            Editable(nameof(AvoidAmbiguousChars), () => AvoidAmbiguousChars, v => AvoidAmbiguousChars = v);
        IOahuEditable<int> IGeneratorViewModel.NumberOfWords => Editable(nameof(NumWords), () => NumWords, v => NumWords = v);
        IOahuEditable<string> IGeneratorViewModel.WordSeparator =>
            Editable(nameof(WordSeparator), () => WordSeparator ?? string.Empty, v => WordSeparator = v);
        IOahuEditable<bool> IGeneratorViewModel.Capitalize => Editable(nameof(Capitalize), () => Capitalize, v => Capitalize = v);
        IOahuEditable<bool> IGeneratorViewModel.IncludeNumber =>
            Editable(nameof(IncludeNumber), () => IncludeNumber, v => IncludeNumber = v);

        IOahuEditable<UsernameKind> IGeneratorViewModel.UsernameType => Editable(nameof(UsernameTypeSelected),
            () => Map<UsernameKind>(UsernameTypeSelected), v => UsernameTypeSelected = Map<UsernameType>(v));
        IOahuEditable<string> IGeneratorViewModel.PlusAddressedEmail => Editable(nameof(PlusAddressedEmail),
            () => PlusAddressedEmail ?? string.Empty, v => PlusAddressedEmail = v);
        IOahuEditable<EmailKind> IGeneratorViewModel.PlusAddressedEmailType => Editable(nameof(PlusAddressedEmailTypeSelected),
            () => Map<EmailKind>(PlusAddressedEmailTypeSelected), v => PlusAddressedEmailTypeSelected = Map<UsernameEmailType>(v));
        IOahuEditable<string> IGeneratorViewModel.CatchAllDomain => Editable(nameof(CatchAllEmailDomain),
            () => CatchAllEmailDomain ?? string.Empty, v => CatchAllEmailDomain = v);
        IOahuEditable<EmailKind> IGeneratorViewModel.CatchAllEmailType => Editable(nameof(CatchAllEmailTypeSelected),
            () => Map<EmailKind>(CatchAllEmailTypeSelected), v => CatchAllEmailTypeSelected = Map<UsernameEmailType>(v));
        IOahuEditable<ForwardedService> IGeneratorViewModel.Service => Editable(nameof(ForwardedEmailServiceSelected),
            () => Map<ForwardedService>(ForwardedEmailServiceSelected),
            v => ForwardedEmailServiceSelected = Map<ForwardedEmailServiceType>(v));
        // The secret is per service: the getter follows ForwardedEmailServiceSelected, which raises
        // PropertyChanged(ForwardedEmailApiSecret) when it changes.
        IOahuEditable<string> IGeneratorViewModel.ApiSecret => Editable(nameof(ForwardedEmailApiSecret),
            () => ForwardedEmailApiSecret ?? string.Empty, v => ForwardedEmailApiSecret = v);
        IOahuEditable<string> IGeneratorViewModel.AddyIoDomain => Editable(nameof(AddyIoDomainName),
            () => AddyIoDomainName ?? string.Empty, v => AddyIoDomainName = v);
        IOahuEditable<string> IGeneratorViewModel.ForwardEmailDomain => Editable(nameof(ForwardEmailDomainName),
            () => ForwardEmailDomainName ?? string.Empty, v => ForwardEmailDomainName = v);
        IOahuEditable<bool> IGeneratorViewModel.CapitalizeRandomWord => Editable(nameof(CapitalizeRandomWordUsername),
            () => CapitalizeRandomWordUsername, v => CapitalizeRandomWordUsername = v);
        IOahuEditable<bool> IGeneratorViewModel.IncludeNumberRandomWord => Editable(nameof(IncludeNumberRandomWordUsername),
            () => IncludeNumberRandomWordUsername, v => IncludeNumberRandomWordUsername = v);

        IOahuEvents<Unit> IGeneratorViewModel.Copied => _copied;

        async Task IGeneratorViewModel.CopyValue()
        {
            await CopyAsync();
            _copied.Publish(Unit.Default);
        }

        Task IGeneratorViewModel.Regenerate()
        {
            // The commands keep MAUI's error dialogs (OnSubmitException) and refuse a second run while one is going.
            (IsUsername ? RegenerateUsernameCommand : RegenerateCommand).Execute(null);
            return Task.CompletedTask;
        }

        Task IGeneratorViewModel.LengthEditingEnded()
        {
#if DEBUG
            Console.WriteLine($"OAHUDEBUG generator length released at {Length} after {_lengthEdits} edits");
            _lengthEdits = 0;
#endif
            return SliderChangedAsync();
        }

        void IGeneratorViewModel.ToggleApiSecret() => ToggleForwardedEmailHiddenValueCommand.Execute(null);

        void IGeneratorViewModel.ShowUsernameTypeHelp() => UsernameTypePromptHelp();

#if DEBUG
        private int _lengthEdits;
#endif

        private void SetLength(int value)
        {
#if DEBUG
            _lengthEdits++;
            Console.WriteLine($"OAHUDEBUG generator length edit #{_lengthEdits}: {value} (main: {MainThread.IsMainThread})");
            // BW_OAHU_CLAMP_LIVE=1: clamp to the policy minimum during the drag (the app clamps only on release), to
            // see whether the native slider fights a value the view model refuses while the finger moves.
            if (Bit.App.Pages.OahuDebug.OahuDebugRoute.Option("BW_OAHU_CLAMP_LIVE", "bwOahuClampLive") == "1"
                && EnforcedPolicyOptions != null && value < EnforcedPolicyOptions.MinLength)
            {
                value = EnforcedPolicyOptions.MinLength;
            }
#endif
            Length = value;
        }

        /// <summary>One adapter per property, made on first use: the contract's getters are called once per screen.</summary>
        private IOahuEditable<T> Editable<T>(string property, Func<T> get, Action<T> set)
        {
            if (!_editables.TryGetValue(property, out var editable))
            {
                editable = OahuEditable.FromProperty(this, property, get, set);
                _editables[property] = editable;
            }
            return (IOahuEditable<T>)editable;
        }

        // The contract mirrors the app's enums (src/Core.Contracts can't reference Bit.Core): same member names.
        private static T Map<T>(Enum value) where T : struct, Enum => Enum.Parse<T>(value.ToString());

        private static string Label(Enum value) => EnumHelper.GetLocalizedValue(value, value.GetType());
    }
}
