import OahuRuntime
import SwiftUI

/// Viewing one cipher (CipherDetailsPage). The MAUI page keeps its title, Close, Edit/Restore and the options sheet,
/// and every dialog (re-prompt, check password, download). Hidden values arrive only while shown.
public struct CipherDetailsScreen: View {
    var model: CipherDetailsModel

    public init(model: CipherDetailsModel) {
        self.model = model
    }

    public var body: some View {
        let palette = model.palette
        Group {
            if let details = model.details {
                content(details, model.copy, palette)
            } else {
                ProgressView().frame(maxWidth: .infinity, maxHeight: .infinity)
            }
        }
        .background(palette.backgroundColor)
        .environment(\.colorScheme, palette.colorScheme)
    }

    private func content(_ d: CipherDetails, _ copy: CipherDetailsCopy, _ p: AppPalette) -> some View {
        List {
            Section {
                ValueRow(label: copy.name, value: d.name, palette: p)
                if let login = d.login {
                    loginRows(login, copy, p)
                }
                if let card = d.card {
                    cardRows(card, copy, p)
                }
                if let identity = d.identity {
                    identityRows(identity, copy, p)
                }
            } header: {
                header(copy.itemInformation, p)
            }

            if !d.uris.isEmpty {
                Section {
                    ForEach(Array(d.uris.enumerated()), id: \.offset) { index, uri in
                        ValueRow(label: uri.label, value: uri.value, palette: p) {
                            if uri.canLaunch {
                                IconButton(glyph: BitwardenIcons.shareSquare, label: copy.launch, palette: p) {
                                    model.launchUri(index)
                                }
                            }
                            IconButton(glyph: BitwardenIcons.clone, label: copy.copy, palette: p) {
                                model.copyUri(index)
                            }
                        }
                    }
                } header: {
                    header(copy.uris, p)
                }
            }

            if let notes = d.notes {
                Section {
                    Text(notes)
                        .foregroundStyle(p.textColor)
                        .textSelection(.enabled)
                        .listRowBackground(p.backgroundColor)
                } header: {
                    header(copy.notes, p)
                }
            }

            if !d.fields.isEmpty {
                Section {
                    ForEach(Array(d.fields.enumerated()), id: \.offset) { index, field in
                        fieldRow(index, field, copy, p)
                    }
                } header: {
                    header(copy.customFields, p)
                }
            }

            if !d.attachments.isEmpty {
                Section {
                    ForEach(d.attachments, id: \.id) { attachment in
                        HStack {
                            Text(attachment.fileName).foregroundStyle(p.textColor).lineLimit(1)
                            Spacer()
                            Text(attachment.size).font(.footnote).foregroundStyle(p.mutedColor)
                            IconButton(glyph: BitwardenIcons.download, label: copy.download, palette: p) {
                                model.downloadAttachment(attachment.id)
                            }
                        }
                        .listRowBackground(p.backgroundColor)
                    }
                } header: {
                    header(copy.attachments, p)
                }
            }

            Section {
                ForEach(Array(d.footer.enumerated()), id: \.offset) { _, line in
                    footerLine(line, p)
                }
            }
            .listRowBackground(Color.clear)
        }
        .listStyle(.insetGrouped)
        .scrollContentBackground(.hidden)
    }

    private func header(_ text: String, _ p: AppPalette) -> some View {
        Text(text).foregroundStyle(Color(argb: p.boxHeaderText))
    }

    @ViewBuilder
    private func loginRows(_ login: LoginDetails, _ copy: CipherDetailsCopy, _ p: AppPalette) -> some View {
        if let username = login.username {
            ValueRow(label: copy.username, value: username, palette: p) {
                IconButton(glyph: BitwardenIcons.clone, label: copy.copy, palette: p) { model.copyValue("LoginUsername") }
            }
        }
        if let password = login.password {
            SecretRow(label: copy.password, secret: password, copy: copy, palette: p,
                      onToggle: { model.togglePassword() }, onCopy: { model.copyValue("LoginPassword") }) {
                if login.canCheckPassword {
                    IconButton(glyph: BitwardenIcons.checkCircle, label: copy.checkPassword, palette: p) {
                        model.checkPassword()
                    }
                }
            }
        }
        if let passkey = login.passkeyCreated {
            ValueRow(label: copy.passkey, value: passkey, palette: p, muted: true)
        }
        if login.hasTotp, let totp = model.totp {
            totpRow(totp, copy, p)
        }
    }

