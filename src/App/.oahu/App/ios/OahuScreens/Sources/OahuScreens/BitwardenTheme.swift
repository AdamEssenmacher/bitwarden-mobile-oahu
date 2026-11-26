import CoreText
import SwiftUI

/// Bitwarden's icon font (bwi-font.ttf, bundled in this package) and the few glyphs the screens draw themselves. Row
/// glyphs come from C# (BitwardenIcons); these are copies of the same code points for the screens' own buttons.
enum BitwardenIcons {
    static let clone = "\u{e905}"
    static let shareSquare = "\u{e903}"
    static let checkCircle = "\u{e912}"
    static let eye = "\u{e913}"
    static let eyeSlash = "\u{e96d}"
    static let download = "\u{e928}"
    static let collection = "\u{e930}"
    static let paperclip = "\u{e93f}"
    static let link = "\u{e954}"
    static let square = "\u{e92f}"
    static let checkSquare = "\u{e93b}"
    static let generate = "\u{e90e}"
    static let questionCircle = "\u{e923}"
    static let camera = "\u{e968}"
    static let cog = "\u{e910}"

    /// Registers the font with the process once (fonts in a Swift package aren't in the app's UIAppFonts).
    static let registered: Bool = {
        guard let url = Bundle.module.url(forResource: "bwi-font", withExtension: "ttf") else { return false }
        var error: Unmanaged<CFError>?
        let ok = CTFontManagerRegisterFontsForURL(url as CFURL, .process, &error)
        // Already registered (MAUI registers the same family when a MAUI page uses it) is fine.
        return ok || UIFont(name: "bwi-font", size: 12) != nil
    }()
}

/// A glyph of the icon font.
struct Glyph: View {
    let glyph: String
    var size: CGFloat = 22
    var color: Color? = nil

    var body: some View {
        let _ = BitwardenIcons.registered
        Text(glyph)
            .font(.custom("bwi-font", size: size, relativeTo: .body))
            .foregroundStyle(color ?? .secondary)
            .accessibilityHidden(true)
    }
}

extension AppPalette {
    var backgroundColor: Color { background.color }
    var textColor: Color { text.color }
    var mutedColor: Color { muted.color }
    var primaryColor: Color { primary.color }
    var dangerColor: Color { danger.color }
    var headerBackgroundColor: Color { listHeaderBackground.color }
    var headerTextColor: Color { listHeaderText.color }
    var separatorColor: Color { separator.color }
    var colorScheme: ColorScheme { isDark ? .dark : .light }
}

/// The website icon by URL, with the type glyph while it loads, when it fails, and when there is none
/// (MAUI: CachedImage over an IconLabel).
struct CipherIcon: View {
    let url: String?
    let glyph: String
    let palette: AppPalette

    var body: some View {
        Group {
            if let url, let parsed = URL(string: url) {
                AsyncImage(url: parsed) { phase in
                    if case .success(let image) = phase {
                        image.resizable().scaledToFit()
                    } else {
                        Glyph(glyph: glyph, color: palette.mutedColor)
                    }
                }
            } else {
                Glyph(glyph: glyph, color: palette.mutedColor)
            }
        }
        .frame(width: 22, height: 22)
        .frame(width: 32)
    }
}

/// The verification code's countdown ring (MAUI: CircularProgressbarView with the seconds inside).
struct TotpRing: View {
    let seconds: String
    let progress: Double
    let color: Color
    let track: Color

    var body: some View {
        ZStack {
            Circle().stroke(track, lineWidth: 3)
            Circle()
                .trim(from: 0, to: max(0, min(1, progress / 100)))
                .stroke(color, style: StrokeStyle(lineWidth: 3, lineCap: .round))
                .rotationEffect(.degrees(-90))
                .animation(.linear(duration: 1), value: progress)
            Text(seconds)
                .font(.caption2.monospacedDigit())
                .foregroundStyle(color)
        }
        .frame(width: 30, height: 30)
        .accessibilityElement(children: .combine)
    }
}
