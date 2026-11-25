package com.oahu.appapp

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.text.input.TextFieldLineLimits
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.unit.dp
import com.adamessenmacher.oahu.compose.rememberTextFieldBinding
import kotlinx.coroutines.delay

/**
 * Adding or editing a folder, hosted in the MAUI FolderAddEditPage. Save and Delete stay MAUI toolbar items; the
 * keyboard's Go key saves, as the MAUI Entry's did.
 */
@Composable
public fun FolderAddEditScreen(model: FolderAddEditModel) {
    val copy by model.copy.collectAsState()
    val focusName by model.focusName.collectAsState()
    val name = model.name.rememberTextFieldBinding()
    val focus = remember { FocusRequester() }
    LaunchedEffect(focusName) {
        if (focusName) {
            // Wait for the modal page's animation, as the MAUI page's RequestFocus did.
            delay(500)
            focus.requestFocus()
        }
    }
    BitwardenTheme {
        Column(Modifier.fillMaxSize().padding(16.dp)) {
            OutlinedTextField(
                state = name.state,
                label = { Text(copy.nameLabel) },
                lineLimits = TextFieldLineLimits.SingleLine,
                keyboardOptions = KeyboardOptions(imeAction = ImeAction.Go),
                onKeyboardAction = {
                    name.commit()
                    model.submit()
                },
                modifier = Modifier.fillMaxWidth().focusRequester(focus),
            )
        }
    }
}