    private func totpRow(_ totp: TotpDisplay, _ copy: CipherDetailsCopy, _ p: AppPalette) -> some View {
        let color = totp.low ? p.dangerColor : p.textColor
        return HStack {
            VStack(alignment: .leading, spacing: 4) {
                Text(copy.verificationCode).font(.footnote).foregroundStyle(p.mutedColor)
                if totp.premiumRequired {
                    Text(copy.premiumRequired).font(.footnote).foregroundStyle(p.dangerColor)
                } else {
                    Text(totp.code ?? "")
                        .font(.title2.monospaced())
                        .foregroundStyle(color)
                        .contentTransition(.numericText())
                }
            }
            Spacer()
            if !totp.premiumRequired {
                TotpRing(seconds: totp.seconds, progress: totp.progress,
                         color: totp.low ? p.dangerColor : p.primaryColor, track: p.separatorColor)
            }
            if totp.canCopy {
                IconButton(glyph: BitwardenIcons.clone, label: copy.copy, palette: p) { model.copyValue("LoginTotp") }
            }
        }
        .listRowBackground(p.backgroundColor)
    }

    @ViewBuilder
    private func cardRows(_ card: CardDetails, _ copy: CipherDetailsCopy, _ p: AppPalette) -> some View {
        if let holder = card.cardholderName {
            ValueRow(label: copy.cardholderName, value: holder, palette: p)
        }
        if let number = card.number {
            SecretRow(label: copy.number, secret: number, copy: copy, palette: p,
                      onToggle: { model.toggleCardNumber() }, onCopy: { model.copyValue("CardNumber") }) {}
        }
        if let brand = card.brand {
            ValueRow(label: copy.brand, value: brand, palette: p)
        }
        if let expiration = card.expiration {
            ValueRow(label: copy.expiration, value: expiration, palette: p)
        }
        if let code = card.code {
            SecretRow(label: copy.securityCode, secret: code, copy: copy, palette: p,
                      onToggle: { model.toggleCardCode() }, onCopy: { model.copyValue("CardCode") }) {}
        }
    }

    @ViewBuilder
    private func identityRows(_ i: IdentityDetails, _ copy: CipherDetailsCopy, _ p: AppPalette) -> some View {
        let rows: [(String, String?)] = [
            (copy.identityName, i.name), (copy.username, i.username), (copy.company, i.company), (copy.ssn, i.ssn),
            (copy.passportNumber, i.passportNumber), (copy.licenseNumber, i.licenseNumber), (copy.email, i.email),
            (copy.phone, i.phone),
        ]
        ForEach(Array(rows.enumerated()), id: \.offset) { _, row in
            if let value = row.1 {
                ValueRow(label: row.0, value: value, palette: p)
            }
        }
        if !i.address.isEmpty {
            ValueRow(label: copy.address, value: i.address.joined(separator: "\n"), palette: p)
        }
    }

    @ViewBuilder
    private func fieldRow(_ index: Int, _ field: FieldRow, _ copy: CipherDetailsCopy, _ p: AppPalette) -> some View {
        switch field {
        case let .textField(name, value, canCopy):
            ValueRow(label: name, value: value ?? "", palette: p) {
                if canCopy {
                    IconButton(glyph: BitwardenIcons.clone, label: copy.copy, palette: p) { model.copyField(index) }
                }
            }
        case let .hiddenField(name, value):
            SecretRow(label: name, secret: value, copy: copy, palette: p,
                      onToggle: { model.toggleField(index) }, onCopy: { model.copyField(index) }) {}
        case let .booleanField(name, value):
            HStack {
                Text(name).foregroundStyle(p.textColor)
                Spacer()
                Glyph(glyph: value ? BitwardenIcons.checkSquare : BitwardenIcons.square, color: p.mutedColor)
            }
            .listRowBackground(p.backgroundColor)
        case let .linkedField(name, linkedTo):
            HStack {
                VStack(alignment: .leading, spacing: 4) {
                    Text(name).font(.footnote).foregroundStyle(p.mutedColor)
                    HStack(spacing: 4) {
                        Glyph(glyph: BitwardenIcons.link, size: 16, color: p.mutedColor)
                        Text(linkedTo).foregroundStyle(p.textColor)
                    }
                }
                Spacer()
            }
            .listRowBackground(p.backgroundColor)
        }
    }

