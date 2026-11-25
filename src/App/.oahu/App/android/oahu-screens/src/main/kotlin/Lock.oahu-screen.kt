package com.oahu.appapp

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.text.input.TextObfuscationMode
import androidx.compose.foundation.text.input.placeCursorAtEnd
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.IconButton
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedSecureTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.Alignment
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.text.TextRange
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.adamessenmacher.oahu.compose.rememberTextFieldBinding

/**
 * The lock page's content, hosted in the MAUI LockPage: the master password or PIN with show/hide, the locked and
 * logged-in-as text, the biometric button and Unlock. The page keeps its title, the account avatar and switcher, the
 * Log out item, the loading overlay and every dialog.
 */
@Composable
fun LockScreen(model: LockModel) {
    val palette by model.palette.collectAsState()
    val copy by model.copy.collectAsState()
    val layout by model.layout.collectAsState()
    // One binding per field; the field shown follows PinEnabled.
    val masterPassword = model.masterPassword.rememberTextFieldBinding()
    val pin = model.pin.rememberTextFieldBinding()
    val secret = if (layout.pinEnabled) pin else masterPassword
    val focus = remember { FocusRequester() }
    LaunchedEffect(model) {
        // One-shot focus requests from C# (on appearing, after show/hide, after a biometric fallback), with the cursor
        // where the view model asks (MAUI: SecretEntry.Focus() and CursorPosition).
        model.focusSecret.collect { request ->
            focus.requestFocus()
            val field = (if (model.layout.value.pinEnabled) pin else masterPassword).state
            request.cursor?.let { c ->
                field.edit { selection = TextRange(c.coerceIn(0, length)) }
            }
        }
    }
    PaletteTheme(palette) {
        Column(
            Modifier.fillMaxSize().background(palette.backgroundColor).imePadding()
                .verticalScroll(rememberScrollState()).padding(16.dp),
        ) {
            if (layout.showSecretEntry) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    OutlinedSecureTextField(
                        state = secret.state,
                        label = { Text(if (layout.pinEnabled) copy.pin else copy.masterPassword) },
                        textObfuscationMode = if (layout.showSecret) TextObfuscationMode.Visible else TextObfuscationMode.RevealLastTyped,
                        textStyle = androidx.compose.ui.text.TextStyle(fontFamily = FontFamily.Monospace),
                        keyboardOptions = KeyboardOptions(
                            keyboardType = if (layout.pinEnabled) KeyboardType.NumberPassword else KeyboardType.Password,
                            imeAction = ImeAction.Go,
                            autoCorrectEnabled = false,
                        ),
                        onKeyboardAction = {
                            secret.commit()
                            model.submit()
                        },
                        modifier = Modifier.weight(1f).focusRequester(focus),
                    )
                    IconButton(
                        onClick = { model.toggleShowSecret() },
                        modifier = Modifier.semantics {
                            contentDescription = copy.toggleVisibility
                            stateDescription = if (layout.showSecret) copy.secretShownHint else copy.secretHiddenHint
                        },
                    ) {
                        Glyph(if (layout.showSecret) BwIcons.EYE_SLASH else BwIcons.EYE, palette.primaryColor)
                    }
                }
            }
            Text(layout.lockedVerifyText, color = palette.mutedColor, fontSize = 13.sp, modifier = Modifier.padding(top = 10.dp))
            Text(layout.loggedInAsText, color = palette.mutedColor, fontSize = 13.sp, modifier = Modifier.padding(top = 10.dp))
            Spacer(Modifier.height(20.dp))
            if (layout.biometricInvalidated) {
                Text(copy.biometricInvalidated, color = palette.dangerColor, fontWeight = FontWeight.Bold, fontSize = 13.sp)
            }
            layout.biometricButton?.let { text ->
                OutlinedButton(onClick = { model.useBiometrics() }, modifier = Modifier.fillMaxWidth()) { Text(text) }
            }
            if (layout.showSecretEntry) {
                Button(
                    onClick = {
                        secret.commit()
                        model.submit()
                    },
                    colors = ButtonDefaults.buttonColors(containerColor = palette.primaryColor),
                    modifier = Modifier.fillMaxWidth(),
                ) { Text(copy.unlock) }
            }
        }
    }
}
