import OahuRuntime
import SwiftUI

/// The item add/edit/clone form, hosted in the MAUI CipherAddEditPage: type, name, the login, card or identity
/// section, URIs, folder, favorite, re-prompt, notes, custom fields, ownership and collections. The page keeps its
/// title, Save/Cancel and the overflow menu, every dialog and action sheet, and the pages it pushes.
public struct CipherAddEditScreen: View {
    @Bindable var model: CipherAddEditModel
    @FocusState private var focus: Focus?
    @State private var pendingFocus: Focus?
    // One row-text state per keyed list (OAHU "Text fields in rows").
    @State private var uriTexts = OahuRowTexts<EditUriRow>(\.uri)
    @State private var fieldTexts = OahuRowTexts<EditFieldRow>(\.editText)
    @State private var localNotes: String?
    static let notesLocal = ProcessInfo.processInfo.environment["BW_OAHU_NOTES_LOCAL"] == "1"

    private var localNotesBinding: Binding<String> {
        Binding(get: { localNotes ?? model.notes }, set: { localNotes = $0; model.notes = $0 })
    }

    enum Focus: Hashable {
        case name, username, password, totp, cardholder, cardNumber, expYear, code, notes
        case identity(Int)
        case uri(String)
        case field(String)
    }

    public init(model: CipherAddEditModel) {
        self.model = model
    }

    public var body: some View {
        let copy = model.copy
        let layout = model.layout
        let palette = model.palette
        Form {
            if layout.ownershipPolicyInEffect {
                Section {
                    Text(copy.personalOwnershipPolicyInEffect)
                        .font(.footnote.bold())
                        .foregroundStyle(palette.mutedColor)
                        .frame(maxWidth: .infinity)
                        .accessibilityIdentifier("PersonalOwnershipPolicyLabel")
                }
            }
            Section {
                if layout.showTypePicker {
                    IndexPicker(label: copy.type, options: copy.typeOptions, selection: $model.type)
                        .accessibilityIdentifier("ItemTypePicker")
                }
                LabeledField(label: copy.name) {
                    TextField(copy.name, text: $model.name)
                        .focused($focus, equals: .name)
                        .submitLabel(.next)
                        .onSubmit { focus = nextAfterName(layout.kind) }
                        .accessibilityIdentifier("ItemNameEntry")
                }
                switch layout.kind {
                case .login: loginSection(copy: copy, layout: layout, palette: palette)
                case .card: cardSection(copy: copy, layout: layout, palette: palette)
                case .identity: identitySection(copy: copy)
                case .secureNote: EmptyView()
                }
            } header: {
                Text(copy.itemInformation)
            }
            if layout.kind == .login {
                urisSection(copy: copy, layout: layout, palette: palette)
            }
            Section {
                IndexPicker(label: copy.folder, options: model.choices.folders, selection: $model.folder)
                    .accessibilityIdentifier("FolderPicker")
                Toggle(copy.favorite, isOn: $model.favorite)
                    .accessibilityIdentifier("ItemFavoriteToggle")
                if layout.showPasswordPrompt {
                    HStack {
                        Text(copy.passwordPrompt)
                        Button { model.passwordPromptHelp() } label: {
                            Glyph(glyph: BitwardenIcons.questionCircle, size: 18, color: palette.mutedColor)
                        }
                        .buttonStyle(.borderless)
                        .accessibilityLabel(copy.passwordPromptHelp)
                        Spacer()
                        Toggle(copy.passwordPrompt, isOn: $model.reprompt)
                            .labelsHidden()
                            .accessibilityIdentifier("MasterPasswordRepromptToggle")
                    }
                }
            } header: {
                Text(copy.miscellaneous)
            }
            Section {
                // MAUI's Editor with AutoSize: grows with its text. DEBUG A/B (BW_OAHU_NOTES_LOCAL=1): bind a local
                // @State and copy it to the model, to tell SwiftUI's behavior from OAHU's binding (gaps.md, step 5).
                TextField(copy.notes, text: Self.notesLocal ? localNotesBinding : $model.notes, axis: .vertical)
                    .lineLimit(3...)
                    .focused($focus, equals: .notes)
                    .accessibilityIdentifier("ItemNotesEntry")
            } header: {
                Text(copy.notes)
            }
            fieldsSection(copy: copy, palette: palette)
            if layout.showOwnership {
                Section {
                    IndexPicker(label: copy.whoOwnsThisItem, options: model.choices.owners, selection: $model.owner)
                        .accessibilityIdentifier("ItemOwnershipPicker")
                } header: {
                    Text(copy.ownership)
                }
            }
            if layout.showCollections {
                Section {
                    if !layout.hasCollections {
                        Text(copy.noCollectionsToList).accessibilityIdentifier("NoCollectionsToListLabel")
                    } else {
                        ForEach(model.collections) { row in
                            Toggle(row.name, isOn: Binding(get: { row.checked }, set: { _ in model.toggleCollection(row.id.rawValue) }))
                                .accessibilityIdentifier("CollectionItemSwitch")
                        }
                    }
                } header: {
                    Text(copy.collections)
                }
            }
        }
        .scrollContentBackground(.hidden)
        .background(palette.backgroundColor)
        .environment(\.colorScheme, palette.colorScheme)
        .tint(palette.primaryColor)
        .task {
            // One-shot focus requests from C#: the name of a new item, a field Save found invalid, a new URI.
            for await request in model.focus.stream() {
                let target: Focus? = switch request.field {
                case .name: .name
                case .username: .username
                case .password: .password
                case .totp: .totp
                case .cardholderName: .cardholder
                case .identityFirstName: .identity(0)
                case .notes: .notes
                case .uri: request.rowKey.map { .uri($0) }
                case .customField: request.rowKey.map { .field($0) }
                }
                if Self.holdFocus {
                    pendingFocus = target
                } else {
                    focus = target
                }
            }
        }
        .task(id: pendingFocus) {
            // Step 7, #976: "the screen holds such a request until it can act on it". OAHU gives no moment at which a
            // presented field accepts focus, so this retries until SwiftUI keeps it (BW_OAHU_FOCUS=held).
            guard let target = pendingFocus else { return }
            for attempt in 1...30 {
                focus = target
                try? await Task.sleep(for: .milliseconds(100))
                if focus == target {
                    print("OAHUDEBUG focus held: kept on attempt \(attempt)")
                    break
                }
                if attempt == 30 { print("OAHUDEBUG focus held: never kept") }
            }
            pendingFocus = nil
        }
    }

