package com.oahu.appapp

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.text.input.TextFieldLineLimits
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import com.adamessenmacher.oahu.compose.rememberTextFieldState

/**
 * Step 7, the D25 trial: the login's URIs as their own contract (an editable keyed list), mounted by the add/edit view
 * model and drawn by the add/edit screen with OahuMount. Rows use the documented row text helper.
 */
@Composable
fun UriSectionScreen(model: UriSectionModel) {
    val copy by model.copy.collectAsState()
    val locked by model.locked.collectAsState()
    val uris by model.uris.collectAsState()
    var pendingFocus by remember { mutableStateOf<String?>(null) }
    LaunchedEffect(model) {
        model.focusRow.collect { pendingFocus = it }
    }
    Column {
        uris.forEach { row ->
            key(row.key) {
                val requester = remember { FocusRequester() }
                Row(verticalAlignment = Alignment.CenterVertically) {
                    OutlinedTextField(
                        state = model.uris.rememberTextFieldState(row.key, keyOf = { it.key }, text = { it.uri },
                            set = { model.setUriText(RowText(row.key, it)) }),
                        label = { Text(copy.uri) },
                        enabled = !locked,
                        lineLimits = TextFieldLineLimits.SingleLine,
                        keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.None,
                            autoCorrectEnabled = false, keyboardType = KeyboardType.Uri, imeAction = ImeAction.Next),
                        modifier = Modifier.weight(1f).padding(vertical = 2.dp).focusRequester(requester)
                            .testTag("LoginUriEntry"),
                    )
                    if (!locked) {
                        IconButton(onClick = { model.uriOptions(row.key) },
                            modifier = Modifier.semantics { contentDescription = copy.options }) {
                            Glyph(BwIcons.COG, MaterialTheme.colorScheme.primary)
                        }
                    }
                }
                LaunchedEffect(pendingFocus) {
                    if (pendingFocus == row.key) {
                        requester.requestFocus()
                        pendingFocus = null
                    }
                }
            }
        }
        if (!locked) {
            TextButton(onClick = { model.addUri() }, modifier = Modifier.testTag("LoginAddNewUriButton")) {
                Text(copy.newUri)
            }
        }
    }
}
