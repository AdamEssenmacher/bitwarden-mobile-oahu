import OahuRuntime
import SwiftUI

/// Settings > Folders, hosted in the MAUI FoldersPage. The page keeps its title and its Close and Add toolbar items,
/// so this screen draws only the list and its empty state.
public struct FoldersScreen: View {
    var model: FoldersModel

    public init(model: FoldersModel) {
        self.model = model
    }

    public var body: some View {
        content
            .scrollContentBackground(.hidden)
            .background(model.palette.backgroundColor)
            .foregroundStyle(model.palette.textColor)
            .tint(model.palette.primaryColor)
            .environment(\.colorScheme, model.palette.colorScheme)
    }

    @ViewBuilder
    private var content: some View {
        if model.showNoData {
            ContentUnavailableView {
                Label(model.copy.noFolders, systemImage: "folder")
            }
            .accessibilityIdentifier("NoFoldersLabel")
        } else {
            List(model.folders) { folder in
                Button {
                    model.open(folder.key)
                } label: {
                    HStack {
                        Label {
                            Text(folder.name)
                                .lineLimit(1)
                                .truncationMode(.tail)
                        } icon: {
                            Image(systemName: "folder")
                                .foregroundStyle(model.palette.primaryColor)
                        }
                        Spacer()
                        Image(systemName: "chevron.right")
                            .font(.footnote.weight(.semibold))
                            .foregroundStyle(.tertiary)
                    }
                    .contentShape(Rectangle())
                }
                .tint(model.palette.textColor)
                .accessibilityIdentifier("FolderCell")
            }
            .listStyle(.insetGrouped)
        }
    }
}
