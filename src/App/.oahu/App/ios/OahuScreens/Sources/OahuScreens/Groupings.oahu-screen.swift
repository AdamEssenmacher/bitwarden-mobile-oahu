import OahuRuntime
import SwiftUI

/// The vault list (GroupingsPage): "My Vault" and every drill-down. The MAUI page keeps its title, toolbar items
/// (account switcher, search, add), the account overlay and every dialog; this screen draws the filter bar, the
/// empty state and the sectioned list with pull to refresh.
public struct GroupingsScreen: View {
    var model: GroupingsModel

    public init(model: GroupingsModel) {
        self.model = model
    }

    public var body: some View {
        let palette = model.palette
        Group {
            if let empty = model.empty {
                emptyState(empty, palette)
            } else {
                list(palette)
            }
        }
        .background(palette.backgroundColor)
        .environment(\.colorScheme, palette.colorScheme)
    }

    private func emptyState(_ empty: GroupingsEmpty, _ palette: AppPalette) -> some View {
        VStack(spacing: 20) {
            if let filter = model.vaultFilter {
                filterBar(filter, palette)
            }
            Spacer()
            Text(empty.text)
                .multilineTextAlignment(.center)
                .foregroundStyle(palette.textColor)
                .accessibilityIdentifier("NoDataDisplayed")
            if empty.showAddButton {
                Button(model.copy.addAnItem) { model.add() }
                    .buttonStyle(.borderedProminent)
                    .tint(palette.primaryColor)
            }
            Spacer()
        }
        .padding(.horizontal, 20)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private func filterBar(_ text: String, _ palette: AppPalette) -> some View {
        HStack {
            Text(text)
                .lineLimit(1)
                .foregroundStyle(palette.mutedColor)
                .accessibilityLabel(model.copy.filter)
            Spacer()
            Button {
                model.chooseVaultFilter()
            } label: {
                Image(systemName: "ellipsis")
                    .foregroundStyle(palette.mutedColor)
                    .frame(width: 44, height: 32)
            }
            .buttonStyle(.borderless)
            .accessibilityLabel(model.copy.filter)
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 6)
    }

    private func list(_ palette: AppPalette) -> some View {
        // The keyed list is flat (OAHU lists have no sections): split it at each header row.
        let sections = GroupingsSection.split(model.rows)
        return List {
            if let filter = model.vaultFilter {
                filterBar(filter, palette)
                    .listRowInsets(EdgeInsets())
                    .listRowBackground(palette.backgroundColor)
            }
            ForEach(sections) { section in
                Section {
                    ForEach(section.rows) { row in
                        rowView(row, palette)
                            .listRowBackground(palette.backgroundColor)
                            .listRowSeparatorTint(palette.separatorColor)
                    }
                } header: {
                    if let header = section.header {
                        HStack {
                            Text(header.title)
                            Spacer()
                            Text(header.count)
                                .accessibilityIdentifier("SectionItemCount")
                        }
                        .font(.footnote.weight(.semibold))
                        .foregroundStyle(palette.headerTextColor)
                    }
                }
            }
        }
        .listStyle(.plain)
        .scrollContentBackground(.hidden)
        .refreshable {
            // Waits for the view model's Refreshing flag, which a sync clears when it completes.
            await OahuWait.run { model.refresh() } until: { !model.isRefreshing }
        }
        .overlay {
            if model.isLoading && model.rows.isEmpty {
                ProgressView(model.copy.loading)
            }
        }
    }

    @ViewBuilder
    private func rowView(_ row: GroupingsRow, _ palette: AppPalette) -> some View {
        switch row {
        case .sectionHeader:
            EmptyView()
        case let .grouping(key, glyph, name, count):
            HStack(spacing: 12) {
                Glyph(glyph: glyph, color: palette.mutedColor)
                    .frame(width: 32)
                Text(name)
                    .lineLimit(1)
                    .foregroundStyle(palette.textColor)
                Spacer()
                Text(count)
                    .foregroundStyle(palette.mutedColor)
                    .accessibilityIdentifier("ItemCountLabel")
            }
            .contentShape(Rectangle())
            .onTapGesture { model.select(key) }
            .accessibilityElement(children: .combine)
            .accessibilityAddTraits(.isButton)
        case let .cipherItem(key, name, subtitle, glyph, iconUrl, shared, hasAttachments, canEdit):
            HStack(spacing: 12) {
                CipherIcon(url: iconUrl, glyph: glyph, palette: palette)
                VStack(alignment: .leading, spacing: 2) {
                    HStack(spacing: 5) {
                        Text(name)
                            .lineLimit(1)
                            .foregroundStyle(palette.textColor)
                        if shared {
                            Glyph(glyph: BitwardenIcons.collection, size: 14, color: palette.mutedColor)
                                .accessibilityLabel(model.copy.shared)
                        }
                        if hasAttachments {
                            Glyph(glyph: BitwardenIcons.paperclip, size: 14, color: palette.mutedColor)
                                .accessibilityLabel(model.copy.attachments)
                        }
                    }
                    if let subtitle {
                        Text(subtitle)
                            .font(.subheadline)
                            .lineLimit(1)
                            .foregroundStyle(palette.mutedColor)
                    }
                }
                Spacer(minLength: 0)
                Button {
                    model.showOptions(key)
                } label: {
                    Image(systemName: "ellipsis")
                        .foregroundStyle(palette.mutedColor)
                        .frame(width: 44, height: 44)
                }
                .buttonStyle(.borderless)
                .accessibilityLabel(model.copy.options)
                .accessibilityIdentifier("CipherOptionsButton")
            }
            .contentShape(Rectangle())
            .onTapGesture { model.select(key) }
            .contextMenu {
                Button(model.copy.view) { model.select(key) }
                if canEdit {
                    Button(model.copy.edit) { model.edit(key) }
                }
                Button(model.copy.options) { model.showOptions(key) }
            }
            .swipeActions(edge: .trailing) {
                Button(model.copy.options) { model.showOptions(key) }
                    .tint(palette.primaryColor)
            }
        case let .totpItem(key, name, subtitle, glyph, iconUrl, code, seconds, progress):
            let low = (Int(seconds) ?? 30) < 7
            HStack(spacing: 10) {
                CipherIcon(url: iconUrl, glyph: glyph, palette: palette)
                VStack(alignment: .leading, spacing: 2) {
                    Text(name).lineLimit(1).foregroundStyle(palette.textColor)
                    if let subtitle {
                        Text(subtitle).font(.subheadline).lineLimit(1).foregroundStyle(palette.mutedColor)
                    }
                }
                Spacer(minLength: 0)
                TotpRing(seconds: seconds, progress: progress,
                         color: low ? palette.dangerColor : palette.primaryColor, track: palette.separatorColor)
                Text(code ?? "")
                    .font(.title3.monospaced())
                    .foregroundStyle(low ? palette.dangerColor : palette.primaryColor)
                    .contentTransition(.numericText())
                Button {
                    model.copyTotp(key)
                } label: {
                    Glyph(glyph: BitwardenIcons.clone, color: palette.primaryColor)
                        .frame(width: 36, height: 44)
                }
                .buttonStyle(.borderless)
                .accessibilityLabel(model.copy.copyTotp)
            }
            .contentShape(Rectangle())
            .onTapGesture { model.select(key) }
        }
    }
}

/// One section of the flat row list: its header row (if any) and the rows up to the next header.
struct GroupingsSection: Identifiable {
    let id: OahuRowID
    let header: (title: String, count: String)?
    var rows: [GroupingsRow]

    static func split(_ rows: KeyedList<GroupingsRow>) -> [GroupingsSection] {
        var sections: [GroupingsSection] = []
        for row in rows {
            if case let .sectionHeader(_, title, count) = row {
                sections.append(GroupingsSection(id: row.id, header: (title, count), rows: []))
            } else if sections.isEmpty {
                sections.append(GroupingsSection(id: OahuRowID("~top"), header: nil, rows: [row]))
            } else {
                sections[sections.count - 1].rows.append(row)
            }
        }
        return sections
    }
}
