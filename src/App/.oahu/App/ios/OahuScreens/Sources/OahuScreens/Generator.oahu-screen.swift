import OahuRuntime
import SwiftUI
import UIKit

/// The password and username generator, hosted in the MAUI GeneratorPage (the Generator tab, and the modal add/edit
/// opens to pick a value). The page keeps its title, toolbar items and dialogs. Every option is bound to an editable
/// value: the view model saves it, normalizes the options against the password policy, and the control shows its
/// answer.
public struct GeneratorScreen: View {
    @Bindable var model: GeneratorModel

    public init(model: GeneratorModel) {
        self.model = model
    }

    public var body: some View {
        let copy = model.copy
        let layout = model.layout
        let palette = model.palette
        Form {
            if layout.policyInEffect {
                Section {
                    Text(copy.policyInEffect)
                        .font(.footnote.bold())
                        .foregroundStyle(palette.mutedColor)
                        .frame(maxWidth: .infinity)
                        .padding(8)
                        .overlay(RoundedRectangle(cornerRadius: 4).stroke(palette.primaryColor))
                        .accessibilityIdentifier("PasswordGeneratorPolicyInEffectLabel")
                }
                .listRowBackground(Color.clear)
            }
            Section {
                GeneratedValueRow(model: model, copy: copy, palette: palette, isUsername: layout.isUsername)
            }
            if layout.showKindPicker {
                Section {
                    Picker(copy.whatToGenerate, selection: $model.kind) {
                        ForEach(copy.kinds, id: \.value) { Text($0.label).tag($0.value) }
                    }
                    .accessibilityIdentifier("GeneratorTypePicker")
                }
            }
            if layout.isUsername {
                UsernameOptions(model: model, copy: copy, layout: layout, palette: palette)
            } else {
                PasswordOptions(model: model, copy: copy, palette: palette)
            }
        }
        .scrollContentBackground(.hidden)
        .background(palette.backgroundColor)
        .environment(\.colorScheme, palette.colorScheme)
        .tint(palette.primaryColor)
        .scrollDismissesKeyboard(.interactively)
        .task {
            // A one-shot event: the value was copied (the MAUI toast still shows).
            let haptic = UINotificationFeedbackGenerator()
            for await _ in model.copied.stream() {
                haptic.notificationOccurred(.success)
            }
        }
    }
}

/// The value in colored runs (MAUI: an HTML label of GeneratedValueFormatter spans), with copy and regenerate.
private struct GeneratedValueRow: View {
    let model: GeneratorModel
    let copy: GeneratorCopy
    let palette: AppPalette
    let isUsername: Bool

    var body: some View {
        HStack(spacing: 4) {
            Text(colored(model.generated))
                .font(.system(.title3, design: .monospaced))
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.vertical, 12)
                .accessibilityIdentifier("GeneratedPasswordLabel")
            Button { model.copyValue() } label: { Glyph(glyph: BitwardenIcons.clone, color: palette.primaryColor) }
                .buttonStyle(.borderless)
                .accessibilityLabel(isUsername ? copy.copyUsername : copy.copyPassword)
                .accessibilityIdentifier("CopyValueButton")
            Button { model.regenerate() } label: { Glyph(glyph: BitwardenIcons.generate, color: palette.primaryColor) }
                .buttonStyle(.borderless)
                .accessibilityLabel(isUsername ? copy.generateUsername : copy.generatePassword)
                .accessibilityIdentifier("RegenerateValueButton")
        }
    }

    private func colored(_ runs: [TextRun]) -> AttributedString {
        var text = AttributedString()
        for run in runs {
            // A zero-width space after each character lets the value wrap anywhere. Without it SwiftUI wraps a long
            // password at word-like boundaries and draws a hyphen that isn't part of the value (MAUI's HTML label
            // used word-break: break-all for the same reason). Copying goes through C#, so the spaces never leave.
            var part = AttributedString(run.text.map { String($0) + "\u{200B}" }.joined())
            switch run.kind {
            case .letters: part.foregroundColor = palette.textColor
            case .digits: part.foregroundColor = palette.passwordNumber.color
            case .symbols: part.foregroundColor = palette.passwordSpecial.color
            }
            text += part
        }
        return text
    }
}

