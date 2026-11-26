import GameController
import QuartzCore
import SwiftUI
import UIKit

/// Step 6 keystroke probe (docs/oahu/06-keystroke-repro.md), active only when BW_OAHU_REPRO_RUN is set. Logs, with the
/// uptime clock C#'s Stopwatch uses: "focus" and "ui" (the focused field's UIKit text, sampled every frame, logged when
/// it changes, so programmatic rewrites show), "uiChange" (UIKit's text-did-change notification), "model" (the
/// generated model's settled notes, sampled every frame) and "key" (a character this probe injects when
/// BW_OAHU_REPRO_MODE=inject: one insertText every 50 ms from the frame focus is seen). Written to
/// Documents/repro977/<run id>.sw.jsonl one second after the last entry.
@MainActor
enum OahuKeyProbe {
    static let run = ProcessInfo.processInfo.environment["BW_OAHU_REPRO_RUN"]
    static let mode = ProcessInfo.processInfo.environment["BW_OAHU_REPRO_MODE"] ?? ""
    static let text = ProcessInfo.processInfo.environment["BW_OAHU_REPRO_TEXT"] ?? "The quick brown fox jumps over"
    private static var lines: [String] = []
    private static var seq = 0
    private static var sampler: Sampler?
    private static var flushGeneration = 0
    /// Set by the screen while the notes field has SwiftUI focus: the probe samples and injects only then (the add
    /// form focuses the name field when it opens).
    static var notesFocused = false {
        didSet { if notesFocused != oldValue { log("notesFocused", "\(notesFocused)") } }
    }

    static func log(_ kind: String, _ text: String) {
        guard let run else { return }
        seq += 1
        let escaped = text.replacingOccurrences(of: "\\", with: "\\\\").replacingOccurrences(of: "\"", with: "\\\"")
            .replacingOccurrences(of: "\n", with: "\\n")
        lines.append(
            String(
                format: "{\"seq\":%d,\"t\":%.6f,\"kind\":\"%@\",\"text\":\"%@\"}", seq, CACurrentMediaTime(), kind,
                escaped))
        flushGeneration &+= 1
        let generation = flushGeneration
        DispatchQueue.main.asyncAfter(deadline: .now() + 1) {
            guard generation == flushGeneration else { return }
            let fm = FileManager.default
            guard let docs = fm.urls(for: .documentDirectory, in: .userDomainMask).first else { return }
            let dir = docs.appendingPathComponent("repro977", isDirectory: true)
            try? fm.createDirectory(at: dir, withIntermediateDirectories: true)
            try? (lines.joined(separator: "\n") + "\n").write(
                to: dir.appendingPathComponent("\(run).sw.jsonl"), atomically: true, encoding: .utf8)
        }
    }

    static func startIfAsked(model: @escaping @MainActor () -> String) {
        guard run != nil, sampler == nil else { return }
        log("start", mode)
        let sampler = Sampler(model: model)
        sampler.start()
        self.sampler = sampler
    }

    @MainActor
    final class Sampler: NSObject {
        private let model: @MainActor () -> String
        private var lastUI: String?
        private var lastModel: String?
        private var injecting = false
        private weak var notesInput: (UIResponder & UITextInput)?
        private static weak var found: UIResponder?

        init(model: @escaping @MainActor () -> String) {
            self.model = model
        }