    private static let holdFocus = ProcessInfo.processInfo.environment["BW_OAHU_FOCUS"] == "held"

    private func nextAfterName(_ kind: EditItemKind) -> Focus? {
        switch kind {
        case .login: .username
        case .card: .cardholder
        case .identity: .identity(0)
        case .secureNote: nil
        }
    }

    // MARK: Login

    @ViewBuilder
    private func loginSection(copy: CipherAddEditCopy, layout: CipherAddEditLayout, palette: AppPalette) -> some View {
        LabeledField(label: copy.username) {
            HStack {
                TextField(copy.username, text: $model.username)
                    .focused($focus, equals: .username)
                    .textContentType(.username)
                    .textInputAutocapitalization(.never)
                    .autocorrectionDisabled()
                    .submitLabel(.next)
                    .onSubmit { focus = .password }
                    .accessibilityIdentifier("LoginUsernameEntry")
                IconButton(glyph: BitwardenIcons.generate, label: copy.generateUsername, color: palette.primaryColor) {
                    model.generateUsername()
                }
                .accessibilityIdentifier("GenerateUsernameButton")
            }
        }
        LabeledField(label: copy.password) {
            HStack {
                SecretField(label: copy.password, text: $model.password, input: $model.passwordTextInput, shown: layout.showPassword && layout.viewPassword)
                    .focused($focus, equals: .password)
                    .disabled(!layout.viewPassword)
                    .submitLabel(.next)
                    .onSubmit { focus = .totp }
                    .accessibilityIdentifier("LoginPasswordEntry")
                if layout.viewPassword {
                    IconButton(glyph: BitwardenIcons.checkCircle, label: copy.checkPassword, color: palette.primaryColor) {
                        model.checkPassword()
                    }
                    .accessibilityIdentifier("CheckPasswordButton")
                    IconButton(glyph: layout.showPassword ? BitwardenIcons.eyeSlash : BitwardenIcons.eye,
                               label: copy.toggleVisibility, color: palette.primaryColor) {
                        let refocus = focus == .password
                        model.togglePassword()
                        if refocus { focus = .password }   // the field swapped; keep the keyboard up
                    }
                    .accessibilityIdentifier("ViewPasswordButton")
                    IconButton(glyph: BitwardenIcons.generate, label: copy.generatePassword, color: palette.primaryColor) {
                        model.generatePassword()
                    }
                    .accessibilityIdentifier("RegeneratePasswordButton")
                }
            }
        }
        if let created = layout.passkeyCreated {
            LabeledField(label: copy.passkey) {
                Text(created).foregroundStyle(palette.mutedColor)
            }
        }
        LabeledField(label: copy.authenticatorKey) {
            if !layout.hasTotp {
                Button { model.scanTotp() } label: {
                    HStack {
                        Glyph(glyph: BitwardenIcons.camera, size: 18, color: palette.primaryColor)
                        Text(copy.setupTotp)
                    }
                    .frame(maxWidth: .infinity)
                }
                .buttonStyle(.bordered)
                .accessibilityIdentifier("SetupTotpButton")
            } else {
                HStack {
                    SecretField(label: copy.authenticatorKey, text: $model.totp, input: $model.totpTextInput, shown: layout.viewPassword)
                        .focused($focus, equals: .totp)
                        .disabled(!layout.viewPassword)
                        .accessibilityIdentifier("LoginTotpEntry")
                    if layout.allowTotpCopy {
                        IconButton(glyph: BitwardenIcons.clone, label: copy.copyTotp, color: palette.primaryColor) {
                            model.copyTotp()
                        }
                        .accessibilityIdentifier("CopyTotpValueButton")
                    }
                    IconButton(glyph: BitwardenIcons.camera, label: copy.scanQr, color: palette.primaryColor) {
                        model.scanTotp()
                    }
                    .accessibilityIdentifier("ScanTotpButton")
                }
            }
        }
    }