    @ViewBuilder
    private func footerLine(_ line: FooterLine, _ p: AppPalette) -> some View {
        let text = Text(line.label).bold() + Text(" ") + Text(line.value).foregroundColor(line.link ? p.primaryColor : nil)
        if line.link {
            Button { model.openPasswordHistory() } label: { text.font(.footnote).foregroundStyle(p.mutedColor) }
                .buttonStyle(.plain)
        } else {
            text.font(.footnote).foregroundStyle(p.mutedColor)
        }
    }
}

/// A label above a value, with optional trailing buttons (MAUI's box-row).
private struct ValueRow<Trailing: View>: View {
    let label: String
    let value: String
    let palette: AppPalette
    var muted = false
    @ViewBuilder var trailing: () -> Trailing

    init(label: String, value: String, palette: AppPalette, muted: Bool = false,
         @ViewBuilder trailing: @escaping () -> Trailing = { EmptyView() }) {
        self.label = label
        self.value = value
        self.palette = palette
        self.muted = muted
        self.trailing = trailing
    }

    var body: some View {
        HStack {
            VStack(alignment: .leading, spacing: 4) {
                Text(label).font(.footnote).foregroundStyle(palette.mutedColor)
                Text(value)
                    .foregroundStyle(muted ? palette.mutedColor : palette.textColor)
                    .textSelection(.enabled)
            }
            Spacer(minLength: 8)
            trailing()
        }
        .listRowBackground(palette.backgroundColor)
    }
}

/// A hidden value: the mask, or (only once C# has sent it) the value colored by letters, digits and symbols.
private struct SecretRow<Extra: View>: View {
    let label: String
    let secret: SecretValue
    let copy: CipherDetailsCopy
    let palette: AppPalette
    let onToggle: () -> Void
    let onCopy: () -> Void
    @ViewBuilder var extra: () -> Extra

    var body: some View {
        HStack {
            VStack(alignment: .leading, spacing: 4) {
                Text(label).font(.footnote).foregroundStyle(palette.mutedColor)
                if let runs = secret.revealed {
                    runs.reduce(Text("")) { text, run in
                        text + Text(run.text).foregroundColor(color(run.kind))
                    }
                    .font(.body.monospaced())
                    .textSelection(.enabled)
                } else {
                    Text(secret.masked).font(.body.monospaced()).foregroundStyle(palette.textColor)
                }
            }
            Spacer(minLength: 8)
            extra()
            if secret.canReveal {
                IconButton(glyph: secret.revealed == nil ? BitwardenIcons.eye : BitwardenIcons.eyeSlash,
                           label: secret.revealed == nil ? copy.show : copy.hide, palette: palette, action: onToggle)
            }
            if secret.canCopy {
                IconButton(glyph: BitwardenIcons.clone, label: copy.copy, palette: palette, action: onCopy)
            }
        }
        .listRowBackground(palette.backgroundColor)
    }

    private func color(_ kind: TextRunKind) -> Color {
        switch kind {
        case .letters: palette.textColor
        case .digits: Color(argb: palette.passwordNumber)
        case .symbols: Color(argb: palette.passwordSpecial)
        }
    }
}

private struct IconButton: View {
    let glyph: String
    let label: String
    let palette: AppPalette
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Glyph(glyph: glyph, size: 20, color: palette.primaryColor)
                .frame(width: 40, height: 40)
        }
        .buttonStyle(.borderless)
        .accessibilityLabel(label)
    }
}
