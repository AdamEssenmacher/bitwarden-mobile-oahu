package com.oahu.appapp

import androidx.compose.foundation.layout.Column
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
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import com.adamessenmacher.oahu.compose.rememberTextFieldState
import com.adamessenmacher.oahu.runtime.OahuEditable

/**
 * Step 7, the D25 trial: the add/edit form's identity section as its own contract, mounted by the add/edit view model
 * and drawn by the add/edit screen with OahuMount inside its scrolling Column. Each line is its own String editable, so
 * it uses rememberTextFieldState (the flat form's record editable needed value-based fields, G31) and a keystroke sends
 * one string. Material colors come from the parent's PaletteTheme.
 */
@Composable
fun IdentitySectionScreen(model: IdentitySectionModel) {
    val copy by model.copy.collectAsState()
    // One requester per line (step 8: C# may forward a request for any line, such as Email after a failed Save).
    val requesters = remember { List(17) { FocusRequester() } }
    LaunchedEffect(model) {
        model.focus.collect { line -> requesters.getOrNull(line)?.requestFocus() }
    }
    val lines: List<Triple<String, OahuEditable<String>, String>> = listOf(
        Triple(copy.firstName, model.firstName, "IdentityFirstNameEntry"),
        Triple(copy.middleName, model.middleName, "IdentityMiddleNameEntry"),
        Triple(copy.lastName, model.lastName, "IdentityLastNameEntry"),
        Triple(copy.username, model.username, "IdentityUsernameEntry"),
        Triple(copy.company, model.company, "IdentityCompanyEntry"),
        Triple(copy.ssn, model.ssn, "IdentitySsnEntry"),
        Triple(copy.passportNumber, model.passportNumber, "IdentityPassportNumberEntry"),
        Triple(copy.licenseNumber, model.licenseNumber, "IdentityLicenseNumberEntry"),
        Triple(copy.email, model.email, "IdentityEmailEntry"),
        Triple(copy.phone, model.phone, "IdentityPhoneEntry"),
        Triple(copy.address1, model.address1, "IdentityAddressOneEntry"),
        Triple(copy.address2, model.address2, "IdentityAddressTwoEntry"),
        Triple(copy.address3, model.address3, "IdentityAddressThreeEntry"),
        Triple(copy.city, model.city, "IdentityCityEntry"),
        Triple(copy.state, model.state, "IdentityStateEntry"),
        Triple(copy.postalCode, model.postalCode, "IdentityPostalCodeEntry"),
        Triple(copy.country, model.country, "IdentityCountryEntry"),
    )
    Column {
        IndexPicker(copy.title, copy.titleOptions, model.title, "IdentityTitlePicker")
        lines.forEachIndexed { i, (label, editable, tag) ->
            OutlinedTextField(
                state = editable.rememberTextFieldState(),
                label = { Text(label) },
                lineLimits = TextFieldLineLimits.SingleLine,
                keyboardOptions = KeyboardOptions(
                    capitalization = when (tag) {
                        "IdentityUsernameEntry", "IdentitySsnEntry", "IdentityEmailEntry" -> KeyboardCapitalization.None
                        else -> KeyboardCapitalization.Sentences
                    },
                    keyboardType = when (tag) {
                        "IdentityEmailEntry" -> KeyboardType.Email
                        "IdentityPhoneEntry" -> KeyboardType.Phone
                        else -> KeyboardType.Text
                    },
                    imeAction = if (i < lines.size - 1) ImeAction.Next else ImeAction.Done),
                modifier = Modifier.fillMaxWidth().padding(vertical = 2.dp).testTag(tag)
                    .focusRequester(requesters[i])
                    .formField("identity.$tag"),   // step 8: also reachable through the parent's registry
            )
        }
    }
}
