import OahuRuntime
import SwiftUI

/// Step 7, the D25 trial: the login's URIs as their own contract, an editable keyed list, mounted by the add/edit view
/// model and drawn by the add/edit screen inside a Form Section. Rows use the documented row text helper.
public struct UriSectionScreen: View {
    @Bindable var model: UriSectionModel
    @State private var texts = OahuRowTexts<EditUriRow>(\.uri)
    @FocusState private var focus: String?
    @Environment(\.appPalette) private var palette

    public init(model: UriSectionModel) {
        self.model = model
    }

    public var body: some View {
        let copy = model.copy
        let locked = model.locked
        ForEach(model.uris) { row in
            VStack(alignment: .leading, spacing: 4) {
                Text(copy.uri).font(.caption).foregroundStyle(.secondary)
                HStack {
                    TextField(copy.uri, text: texts.binding(for: row.id, in: { model.uris }) {
                        model.setUriText(RowText(key: row.id.rawValue, text: $0))
                    })
                    .focused($focus, equals: row.id.rawValue)
                    .keyboardType(.URL)
                    .textContentType(.URL)
                    .textInputAutocapitalization(.never)
                    .autocorrectionDisabled()
                    .disabled(locked)
                    .accessibilityIdentifier("LoginUriEntry")
                    if !locked {
                        Button { model.uriOptions(row.id.rawValue) } label: {
                            Glyph(glyph: BitwardenIcons.cog, color: palette?.primaryColor)
                        }
                        .buttonStyle(.borderless)
                        .accessibilityLabel(copy.options)
                        .accessibilityIdentifier("LoginUriOptionsButton")
                    }
                }
            }
        }
        if !locked {
            Button(copy.newUri) { model.addUri() }
                .accessibilityIdentifier("LoginAddNewUriButton")
                .task {
                    for await key in model.focusRow.stream() {
                        focus = key
                    }
                }
        }
    }
}