private struct PasswordOptions: View {
    @Bindable var model: GeneratorModel
    let copy: GeneratorCopy
    let palette: AppPalette

    var body: some View {
        let locks = model.locks
        Section {
            Picker(copy.passwordType, selection: $model.passwordType) {
                ForEach(copy.passwordKinds, id: \.value) { Text($0.label).tag($0.value) }
            }
            .accessibilityIdentifier("PasswordTypePicker")
            if model.passwordType == .passphrase {
                Stepper(value: $model.numberOfWords, in: 3 ... 20) {
                    LabeledContent(copy.numberOfWords, value: "\(model.numberOfWords)")
                }
                .accessibilityIdentifier("NumberOfWordsStepper")
                LabeledContent(copy.wordSeparator) {
                    TextField(copy.wordSeparator, text: $model.wordSeparator)
                        .multilineTextAlignment(.trailing)
                        // MAUI: IsSpellCheckEnabled/IsTextPredictionEnabled off and NoEmojiKeyboardEffect.
                        .keyboardType(.asciiCapable)
                        .autocorrectionDisabled()
                        .textInputAutocapitalization(.never)
                        .accessibilityIdentifier("WordSeparatorEntry")
                }
                Toggle(copy.capitalize, isOn: $model.capitalize)
                    .disabled(locks.capitalize)
                    .accessibilityIdentifier("CapitalizePassphraseToggle")
                Toggle(copy.includeNumber, isOn: $model.includeNumber)
                    .disabled(locks.includeNumber)
                    .accessibilityIdentifier("IncludeNumbersToggle")
            } else {
                VStack(alignment: .leading) {
                    LabeledContent(copy.length, value: "\(model.length)")
                        .accessibilityIdentifier("PasswordLengthLabel")
                    // No step: the view model's Int is the slider's value, so the thumb follows the rounded answer
                    // (MAUI's slider was continuous over an int property too). Releasing it saves the options.
                    Slider(
                        value: Binding(
                            get: { Double(model.length) },
                            set: { model.length = Int($0.rounded()) }),
                        in: 5 ... 128,
                        onEditingChanged: { editing in
                            if !editing { model.lengthEditingEnded() }
                        }
                    )
                    .accessibilityIdentifier("PasswordLengthSlider")
                    .accessibilityValue("\(model.length)")
                }
                Toggle(isOn: $model.uppercase) { OptionLabel(copy.uppercaseLabel, copy.uppercaseDescription) }
                    .disabled(locks.uppercase)
                    .accessibilityIdentifier("UppercaseAtoZToggle")
                Toggle(isOn: $model.lowercase) { OptionLabel(copy.lowercaseLabel, copy.lowercaseDescription) }
                    .disabled(locks.lowercase)
                    .accessibilityIdentifier("LowercaseAtoZToggle")
                Toggle(isOn: $model.numbers) { OptionLabel(copy.numbersLabel, copy.numbersDescription) }
                    .disabled(locks.numbers)
                    .accessibilityIdentifier("NumbersZeroToNineToggle")
                Toggle(isOn: $model.special) { OptionLabel(copy.specialLabel, copy.specialDescription) }
                    .disabled(locks.special)
                    .accessibilityIdentifier("SpecialCharactersToggle")
                Stepper(value: $model.minNumbers, in: 0 ... 5) {
                    LabeledContent(copy.minNumbers, value: "\(model.minNumbers)")
                }
                .accessibilityIdentifier("MinNumberStepper")
                Stepper(value: $model.minSpecial, in: 0 ... 5) {
                    LabeledContent(copy.minSpecial, value: "\(model.minSpecial)")
                }
                .accessibilityIdentifier("MinSpecialStepper")
                Toggle(copy.avoidAmbiguous, isOn: $model.avoidAmbiguous)
                    .accessibilityIdentifier("AvoidAmbiguousCharsToggle")
            }
        } header: {
            Text(copy.options)
        }
    }
}

private struct OptionLabel: View {
    let label: String
    let description: String

    init(_ label: String, _ description: String) {
        self.label = label
        self.description = description
    }

    var body: some View {
        Text(label).accessibilityLabel(description)
    }
}

