package com.oahu.appapp

import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.background
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.IconButton
import androidx.compose.material3.Text
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/**
 * The vault list (GroupingsPage): "My Vault" and every drill-down. The MAUI page keeps its title, toolbar items
 * (account switcher, search, Sync, Lock, Exit), the floating add button, the account overlay and every dialog; this
 * screen draws the filter bar, the empty state and the sectioned list with pull to refresh.
 */
@OptIn(ExperimentalMaterial3Api::class, ExperimentalFoundationApi::class)
@Composable
fun GroupingsScreen(model: GroupingsModel) {
    val palette by model.palette.collectAsState()
    val copy by model.copy.collectAsState()
    val rows by model.rows.collectAsState()
    val empty by model.empty.collectAsState()
    val filter by model.vaultFilter.collectAsState()
    val isLoading by model.isLoading.collectAsState()
    val isRefreshing by model.isRefreshing.collectAsState()

    PaletteTheme(palette) {
        Column(Modifier.fillMaxSize().background(palette.backgroundColor)) {
            filter?.let { FilterBar(it, copy, palette) { model.chooseVaultFilter() } }
            val currentEmpty = empty
            if (currentEmpty != null) {
                EmptyState(currentEmpty, copy, palette) { model.add() }
            } else {
                PullToRefreshBox(
                    isRefreshing = isRefreshing,
                    onRefresh = { model.refresh() },
                    modifier = Modifier.fillMaxSize(),
                ) {
                    val listState = rememberLazyListState()
                    // Room at the end for the MAUI floating add button that sits over this screen.
                    LazyColumn(state = listState, contentPadding = PaddingValues(bottom = 88.dp),
                        modifier = Modifier.fillMaxSize()) {
                        rows.forEach { row ->
                            when (row) {
                                is GroupingsRow.SectionHeader -> stickyHeader(key = row.key, contentType = "header") {
                                    SectionHeader(row, palette)
                                }
                                is GroupingsRow.Grouping -> item(key = row.key, contentType = "group") {
                                    GroupRow(row, palette) { model.select(row.key) }
                                }
                                is GroupingsRow.CipherItem -> item(key = row.key, contentType = "cipher") {
                                    CipherRow(row, copy, palette, model)
                                }
                                is GroupingsRow.TotpItem -> item(key = row.key, contentType = "totp") {
                                    TotpRow(row, copy, palette, model)
                                }
                            }
                        }
                    }
                    if (isLoading && rows.isEmpty()) {
                        CircularProgressIndicator(Modifier.align(Alignment.Center), color = palette.primaryColor)
                    }
                }
            }
        }
    }
}

@Composable
private fun FilterBar(text: String, copy: GroupingsCopy, palette: AppPalette, onMenu: () -> Unit) {
    Row(Modifier.fillMaxWidth().padding(start = 10.dp, top = 5.dp), verticalAlignment = Alignment.CenterVertically) {
        Text(text, color = palette.mutedColor, maxLines = 1, overflow = TextOverflow.Ellipsis, modifier = Modifier.weight(1f))
        IconButton(onClick = onMenu, modifier = Modifier.semantics { contentDescription = copy.filter }) {
            Text("⋮", fontSize = 22.sp, color = palette.mutedColor)
        }
    }
}

@Composable
private fun EmptyState(empty: GroupingsEmpty, copy: GroupingsCopy, palette: AppPalette, onAdd: () -> Unit) {
    Column(
        Modifier.fillMaxSize().padding(horizontal = 20.dp),
        verticalArrangement = Arrangement.spacedBy(20.dp, Alignment.CenterVertically),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Text(empty.text, color = palette.textColor, textAlign = TextAlign.Center)
        if (empty.showAddButton) {
            Button(onClick = onAdd, colors = ButtonDefaults.buttonColors(containerColor = palette.primaryColor)) {
                Text(copy.addAnItem)
            }
        }
    }
}

@Composable
private fun SectionHeader(row: GroupingsRow.SectionHeader, palette: AppPalette) {
    Column(Modifier.fillMaxWidth().background(palette.headerBackgroundColor)) {
        Row(Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 8.dp)) {
            Text(row.title, color = palette.headerTextColor, fontWeight = FontWeight.SemiBold, fontSize = 14.sp,
                modifier = Modifier.weight(1f))
            Text(row.count, color = palette.headerTextColor, fontSize = 14.sp)
        }
        HorizontalDivider(color = palette.separatorColor)
    }
}