    // MARK: Card

    @ViewBuilder
    private func cardSection(copy: CipherAddEditCopy, layout: CipherAddEditLayout, palette: AppPalette) -> some View {
        LabeledField(label: copy.cardholderName) {
            TextField(copy.cardholderName, text: $model.cardholderName)
                .focused($focus, equals: .cardholder)
                .textContentType(.name)
                .submitLabel(.next)
                .onSubmit { focus = .cardNumber }
                .accessibilityIdentifier("CardholderNameEntry")
        }
        LabeledField(label: copy.number) {
            HStack {
                SecretField(label: copy.number, text: $model.cardNumber, input: $model.cardNumberTextInput, shown: layout.showCardNumber)
                    .focused($focus, equals: .cardNumber)
                    .accessibilityIdentifier("CardNumberEntry")
                IconButton(glyph: layout.showCardNumber ? BitwardenIcons.eyeSlash : BitwardenIcons.eye,
                           label: copy.toggleVisibility, color: palette.primaryColor) {
                    let refocus = focus == .cardNumber
                    model.toggleCardNumber()
                    if refocus { focus = .cardNumber }
                }
                .accessibilityIdentifier("ShowCardNumberButton")
            }
        }
        IndexPicker(label: copy.brand, options: copy.cardBrandOptions, selection: $model.cardBrand)
            .accessibilityIdentifier("CardBrandPicker")
        IndexPicker(label: copy.expirationMonth, options: copy.cardExpMonthOptions, selection: $model.cardExpMonth)
            .accessibilityIdentifier("CardExpirationMonthPicker")
        LabeledField(label: copy.expirationYear) {
            TextField(copy.expirationYear, text: $model.cardExpYear)
                .focused($focus, equals: .expYear)
                .keyboardType(.numberPad)
                .accessibilityIdentifier("CardExpirationYearEntry")
        }
        LabeledField(label: copy.securityCode) {
            HStack {
                SecretField(label: copy.securityCode, text: $model.cardCode, input: $model.cardCodeTextInput, shown: layout.showCardCode)
                    .focused($focus, equals: .code)
                    .keyboardType(.numberPad)
                    .accessibilityIdentifier("CardSecurityCodeEntry")
                IconButton(glyph: layout.showCardCode ? BitwardenIcons.eyeSlash : BitwardenIcons.eye,
                           label: copy.toggleVisibility, color: palette.primaryColor) {
                    let refocus = focus == .code
                    model.toggleCardCode()
                    if refocus { focus = .code }
                }
                .accessibilityIdentifier("CardShowSecurityCodeButton")
            }
        }
    }

    // MARK: Identity

    private struct IdentityLine {
        let label: String
        let path: WritableKeyPath<IdentityFields, String>
        var keyboard: UIKeyboardType = .default
        var content: UITextContentType? = nil
        var capitalization: TextInputAutocapitalization = .sentences
        let id: String
    }

