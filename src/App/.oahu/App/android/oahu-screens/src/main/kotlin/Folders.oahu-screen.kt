package com.oahu.appapp

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.ListItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp

/**
 * Settings > Folders, hosted in the MAUI FoldersPage. The page keeps its title and toolbar; this screen draws the
 * list, its empty state and the floating add button the MAUI page drew on Android.
 */
@Composable
public fun FoldersScreen(model: FoldersModel) {
    val copy by model.copy.collectAsState()
    val showNoData by model.showNoData.collectAsState()
    val folders by model.folders.collectAsState()
    BitwardenTheme {
        Box(Modifier.fillMaxSize()) {
            if (showNoData) {
                Column(
                    Modifier.fillMaxSize().padding(horizontal = 20.dp),
                    verticalArrangement = Arrangement.Center,
                    horizontalAlignment = Alignment.CenterHorizontally,
                ) {
                    Icon(FolderIcon, contentDescription = null, tint = MaterialTheme.colorScheme.outline)
                    Text(
                        copy.noFolders,
                        Modifier.padding(top = 12.dp),
                        style = MaterialTheme.typography.bodyLarge,
                        textAlign = TextAlign.Center,
                    )
                }
            } else {
                LazyColumn(Modifier.fillMaxSize(), contentPadding = PaddingValues(bottom = 88.dp)) {
                    items(folders, key = { it.key }) { folder ->
                        ListItem(
                            headlineContent = {
                                Text(folder.name, maxLines = 1, overflow = TextOverflow.Ellipsis)
                            },
                            leadingContent = { Icon(FolderIcon, contentDescription = null) },
                            modifier = Modifier.clickable { model.open(folder.key) },
                        )
                        HorizontalDivider()
                    }
                }
            }
            FloatingActionButton(
                onClick = { model.add() },
                modifier = Modifier.align(Alignment.BottomEnd).padding(16.dp)
                    .semantics { contentDescription = copy.addFolder },
            ) {
                Icon(AddIcon, contentDescription = null)
            }
        }
    }
}