private struct UsernameOptions: View {
    @Bindable var model: GeneratorModel
    let copy: GeneratorCopy
    let layout: GeneratorLayout
    let palette: AppPalette

    var body: some View {
        Section {
            HStack {
                Picker(copy.usernameType, selection: $model.usernameType) {
                    ForEach(copy.usernameKinds, id: \.value) { Text($0.label).tag($0.value) }
                }
                .accessibilityIdentifier("UsernameTypePicker")
                Button { model.showUsernameTypeHelp() } label: {
                    Image(systemName: "questionmark.circle")
                }
                .buttonStyle(.borderless)
                .accessibilityLabel(copy.usernameTypeHelp)
            }
            switch model.usernameType {
            case .plusAddressedEmail:
                EmailField(label: copy.emailRequired, text: $model.plusAddressedEmail, id: "PlusAddressedEmailEntry")
                emailType($model.plusAddressedEmailType)
            case .catchAllEmail:
                EmailField(label: copy.domainRequired, text: $model.catchAllDomain, id: "CatchAllEmailDomainEntry")
                emailType($model.catchAllEmailType)
            case .forwardedEmailAlias:
                Picker(copy.service, selection: $model.service) {
                    ForEach(copy.services, id: \.value) { Text($0.label).tag($0.value) }
                }
                .accessibilityIdentifier("ServiceTypePicker")
                if let label = layout.apiSecretLabel {
                    ApiSecretField(model: model, label: label, copy: copy, palette: palette, shown: layout.showApiSecret)
                }
                if model.service == .anonAddy {
                    EmailField(label: copy.domainRequired, text: $model.addyIoDomain, id: "AnonAddyDomainNameEntry")
                }
                if model.service == .forwardEmail {
                    EmailField(label: copy.domainRequired, text: $model.forwardEmailDomain, id: "ForwardEmailDomainNameEntry")
                }
            case .randomWord:
                Toggle(copy.capitalize, isOn: $model.capitalizeRandomWord)
                    .accessibilityIdentifier("CapitalizeRandomWordUsernameToggle")
                Toggle(copy.includeNumber, isOn: $model.includeNumberRandomWord)
                    .accessibilityIdentifier("IncludeNumberRandomWordUsernameToggle")
            }
        } header: {
            Text(copy.options)
        } footer: {
            if !layout.usernameTypeDescription.isEmpty {
                Text(layout.usernameTypeDescription)
            }
        }
    }

    @ViewBuilder
    private func emailType(_ selection: Binding<EmailKind>) -> some View {
        if layout.showEmailType {
            Picker(copy.emailType, selection: selection) {
                ForEach(copy.emailKinds, id: \.value) { Text($0.label).tag($0.value) }
            }
            LabeledContent(copy.website, value: layout.emailWebsite ?? "")
        }
    }
}

private struct EmailField: View {
    let label: String
    @Binding var text: String
    let id: String

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(label).font(.caption).foregroundStyle(.secondary)
            TextField(label, text: $text)
                .keyboardType(.emailAddress)
                .textContentType(.emailAddress)
                .autocorrectionDisabled()
                .textInputAutocapitalization(.never)
                .accessibilityIdentifier(id)
        }
    }
}

/// The forwarding service's API key: hidden unless the eye is on (MAUI: an Entry with IsPassword).
private struct ApiSecretField: View {
    @Bindable var model: GeneratorModel
    let label: String
    let copy: GeneratorCopy
    let palette: AppPalette
    let shown: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(label).font(.caption).foregroundStyle(.secondary)
            HStack {
                Group {
                    if shown {
                        TextField(label, text: $model.apiSecret)
                    } else {
                        SecureField(label, text: $model.apiSecret)
                    }
                }
                .autocorrectionDisabled()
                .textInputAutocapitalization(.never)
                .font(.system(.body, design: .monospaced))
                .accessibilityIdentifier("ForwardedEmailApiSecretEntry")
                Button { model.toggleApiSecret() } label: {
                    Glyph(glyph: shown ? BitwardenIcons.eyeSlash : BitwardenIcons.eye, color: palette.primaryColor)
                }
                .buttonStyle(.borderless)
                .accessibilityLabel(copy.toggleVisibility)
                .accessibilityIdentifier("ShowForwardedEmailApiSecretButton")
            }
        }
    }
}
