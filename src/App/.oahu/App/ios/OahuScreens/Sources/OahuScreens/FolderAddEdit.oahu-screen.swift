import OahuRuntime
import SwiftUI

/// Adding or editing a folder, hosted in the MAUI FolderAddEditPage. Cancel, Save and the options menu stay MAUI
/// toolbar items; Return on the keyboard saves, as the MAUI Entry's Go key did.
public struct FolderAddEditScreen: View {
    @Bindable var model: FolderAddEditModel
    @FocusState private var nameFocused: Bool

    public init(model: FolderAddEditModel) {
        self.model = model
    }

    public var body: some View {
        Form {
            Section {
                TextField(model.copy.nameLabel, text: $model.name)
                    .focused($nameFocused)
                    .submitLabel(.go)
                    .onSubmit { model.submit() }
                    .accessibilityIdentifier("FolderNameEntry")
            } header: {
                Text(model.copy.nameLabel)
            }
        }
        .task {
            guard model.focusName else { return }
            // Wait for the modal presentation to settle, as the MAUI page's RequestFocus did.
            try? await Task.sleep(for: .milliseconds(500))
            nameFocused = true
        }
    }
}