    private func identityLines(_ copy: CipherAddEditCopy) -> [IdentityLine] {
        [
            .init(label: copy.firstName, path: \.firstName, content: .givenName, capitalization: .words, id: "IdentityFirstNameEntry"),
            .init(label: copy.middleName, path: \.middleName, content: .middleName, capitalization: .words, id: "IdentityMiddleNameEntry"),
            .init(label: copy.lastName, path: \.lastName, content: .familyName, capitalization: .words, id: "IdentityLastNameEntry"),
            .init(label: copy.username, path: \.username, content: .username, capitalization: .never, id: "IdentityUsernameEntry"),
            .init(label: copy.company, path: \.company, content: .organizationName, id: "IdentityCompanyEntry"),
            .init(label: copy.ssn, path: \.ssn, capitalization: .never, id: "IdentitySsnEntry"),
            .init(label: copy.passportNumber, path: \.passportNumber, capitalization: .characters, id: "IdentityPassportNumberEntry"),
            .init(label: copy.licenseNumber, path: \.licenseNumber, capitalization: .characters, id: "IdentityLicenseNumberEntry"),
            .init(label: copy.email, path: \.email, keyboard: .emailAddress, content: .emailAddress, capitalization: .never, id: "IdentityEmailEntry"),
            .init(label: copy.phone, path: \.phone, keyboard: .phonePad, content: .telephoneNumber, id: "IdentityPhoneEntry"),
            .init(label: copy.address1, path: \.address1, content: .streetAddressLine1, id: "IdentityAddressOneEntry"),
            .init(label: copy.address2, path: \.address2, content: .streetAddressLine2, id: "IdentityAddressTwoEntry"),
            .init(label: copy.address3, path: \.address3, id: "IdentityAddressThreeEntry"),
            .init(label: copy.city, path: \.city, content: .addressCity, id: "IdentityCityEntry"),
            .init(label: copy.state, path: \.state, content: .addressState, id: "IdentityStateEntry"),
            .init(label: copy.postalCode, path: \.postalCode, content: .postalCode, capitalization: .characters, id: "IdentityPostalCodeEntry"),
            .init(label: copy.country, path: \.country, content: .countryName, id: "IdentityCountryEntry"),
        ]
    }

    @ViewBuilder
    private func identitySection(copy: CipherAddEditCopy) -> some View {
        IndexPicker(label: copy.title, options: copy.identityTitleOptions, selection: $model.identityTitle)
            .accessibilityIdentifier("IdentityTitlePicker")
        let lines = identityLines(copy)
        ForEach(lines.indices, id: \.self) { i in
            let line = lines[i]
            LabeledField(label: line.label) {
                // A field of the record editable: each keystroke proposes the whole IdentityFields record.
                TextField(line.label, text: $model.identity[dynamicMember: line.path])
                    .focused($focus, equals: .identity(i))
                    .keyboardType(line.keyboard)
                    .textContentType(line.content)
                    .textInputAutocapitalization(line.capitalization)
                    .submitLabel(i < lines.count - 1 ? .next : .done)
                    .onSubmit { focus = i < lines.count - 1 ? .identity(i + 1) : nil }
                    .accessibilityIdentifier(line.id)
            }
        }
    }

    // MARK: URIs

    @ViewBuilder
    private func urisSection(copy: CipherAddEditCopy, layout: CipherAddEditLayout, palette: AppPalette) -> some View {
        Section {
            ForEach(model.uris) { row in
                LabeledField(label: copy.uri) {
                    HStack {
                        TextField(copy.uri, text: uriTexts.binding(for: row.id, in: { model.uris }) {
                            model.setUriText(RowText(key: row.id.rawValue, text: $0))
                        })
                        .focused($focus, equals: .uri(row.id.rawValue))
                        .keyboardType(.URL)
                        .textContentType(.URL)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                        .disabled(layout.urisLocked)
                        .accessibilityIdentifier("LoginUriEntry")
                        if !layout.urisLocked {
                            IconButton(glyph: BitwardenIcons.cog, label: copy.options, color: palette.primaryColor) {
                                model.uriOptions(row.id.rawValue)
                            }
                            .accessibilityIdentifier("LoginUriOptionsButton")
                        }
                    }
                }
            }
            if !layout.urisLocked {
                Button(copy.newUri) { model.addUri() }
                    .accessibilityIdentifier("LoginAddNewUriButton")
            }
        } header: {
            Text(copy.uris)
        }
    }

    // MARK: Custom fields

    @ViewBuilder
    private func fieldsSection(copy: CipherAddEditCopy, palette: AppPalette) -> some View {
        Section {
            ForEach(model.fields) { row in
                fieldRow(row, copy: copy, palette: palette)
            }
            Button(copy.newCustomField) { model.addField() }
                .accessibilityIdentifier("NewCustomFieldButton")
        } header: {
            Text(copy.customFields)
        }
    }

