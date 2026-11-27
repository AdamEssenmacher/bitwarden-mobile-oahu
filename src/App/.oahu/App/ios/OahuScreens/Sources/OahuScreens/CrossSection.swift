import SwiftUI

// Step 8 (docs/oahu/08-cross-section.md): native channels between a parent screen and the sections it mounts with
// OahuMountView. OahuMountView draws the mounted screen as an ordinary view in the parent's hierarchy (no hosting
// controller), so environment values flow down into it and SwiftUI focus, preferences and scroll ids are shared with
// the parent. Nothing here is OAHU API: the app declares it once in its screen package and any section can use it.
// Turned on with BW_OAHU_CROSS=native; without it each screen keeps its own @FocusState (step 7).

/// A field's place in the form's shared focus: "<section>.<field>", for example "identity.email".
typealias FormFieldID = String

enum CrossSection {
    static let native = ProcessInfo.processInfo.environment["BW_OAHU_CROSS"] == "native"

    /// The section a field belongs to, for the parent's "editing" label.
    static func section(of id: FormFieldID?) -> String? {
        id.flatMap { $0.split(separator: ".").first.map(String.init) }
    }
}

extension EnvironmentValues {
    /// The parent's focus binding, set by the parent around its Form; nil when the screen draws alone (or step 7 mode).
    @Entry var formFocus: FocusState<FormFieldID?>.Binding? = nil
    /// Moves focus to the field after this one in the parent's order (Return); nil without a shared focus.
    @Entry var formAdvance: ((FormFieldID) -> Void)? = nil
    @Entry var formOrder: FormOrder? = nil
}

extension FocusedValues {
    /// The section whose field has focus, published by the field itself (a probe of a channel that needs no binding).
    @Entry var formEditingSection: String?
}

/// The form's full field order. A preference (below) does cross the mount, but inside a Form/List it carries only the
/// rows built so far, in the order they were built, so each screen also declares its whole list here: the section
/// under its name, the parent as a template naming where each section goes ("@identity"). A plain class, not observed:
/// a section declares from its body, and only actions read it.
@MainActor final class FormOrder {
    private var declared: [String: [FormFieldID]] = [:]

    func declare(_ section: String, _ ids: [FormFieldID]) {
        if declared[section] != ids { declared[section] = ids }
    }

    var all: [FormFieldID] {
        (declared["parent"] ?? []).flatMap { id in
            id.hasPrefix("@") ? declared[String(id.dropFirst())] ?? [] : [id]
        }
    }
}

/// Every built field's id, collected from the parent and from inside its mounted sections (logged for comparison).
struct FormFieldOrderKey: PreferenceKey {
    static let defaultValue: [FormFieldID] = []
    static func reduce(value: inout [FormFieldID], nextValue: () -> [FormFieldID]) {
        value += nextValue()
    }
}

extension View {
    /// Binds a field to the form's shared focus when the parent provides one, else to the view's own `local` focus.
    func formField<Local: Hashable>(_ id: FormFieldID, local: FocusState<Local?>.Binding, equals value: Local) -> some View {
        modifier(FormFieldModifier(id: id, local: local, value: value))
    }
}

private struct FormFieldModifier<Local: Hashable>: ViewModifier {
    @Environment(\.formFocus) private var shared
    let id: FormFieldID
    let local: FocusState<Local?>.Binding
    let value: Local

    func body(content: Content) -> some View {
        if let shared {
            content
                .focused(shared, equals: id)
                .preference(key: FormFieldOrderKey.self, value: [id])
                .focusedValue(\.formEditingSection, CrossSection.section(of: id))
        } else {
            content.focused(local, equals: value)
        }
    }
}
