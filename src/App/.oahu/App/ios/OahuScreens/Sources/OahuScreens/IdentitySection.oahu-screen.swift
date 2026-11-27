import OahuRuntime
import SwiftUI

/// Step 7, the D25 trial: the add/edit form's identity section as its own contract, mounted by the add/edit view model
/// and drawn by the add/edit screen with OahuMountView inside its Form. Each line is its own string editable, so a
/// keystroke sends one string (the flat form's record editable sent all 17). The rows are this view's body, so the
/// parent Form lays them out as its own rows; colors come from the parent's environment (tint, colorScheme, appPalette).
public struct IdentitySectionScreen: View {
    @Bindable var model: IdentitySectionModel
    @FocusState private var focus: Int?
    // Step 8: the parent's shared focus order (CrossSection.swift), when it provides one.
    @Environment(\.formAdvance) private var advance
    @Environment(\.formOrder) private var order

    public init(model: IdentitySectionModel) {
        self.model = model
    }

    private struct Line {
        let label: String
        let path: ReferenceWritableKeyPath<IdentitySectionModel, String>
        var keyboard: UIKeyboardType = .default
        var content: UITextContentType? = nil
        var capitalization: TextInputAutocapitalization = .sentences
        let id: String
    }

    private func lines(_ c: IdentitySectionCopy) -> [Line] {
        [
            .init(label: c.firstName, path: \.firstName, content: .givenName, capitalization: .words, id: "IdentityFirstNameEntry"),
            .init(label: c.middleName, path: \.middleName, content: .middleName, capitalization: .words, id: "IdentityMiddleNameEntry"),
            .init(label: c.lastName, path: \.lastName, content: .familyName, capitalization: .words, id: "IdentityLastNameEntry"),
            .init(label: c.username, path: \.username, content: .username, capitalization: .never, id: "IdentityUsernameEntry"),
            .init(label: c.company, path: \.company, content: .organizationName, id: "IdentityCompanyEntry"),
            .init(label: c.ssn, path: \.ssn, capitalization: .never, id: "IdentitySsnEntry"),
            .init(label: c.passportNumber, path: \.passportNumber, capitalization: .characters, id: "IdentityPassportNumberEntry"),
            .init(label: c.licenseNumber, path: \.licenseNumber, capitalization: .characters, id: "IdentityLicenseNumberEntry"),
            .init(label: c.email, path: \.email, keyboard: .emailAddress, content: .emailAddress, capitalization: .never, id: "IdentityEmailEntry"),
            .init(label: c.phone, path: \.phone, keyboard: .phonePad, content: .telephoneNumber, id: "IdentityPhoneEntry"),
            .init(label: c.address1, path: \.address1, content: .streetAddressLine1, id: "IdentityAddressOneEntry"),
            .init(label: c.address2, path: \.address2, content: .streetAddressLine2, id: "IdentityAddressTwoEntry"),
            .init(label: c.address3, path: \.address3, id: "IdentityAddressThreeEntry"),
            .init(label: c.city, path: \.city, content: .addressCity, id: "IdentityCityEntry"),
            .init(label: c.state, path: \.state, content: .addressState, id: "IdentityStateEntry"),
            .init(label: c.postalCode, path: \.postalCode, content: .postalCode, capitalization: .characters, id: "IdentityPostalCodeEntry"),
            .init(label: c.country, path: \.country, content: .countryName, id: "IdentityCountryEntry"),
        ]
    }

    public var body: some View {
        let copy = model.copy
        let all = lines(copy)
        let _ = order?.declare("identity", all.map { "identity." + $0.id })
        Picker(copy.title, selection: $model.title) {
            ForEach(copy.titleOptions.indices, id: \.self) { i in
                Text(copy.titleOptions[i]).tag(i)
            }
        }
        .accessibilityIdentifier("IdentityTitlePicker")
        .task {
            // Focus requests forwarded by C#: the parent's Return on the name field, or its own focus request.
            for await line in model.focus.stream() {
                focus = line
            }
        }
        ForEach(all.indices, id: \.self) { i in
            let line = all[i]
            let fieldID = "identity." + line.id
            VStack(alignment: .leading, spacing: 4) {
                Text(line.label).font(.caption).foregroundStyle(.secondary)
                TextField(line.label, text: $model[dynamicMember: line.path])
                    .formField(fieldID, local: $focus, equals: i)
                    .keyboardType(line.keyboard)
                    .textContentType(line.content)
                    .textInputAutocapitalization(line.capitalization)
                    .submitLabel(advance != nil || i < all.count - 1 ? .next : .done)
                    .onSubmit {
                        if let advance {
                            advance(fieldID)   // step 8: the parent's order, which may leave the section
                        } else {
                            focus = i < all.count - 1 ? i + 1 : nil
                        }
                    }
                    .accessibilityIdentifier(line.id)
            }
            .id(fieldID)   // step 8: a scroll target for the parent's ScrollViewReader
        }
    }
}