@Composable
private fun GroupRow(row: GroupingsRow.Grouping, palette: AppPalette, onClick: () -> Unit) {
    Row(
        Modifier.fillMaxWidth().combinedClickable(onClick = onClick).padding(horizontal = 12.dp, vertical = 14.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(Modifier.width(40.dp), contentAlignment = Alignment.Center) { Glyph(row.glyph, palette.mutedColor) }
        Spacer(Modifier.width(4.dp))
        Text(row.name, color = palette.textColor, maxLines = 1, overflow = TextOverflow.Ellipsis, modifier = Modifier.weight(1f))
        Text(row.count, color = palette.mutedColor)
    }
}

@OptIn(ExperimentalFoundationApi::class)
@Composable
private fun CipherRow(row: GroupingsRow.CipherItem, copy: GroupingsCopy, palette: AppPalette, model: GroupingsModel) {
    var menu by remember { mutableStateOf(false) }
    Box {
        Row(
            Modifier.fillMaxWidth()
                .combinedClickable(onClick = { model.select(row.key) }, onLongClick = { menu = true })
                .padding(start = 4.dp, top = 6.dp, bottom = 6.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            CipherIcon(row.iconUrl, row.glyph, palette)
            Column(Modifier.weight(1f).padding(start = 4.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(row.name, color = palette.textColor, maxLines = 1, overflow = TextOverflow.Ellipsis,
                        modifier = Modifier.weight(1f, fill = false))
                    if (row.shared) {
                        Glyph(BwIcons.COLLECTION, palette.mutedColor, 14.sp,
                            Modifier.padding(start = 5.dp).semantics { contentDescription = copy.shared })
                    }
                    if (row.hasAttachments) {
                        Glyph(BwIcons.PAPERCLIP, palette.mutedColor, 14.sp,
                            Modifier.padding(start = 5.dp).semantics { contentDescription = copy.attachments })
                    }
                }
                row.subtitle?.let {
                    Text(it, color = palette.mutedColor, fontSize = 14.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                }
            }
            IconButton(onClick = { model.showOptions(row.key) },
                modifier = Modifier.semantics { contentDescription = copy.options }) {
                Text("⋮", fontSize = 22.sp, color = palette.mutedColor)
            }
        }
        // Long press: the native context menu (View, Edit, Options); MAUI had only the "more" button.
        DropdownMenu(expanded = menu, onDismissRequest = { menu = false }) {
            DropdownMenuItem(text = { Text(copy.view) }, onClick = { menu = false; model.select(row.key) })
            if (row.canEdit) {
                DropdownMenuItem(text = { Text(copy.edit) }, onClick = { menu = false; model.edit(row.key) })
            }
            DropdownMenuItem(text = { Text(copy.options) }, onClick = { menu = false; model.showOptions(row.key) })
        }
    }
    HorizontalDivider(Modifier.padding(start = 48.dp), color = palette.separatorColor)
}

@OptIn(ExperimentalFoundationApi::class)
@Composable
private fun TotpRow(row: GroupingsRow.TotpItem, copy: GroupingsCopy, palette: AppPalette, model: GroupingsModel) {
    val low = (row.seconds.toIntOrNull() ?: 30) < 7
    val color = if (low) palette.dangerColor else palette.primaryColor
    Row(
        Modifier.fillMaxWidth().combinedClickable(onClick = { model.select(row.key) })
            .padding(start = 4.dp, end = 4.dp, top = 8.dp, bottom = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        CipherIcon(row.iconUrl, row.glyph, palette)
        Column(Modifier.weight(1f).padding(start = 4.dp)) {
            Text(row.name, color = palette.textColor, maxLines = 1, overflow = TextOverflow.Ellipsis)
            row.subtitle?.let {
                Text(it, color = palette.mutedColor, fontSize = 14.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
            }
        }
        TotpRing(row.seconds, row.progress, color, palette.separatorColor)
        Text(row.code ?: "", color = color, fontSize = 20.sp, fontFamily = FontFamily.Monospace,
            modifier = Modifier.padding(horizontal = 8.dp))
        IconButton(onClick = { model.copyTotp(row.key) }, modifier = Modifier.size(40.dp)
            .semantics { contentDescription = copy.copyTotp }) {
            Glyph(BwIcons.CLONE, palette.primaryColor)
        }
    }
    HorizontalDivider(Modifier.padding(start = 48.dp), color = palette.separatorColor)
}
