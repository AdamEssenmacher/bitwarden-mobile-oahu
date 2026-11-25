import OahuRuntime
import SwiftUI

/// The lock page's content, hosted in the MAUI LockPage: the master password or PIN field with show/hide, the locked
/// and logged-in-as text, the biometric button and Unlock. The page keeps its title, the account avatar and switcher,
/// the log out item or options menu, the loading overlay and every dialog.
public struct LockScreen: View {
    @Bindable var model: LockModel
    @FocusState private var secretFocused: Bool

    public init(model: LockModel) {
        self.model = model
    }

    public var body: some View {
        let copy = model.copy
        let layout = model.layout
        let palette = model.palette
        Form {
            if layout.showSecretEntry {
                Section {
                    HStack {
                        secretField(layout: layout, copy: copy)
                        Button { model.toggleShowSecret() } label: {
                            Glyph(glyph: layout.showSecret ? BitwardenIcons.eyeSlash : BitwardenIcons.eye,
                                  color: palette.primaryColor)
                        }
                        .buttonStyle(.borderless)
                        .accessibilityLabel(copy.toggleVisibility)
                        .accessibilityHint(layout.showSecret ? copy.secretShownHint : copy.secretHiddenHint)
                        .accessibilityIdentifier(layout.pinEnabled ? "PinVisibilityToggle" : "PasswordVisibilityToggle")
                    }
                } header: {
                    Text(layout.pinEnabled ? copy.pin : copy.masterPassword)
                } footer: {
                    VStack(alignment: .leading, spacing: 10) {
                        Text(layout.lockedVerifyText)
                        Text(layout.loggedInAsText).accessibilityIdentifier("LoggedInAsLabel")
                    }
                }
            } else {
                Section {
                } footer: {
                    VStack(alignment: .leading, spacing: 10) {
                        Text(layout.lockedVerifyText)
                        Text(layout.loggedInAsText).accessibilityIdentifier("LoggedInAsLabel")
                    }
                }
            }
            Section {
                if layout.biometricInvalidated {
                    Text(copy.biometricInvalidated)
                        .font(.footnote.bold())
                        .foregroundStyle(palette.dangerColor)
                }
                if let biometric = layout.biometricButton {
                    Button(biometric) { model.useBiometrics() }
                        .frame(maxWidth: .infinity)
                        .accessibilityIdentifier("BiometricUnlockButton")
                }
                if layout.showSecretEntry {
                    Button {
                        model.submit()
                    } label: {
                        Text(copy.unlock).bold().frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.borderedProminent)
                    .listRowInsets(EdgeInsets())
                    .accessibilityIdentifier("UnlockVaultButton")
                }
            }
            .listRowBackground(Color.clear)
        }
        .scrollContentBackground(.hidden)
        .background(palette.backgroundColor)
        .environment(\.colorScheme, palette.colorScheme)
        .tint(palette.primaryColor)
        .task {
            // One-shot focus requests from C# (on appearing, after show/hide, after a biometric fallback). SwiftUI
            // has no API to place the cursor, so the requested position is ignored (docs/oahu/gaps.md).
            for await _ in model.focusSecret.stream() {
                secretFocused = true
            }
        }
    }

    @ViewBuilder
    private func secretField(layout: LockLayout, copy: LockCopy) -> some View {
        let label = layout.pinEnabled ? copy.pin : copy.masterPassword
        let text = layout.pinEnabled ? $model.pin : $model.masterPassword
        Group {
            // MAUI's MonoEntry swapped IsPassword in place; SwiftUI has no secure toggle on one field, so the eye
            // swaps a SecureField for a TextField and C# asks for focus again (TogglePassword's FocusSecretEntry).
            if layout.showSecret {
                TextField(label, text: text)
            } else {
                SecureField(label, text: text)
            }
        }
        .focused($secretFocused)
        .font(.system(.body, design: .monospaced))
        .keyboardType(layout.pinEnabled ? .numberPad : .default)
        .textContentType(layout.pinEnabled ? nil : .password)
        .autocorrectionDisabled()
        .textInputAutocapitalization(.never)
        .submitLabel(.go)
        .onSubmit { model.submit() }
        .accessibilityIdentifier(layout.pinEnabled ? "PinEntry" : "MasterPasswordEntry")
    }
}