    @ViewBuilder
    private func fieldRow(_ row: EditFieldRow, copy: CipherAddEditCopy, palette: AppPalette) -> some View {
        let key = row.id.rawValue
        let options = IconButton(glyph: BitwardenIcons.cog, label: copy.options, color: palette.primaryColor) {
            model.fieldOptions(key)
        }
        switch row {
        case let .text(_, name, _):
            LabeledField(label: name) {
                HStack {
                    TextField(name, text: fieldText(row))
                        .focused($focus, equals: .field(key))
                        .accessibilityIdentifier("TextCustomFieldValueEntry")
                    options
                }
            }
        case let .hidden(_, name, _, shown, canReveal):
            LabeledField(label: name) {
                HStack {
                    SecretField(label: name, text: fieldText(row), shown: shown)
                        .focused($focus, equals: .field(key))
                        .disabled(!canReveal)
                        .accessibilityIdentifier("HiddenCustomFieldValueEntry")
                    if canReveal {
                        IconButton(glyph: shown ? BitwardenIcons.eyeSlash : BitwardenIcons.eye,
                                   label: copy.toggleVisibility, color: palette.primaryColor) {
                            let refocus = focus == .field(key)
                            model.toggleFieldHidden(key)
                            if refocus { focus = .field(key) }
                        }
                        .accessibilityIdentifier("HiddenCustomFieldShowValueButton")
                    }
                    options
                }
            }
        case let .toggle(_, name, value):
            HStack {
                Toggle(name, isOn: Binding(get: { value }, set: { model.setFieldBool(RowBool(key: key, value: $0)) }))
                    .accessibilityIdentifier("BooleanCustomFieldSwitch")
                options
            }
        case let .linked(_, name, selected, linkedOptions):
            HStack {
                IndexPicker(label: name, options: linkedOptions, selection: Binding(
                    get: { selected }, set: { model.setFieldLinked(RowIndex(key: key, index: $0)) }))
                    .accessibilityIdentifier("LinkedCustomFieldValuePicker")
                options
            }
        }
    }

    private func fieldText(_ row: EditFieldRow) -> Binding<String> {
        fieldTexts.binding(for: row.id, in: { model.fields }) {
            model.setFieldText(RowText(key: row.id.rawValue, text: $0))
        }
    }
}

extension EditFieldRow {
    /// The text a text or hidden field edits (OahuRowTexts needs a key path to a String).
    var editText: String {
        switch self {
        case let .text(_, _, value): value
        case let .hidden(_, _, value, _, _): value
        case .toggle, .linked: ""
        }
    }
}

/// A label above its control, as MAUI's box-row did.
private struct LabeledField<Content: View>: View {
    let label: String
    @ViewBuilder let content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(label).font(.caption).foregroundStyle(.secondary)
            content
        }
    }
}

/// A picker over the options' indexes (the view model's *SelectedIndex properties).
private struct IndexPicker: View {
    let label: String
    let options: [String]
    @Binding var selection: Int

    var body: some View {
        Picker(label, selection: $selection) {
            ForEach(options.indices, id: \.self) { i in
                Text(options[i]).tag(i)
            }
        }
    }
}

/// MAUI's MonoEntry with IsPassword. Steps 5-6 swapped a SecureField for a TextField (docs/oahu/gaps.md, G28); step 7
/// uses OahuTextField's secure mode (#982), one UIKit field whose secure entry follows the eye. BW_OAHU_OLDSECURE=1
/// brings back the swap for comparison.
private struct SecretField: View {
    let label: String
    @Binding var text: String
    /// The generated TextInput binding of a string editable, which OahuTextField reads; rows pass none.
    var input: Binding<String>? = nil
    let shown: Bool

    var body: some View {
        if ProcessInfo.processInfo.environment["BW_OAHU_OLDSECURE"] == "1" {
            Group {
                if shown {
                    TextField(label, text: $text)
                } else {
                    SecureField(label, text: $text)
                }
            }
            .font(.system(.body, design: .monospaced))
            .textInputAutocapitalization(.never)
            .autocorrectionDisabled()
        } else {
            OahuTextField(
                label, text: input ?? $text,
                keyboard: .init(autocapitalizationType: .none, autocorrectionType: .no),
                isSecure: !shown)
                .font(.system(.body, design: .monospaced))
        }
    }
}

private struct IconButton: View {
    let glyph: String
    let label: String
    let color: Color
    let action: () -> Void

    var body: some View {
        Button(action: action) { Glyph(glyph: glyph, color: color) }
            .buttonStyle(.borderless)
            .accessibilityLabel(label)
    }
}