        func start() {
            CADisplayLink(target: self, selector: #selector(tick)).add(to: .main, forMode: .common)
            for name in [UITextField.textDidChangeNotification, UITextView.textDidChangeNotification] {
                NotificationCenter.default.addObserver(self, selector: #selector(changed(_:)), name: name, object: nil)
            }
            // Round 2 (hardware-keyboard path): the software keyboard's show/hide, the hardware keyboard's presence
            // and every HID key the app receives (GameController), independent of UIKit text.
            let center = NotificationCenter.default
            center.addObserver(forName: UIResponder.keyboardWillShowNotification, object: nil, queue: .main) { note in
                let frame = (note.userInfo?[UIResponder.keyboardFrameEndUserInfoKey] as? CGRect) ?? .zero
                MainActor.assumeIsolated { OahuKeyProbe.log("kbShow", "\(Int(frame.height))") }
            }
            center.addObserver(forName: UIResponder.keyboardWillHideNotification, object: nil, queue: .main) { _ in
                MainActor.assumeIsolated { OahuKeyProbe.log("kbHide", "") }
            }
            center.addObserver(forName: .GCKeyboardDidConnect, object: nil, queue: .main) { _ in
                MainActor.assumeIsolated {
                    OahuKeyProbe.log("hwKeyboard", "connected")
                    Sampler.watchKeys()
                }
            }
            center.addObserver(forName: .GCKeyboardDidDisconnect, object: nil, queue: .main) { _ in
                MainActor.assumeIsolated { OahuKeyProbe.log("hwKeyboard", "disconnected") }
            }
            OahuKeyProbe.log("hwKeyboard", GCKeyboard.coalesced == nil ? "none" : "present")
            Sampler.watchKeys()
        }

        private static func watchKeys() {
            guard let input = GCKeyboard.coalesced?.keyboardInput else { return }
            input.keyChangedHandler = { _, _, keyCode, pressed in
                let code = "\(keyCode.rawValue)"
                let kind = pressed ? "hidDown" : "hidUp"
                if Thread.isMainThread {
                    MainActor.assumeIsolated { OahuKeyProbe.log(kind, code) }
                } else {
                    DispatchQueue.main.async { MainActor.assumeIsolated { OahuKeyProbe.log(kind, code + " (async)") } }
                }
            }
        }

        @objc private func changed(_ note: Notification) {
            let text = (note.object as? UITextField)?.text ?? (note.object as? UITextView)?.text ?? "?"
            OahuKeyProbe.log("uiChange", text)
        }

        @objc private func tick() {
            let value = model()
            if value != lastModel {
                lastModel = value
                OahuKeyProbe.log("model", value)
            }
            guard OahuKeyProbe.notesFocused else {
                // Round 2: keep sampling the notes field after it loses focus (a hardware Return ends editing), so
                // the run's final UIKit text is logged.
                if let input = notesInput, let all = input.textRange(from: input.beginningOfDocument, to: input.endOfDocument) {
                    let text = input.text(in: all) ?? ""
                    if text != lastUI {
                        lastUI = text
                        OahuKeyProbe.log("uiUnfocused", text)
                    }
                }
                return
            }
            Self.found = nil
            UIApplication.shared.sendAction(#selector(UIResponder.oahuKeyProbeFound), to: nil, from: nil, for: nil)
            guard let input = Self.found as? UITextInput,
                let all = input.textRange(from: input.beginningOfDocument, to: input.endOfDocument)
            else { return }
            if notesInput == nil { notesInput = input as? UIResponder & UITextInput }
            let text = input.text(in: all) ?? ""
            if text != lastUI {
                if lastUI == nil { OahuKeyProbe.log("focus", text) }
                lastUI = text
                OahuKeyProbe.log("ui", text)
            }
            if OahuKeyProbe.mode == "inject" && !injecting {
                injecting = true
                let characters = Array(OahuKeyProbe.text)
                Task { @MainActor in
                    for character in characters {
                        OahuKeyProbe.log("key", String(character))
                        input.insertText(String(character))
                        try? await Task.sleep(for: .milliseconds(50))
                    }
                    OahuKeyProbe.log("injected", "")
                }
            }
        }

        fileprivate static func setFound(_ responder: UIResponder) { found = responder }
    }
}

extension UIResponder {
    @objc fileprivate func oahuKeyProbeFound() {
        MainActor.assumeIsolated { OahuKeyProbe.Sampler.setFound(self) }
    }
}
