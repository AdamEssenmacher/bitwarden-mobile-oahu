package com.oahu.appapp

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.text.input.TextFieldLineLimits
import androidx.compose.foundation.text.input.TextFieldState
import androidx.compose.foundation.text.input.TextObfuscationMode
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuAnchorType
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.IconButton
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedSecureTextField
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Switch
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
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.adamessenmacher.oahu.compose.OahuMount
import com.adamessenmacher.oahu.compose.collectAsMutableState
import com.adamessenmacher.oahu.compose.rememberTextFieldState

/**
 * The item add/edit/clone form, hosted in the MAUI CipherAddEditPage: type, name, the login, card or identity section,
 * URIs, folder, favorite, re-prompt, notes, custom fields, ownership and collections. The page keeps its title, Save
 * and the secondary toolbar items, every dialog and action sheet, and the pages it pushes.
 */
@Composable
fun CipherAddEditScreen(model: CipherAddEditModel) {
    val palette by model.palette.collectAsState()
    val copy by model.copy.collectAsState()
    val layout by model.layout.collectAsState()
    val sections by model.sections.collectAsState()
    val focus = remember { FormFocusRequesters() }
    // A focus request for a row waits until the row is composed (a new URI arrives in the same batch as the request).
    var pendingRowFocus by remember { mutableStateOf<String?>(null) }
    LaunchedEffect(model) {
        model.focus.collect { request ->
            when (request.field) {
                FormField.Name -> focus.name.requestFocus()
                FormField.Username -> focus.username.requestFocus()
                FormField.Password -> focus.password.requestFocus()
                FormField.Totp -> focus.totp.requestFocus()
                FormField.CardholderName -> focus.cardholder.requestFocus()
                FormField.IdentityFirstName -> focus.identityFirst.requestFocus()
                FormField.Notes -> focus.notes.requestFocus()
                FormField.Uri, FormField.CustomField -> pendingRowFocus = request.rowKey
            }
        }
    }
    PaletteTheme(palette) {
        Column(
            Modifier.fillMaxSize().background(palette.backgroundColor).imePadding()
                .verticalScroll(rememberScrollState()).padding(horizontal = 16.dp, vertical = 8.dp),
        ) {
            if (layout.ownershipPolicyInEffect) {
                Text(copy.personalOwnershipPolicyInEffect, color = palette.mutedColor, fontWeight = FontWeight.Bold,
                    fontSize = 13.sp, modifier = Modifier.padding(vertical = 8.dp))
            }
            Header(copy.itemInformation, palette)
            if (layout.showTypePicker) {
                IndexPicker(copy.type, copy.typeOptions, model.type, "ItemTypePicker")
            }
            TextLine(copy.name, model.name.rememberTextFieldState(), focus.name, "ItemNameEntry")
            when (layout.kind) {
                EditItemKind.Login -> LoginSection(model, copy, layout, palette, focus)
                EditItemKind.Card -> CardSection(model, copy, layout, palette, focus)
                EditItemKind.Identity ->
                    // Step 7, D25 trial: the identity section as a mounted child contract when its id is set.
                    if (sections.identity != 0L) OahuMount(sections.identity) else IdentitySection(model, copy, focus)
                EditItemKind.SecureNote -> {}
            }
            if (layout.kind == EditItemKind.Login) {
                if (sections.uris != 0L) {
                    Header(copy.uris, palette)
                    OahuMount(sections.uris)
                } else {
                    UrisSection(model, copy, layout, palette, pendingRowFocus) { pendingRowFocus = null }
                }
            }
            Header(copy.miscellaneous, palette)
            val choices by model.choices.collectAsState()
            IndexPicker(copy.folder, choices.folders, model.folder, "FolderPicker")
            var favorite by model.favorite.collectAsMutableState()
            SwitchLine(copy.favorite, favorite, "ItemFavoriteToggle") { favorite = it }
            if (layout.showPasswordPrompt) {
                var reprompt by model.reprompt.collectAsMutableState()
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(copy.passwordPrompt)
                    IconButton(onClick = { model.passwordPromptHelp() },
                        modifier = Modifier.semantics { contentDescription = copy.passwordPromptHelp }) {
                        Glyph(BwIcons.QUESTION_CIRCLE, palette.mutedColor, 18.sp)
                    }
                    Row(Modifier.weight(1f), horizontalArrangement = Arrangement.End) {
                        Switch(checked = reprompt, onCheckedChange = { reprompt = it },
                            modifier = Modifier.testTag("MasterPasswordRepromptToggle"))
                    }
                }
            }
            Header(copy.notes, palette)
            // MAUI's Editor with AutoSize: grows with its text.
            OutlinedTextField(
                state = model.notes.rememberTextFieldState(),
                lineLimits = TextFieldLineLimits.MultiLine(minHeightInLines = 3),
                modifier = Modifier.fillMaxWidth().focusRequester(focus.notes).testTag("ItemNotesEntry"),
            )
            FieldsSection(model, copy, palette, pendingRowFocus) { pendingRowFocus = null }
            if (layout.showOwnership) {
                Header(copy.ownership, palette)
                IndexPicker(copy.whoOwnsThisItem, choices.owners, model.owner, "ItemOwnershipPicker")
            }
            if (layout.showCollections) {
                Header(copy.collections, palette)
                if (!layout.hasCollections) {
                    Text(copy.noCollectionsToList, modifier = Modifier.padding(vertical = 8.dp))
                } else {
                    val collections by model.collections.collectAsState()
                    collections.forEach { row ->
                        key(row.key) {
                            SwitchLine(row.name, row.checked, "CollectionItemSwitch") { model.toggleCollection(row.key) }
                        }
                    }
                }
            }
        }
    }
}

private class FormFocusRequesters {
    val name = FocusRequester()
    val username = FocusRequester()
    val password = FocusRequester()
    val totp = FocusRequester()
    val cardholder = FocusRequester()
    val identityFirst = FocusRequester()
    val notes = FocusRequester()
}

@Composable
private fun LoginSection(
    model: CipherAddEditModel, copy: CipherAddEditCopy, layout: CipherAddEditLayout, palette: AppPalette,
    focus: FormFocusRequesters,
) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        TextLine(copy.username, model.username.rememberTextFieldState(), focus.username, "LoginUsernameEntry",
            keyboard = KeyboardOptions(autoCorrectEnabled = false, keyboardType = KeyboardType.Email,
                imeAction = ImeAction.Next), modifier = Modifier.weight(1f))
        IconGlyphButton(BwIcons.GENERATE, copy.generateUsername, palette) { model.generateUsername() }
    }
    Row(verticalAlignment = Alignment.CenterVertically) {
        SecretLine(copy.password, model.password.rememberTextFieldState(), layout.showPassword && layout.viewPassword,
            enabled = layout.viewPassword, focus = focus.password, tag = "LoginPasswordEntry", modifier = Modifier.weight(1f))
        if (layout.viewPassword) {
            IconGlyphButton(BwIcons.CHECK_CIRCLE, copy.checkPassword, palette) { model.checkPassword() }
            IconGlyphButton(if (layout.showPassword) BwIcons.EYE_SLASH else BwIcons.EYE, copy.toggleVisibility, palette) {
                model.togglePassword()
            }
            IconGlyphButton(BwIcons.GENERATE, copy.generatePassword, palette) { model.generatePassword() }
        }
    }
    layout.passkeyCreated?.let { created ->
        Text(copy.passkey, color = palette.mutedColor, fontSize = 12.sp, modifier = Modifier.padding(top = 8.dp))
        Text(created, color = palette.mutedColor)
    }
    if (!layout.hasTotp) {
        Text(copy.authenticatorKey, color = palette.mutedColor, fontSize = 12.sp, modifier = Modifier.padding(top = 8.dp))
        OutlinedButton(onClick = { model.scanTotp() }, modifier = Modifier.fillMaxWidth().testTag("SetupTotpButton")) {
            Glyph(BwIcons.CAMERA, palette.primaryColor, 18.sp)
            Text("  " + copy.setupTotp)
        }
    } else {
        Row(verticalAlignment = Alignment.CenterVertically) {
            SecretLine(copy.authenticatorKey, model.totp.rememberTextFieldState(), shown = layout.viewPassword,
                enabled = layout.viewPassword, focus = focus.totp, tag = "LoginTotpEntry", modifier = Modifier.weight(1f))
            if (layout.allowTotpCopy) {
                IconGlyphButton(BwIcons.CLONE, copy.copyTotp, palette) { model.copyTotp() }
            }
            IconGlyphButton(BwIcons.CAMERA, copy.scanQr, palette) { model.scanTotp() }
        }
    }
}

@Composable
private fun CardSection(
    model: CipherAddEditModel, copy: CipherAddEditCopy, layout: CipherAddEditLayout, palette: AppPalette,
    focus: FormFocusRequesters,
) {
    TextLine(copy.cardholderName, model.cardholderName.rememberTextFieldState(), focus.cardholder, "CardholderNameEntry",
        keyboard = KeyboardOptions(capitalization = KeyboardCapitalization.Words, imeAction = ImeAction.Next))
    Row(verticalAlignment = Alignment.CenterVertically) {
        SecretLine(copy.number, model.cardNumber.rememberTextFieldState(), layout.showCardNumber, tag = "CardNumberEntry",
            keyboardType = KeyboardType.NumberPassword, modifier = Modifier.weight(1f))
        IconGlyphButton(if (layout.showCardNumber) BwIcons.EYE_SLASH else BwIcons.EYE, copy.toggleVisibility, palette) {
            model.toggleCardNumber()
        }
    }
    IndexPicker(copy.brand, copy.cardBrandOptions, model.cardBrand, "CardBrandPicker")
    IndexPicker(copy.expirationMonth, copy.cardExpMonthOptions, model.cardExpMonth, "CardExpirationMonthPicker")
    TextLine(copy.expirationYear, model.cardExpYear.rememberTextFieldState(), null, "CardExpirationYearEntry",
        keyboard = KeyboardOptions(keyboardType = KeyboardType.Number, imeAction = ImeAction.Next))
    Row(verticalAlignment = Alignment.CenterVertically) {
        SecretLine(copy.securityCode, model.cardCode.rememberTextFieldState(), layout.showCardCode,
            tag = "CardSecurityCodeEntry", keyboardType = KeyboardType.NumberPassword, modifier = Modifier.weight(1f))
        IconGlyphButton(if (layout.showCardCode) BwIcons.EYE_SLASH else BwIcons.EYE, copy.toggleVisibility, palette) {
            model.toggleCardCode()
        }
    }
}

private class IdentityLine(
    val label: String,
    val get: (IdentityFields) -> String,
    val set: IdentityFields.(String) -> IdentityFields,
    val tag: String,
    val keyboardType: KeyboardType = KeyboardType.Text,
    val capitalization: KeyboardCapitalization = KeyboardCapitalization.Sentences,
)

@Composable
private fun IdentitySection(model: CipherAddEditModel, copy: CipherAddEditCopy, focus: FormFocusRequesters) {
    IndexPicker(copy.title, copy.identityTitleOptions, model.identityTitle, "IdentityTitlePicker")
    val lines = remember(copy) {
        listOf(
            IdentityLine(copy.firstName, { it.firstName }, { copy(firstName = it) }, "IdentityFirstNameEntry", capitalization = KeyboardCapitalization.Words),
            IdentityLine(copy.middleName, { it.middleName }, { copy(middleName = it) }, "IdentityMiddleNameEntry", capitalization = KeyboardCapitalization.Words),
            IdentityLine(copy.lastName, { it.lastName }, { copy(lastName = it) }, "IdentityLastNameEntry", capitalization = KeyboardCapitalization.Words),
            IdentityLine(copy.username, { it.username }, { copy(username = it) }, "IdentityUsernameEntry", capitalization = KeyboardCapitalization.None),
            IdentityLine(copy.company, { it.company }, { copy(company = it) }, "IdentityCompanyEntry"),
            IdentityLine(copy.ssn, { it.ssn }, { copy(ssn = it) }, "IdentitySsnEntry", capitalization = KeyboardCapitalization.None),
            IdentityLine(copy.passportNumber, { it.passportNumber }, { copy(passportNumber = it) }, "IdentityPassportNumberEntry", capitalization = KeyboardCapitalization.Characters),
            IdentityLine(copy.licenseNumber, { it.licenseNumber }, { copy(licenseNumber = it) }, "IdentityLicenseNumberEntry", capitalization = KeyboardCapitalization.Characters),
            IdentityLine(copy.email, { it.email }, { copy(email = it) }, "IdentityEmailEntry", KeyboardType.Email, KeyboardCapitalization.None),
            IdentityLine(copy.phone, { it.phone }, { copy(phone = it) }, "IdentityPhoneEntry", KeyboardType.Phone),
            IdentityLine(copy.address1, { it.address1 }, { copy(address1 = it) }, "IdentityAddressOneEntry"),
            IdentityLine(copy.address2, { it.address2 }, { copy(address2 = it) }, "IdentityAddressTwoEntry"),
            IdentityLine(copy.address3, { it.address3 }, { copy(address3 = it) }, "IdentityAddressThreeEntry"),
            IdentityLine(copy.city, { it.city }, { copy(city = it) }, "IdentityCityEntry"),
            IdentityLine(copy.state, { it.state }, { copy(state = it) }, "IdentityStateEntry"),
            IdentityLine(copy.postalCode, { it.postalCode }, { copy(postalCode = it) }, "IdentityPostalCodeEntry", capitalization = KeyboardCapitalization.Characters),
            IdentityLine(copy.country, { it.country }, { copy(country = it) }, "IdentityCountryEntry"),
        )
    }
    // oahu-compose has text-field helpers for a String editable and for a row of a list, not for a field of a record
    // editable, so these use the value-based field with collectAsMutableState (docs/oahu/gaps.md, step 5).
    var identity by model.identity.collectAsMutableState()
    lines.forEachIndexed { i, line ->
        OutlinedTextField(
            value = line.get(identity),
            onValueChange = { identity = line.set(identity, it) },
            label = { Text(line.label) },
            singleLine = true,
            keyboardOptions = KeyboardOptions(capitalization = line.capitalization, keyboardType = line.keyboardType,
                imeAction = if (i < lines.size - 1) ImeAction.Next else ImeAction.Done),
            modifier = Modifier.fillMaxWidth().padding(vertical = 2.dp).testTag(line.tag)
                .let { if (i == 0) it.focusRequester(focus.identityFirst) else it },
        )
    }
}

@Composable
private fun UrisSection(
    model: CipherAddEditModel, copy: CipherAddEditCopy, layout: CipherAddEditLayout, palette: AppPalette,
    pendingFocus: String?, focused: () -> Unit,
) {
    Header(copy.uris, palette)
    val uris by model.uris.collectAsState()
    uris.forEach { row ->
        key(row.key) {
            val requester = remember { FocusRequester() }
            Row(verticalAlignment = Alignment.CenterVertically) {
                OutlinedTextField(
                    state = model.uris.rememberTextFieldState(row.key, keyOf = { it.key }, text = { it.uri },
                        set = { model.setUriText(RowText(row.key, it)) }),
                    label = { Text(copy.uri) },
                    enabled = !layout.urisLocked,
                    lineLimits = TextFieldLineLimits.SingleLine,
                    keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.None,
                        autoCorrectEnabled = false, keyboardType = KeyboardType.Uri, imeAction = ImeAction.Next),
                    modifier = Modifier.weight(1f).padding(vertical = 2.dp).focusRequester(requester).testTag("LoginUriEntry"),
                )
                if (!layout.urisLocked) {
                    IconGlyphButton(BwIcons.COG, copy.options, palette) { model.uriOptions(row.key) }
                }
            }
            LaunchedEffect(pendingFocus) {
                if (pendingFocus == row.key) {
                    requester.requestFocus()
                    focused()
                }
            }
        }
    }
    if (!layout.urisLocked) {
        TextButton(onClick = { model.addUri() }, modifier = Modifier.testTag("LoginAddNewUriButton")) { Text(copy.newUri) }
    }
}

@Composable
private fun FieldsSection(
    model: CipherAddEditModel, copy: CipherAddEditCopy, palette: AppPalette, pendingFocus: String?, focused: () -> Unit,
) {
    Header(copy.customFields, palette)
    val fields by model.fields.collectAsState()
    fields.forEach { row ->
        key(row.key) {
            val requester = remember { FocusRequester() }
            Row(verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    when (row) {
                        is EditFieldRow.Text -> OutlinedTextField(
                            state = fieldText(model, row.key),
                            label = { Text(row.name) },
                            lineLimits = TextFieldLineLimits.SingleLine,
                            modifier = Modifier.fillMaxWidth().padding(vertical = 2.dp).focusRequester(requester)
                                .testTag("TextCustomFieldValueEntry"),
                        )
                        is EditFieldRow.Hidden -> Row(verticalAlignment = Alignment.CenterVertically) {
                            SecretLine(row.name, fieldText(model, row.key), row.shown, enabled = row.canReveal,
                                focus = requester, tag = "HiddenCustomFieldValueEntry", modifier = Modifier.weight(1f))
                            if (row.canReveal) {
                                IconGlyphButton(if (row.shown) BwIcons.EYE_SLASH else BwIcons.EYE, copy.toggleVisibility,
                                    palette) { model.toggleFieldHidden(row.key) }
                            }
                        }
                        is EditFieldRow.Toggle -> SwitchLine(row.name, row.value, "BooleanCustomFieldSwitch") {
                            model.setFieldBool(RowBool(row.key, it))
                        }
                        is EditFieldRow.Linked -> RowIndexPicker(row.name, row.options, row.selected) {
                            model.setFieldLinked(RowIndex(row.key, it))
                        }
                    }
                }
                IconGlyphButton(BwIcons.COG, copy.options, palette) { model.fieldOptions(row.key) }
            }
            LaunchedEffect(pendingFocus) {
                if (pendingFocus == row.key) {
                    requester.requestFocus()
                    focused()
                }
            }
        }
    }
    TextButton(onClick = { model.addField() }, modifier = Modifier.testTag("NewCustomFieldButton")) { Text(copy.newCustomField) }
}

@Composable
private fun fieldText(model: CipherAddEditModel, key: String): TextFieldState =
    model.fields.rememberTextFieldState(key, keyOf = { it.key }, text = {
        when (it) {
            is EditFieldRow.Text -> it.value
            is EditFieldRow.Hidden -> it.value
            else -> ""
        }
    }, set = { model.setFieldText(RowText(key, it)) })

@Composable
private fun Header(text: String, palette: AppPalette) {
    Text(text.uppercase(), color = palette.mutedColor, fontSize = 13.sp, fontWeight = FontWeight.Bold,
        modifier = Modifier.padding(top = 20.dp, bottom = 4.dp))
}

@Composable
private fun TextLine(
    label: String, state: TextFieldState, focus: FocusRequester?, tag: String,
    keyboard: KeyboardOptions = KeyboardOptions(imeAction = ImeAction.Next), modifier: Modifier = Modifier.fillMaxWidth(),
) {
    OutlinedTextField(
        state = state,
        label = { Text(label) },
        lineLimits = TextFieldLineLimits.SingleLine,
        keyboardOptions = keyboard,
        modifier = modifier.padding(vertical = 2.dp).let { if (focus != null) it.focusRequester(focus) else it }.testTag(tag),
    )
}

/** MAUI's MonoEntry with IsPassword: one field whose obfuscation follows the eye (keeps text, focus and cursor). */
@Composable
private fun SecretLine(
    label: String, state: TextFieldState, shown: Boolean, enabled: Boolean = true, focus: FocusRequester? = null,
    tag: String, keyboardType: KeyboardType = KeyboardType.Password, modifier: Modifier = Modifier,
) {
    OutlinedSecureTextField(
        state = state,
        label = { Text(label) },
        enabled = enabled,
        textObfuscationMode = if (shown) TextObfuscationMode.Visible else TextObfuscationMode.RevealLastTyped,
        textStyle = androidx.compose.ui.text.TextStyle(fontFamily = FontFamily.Monospace),
        keyboardOptions = KeyboardOptions(keyboardType = keyboardType, autoCorrectEnabled = false, imeAction = ImeAction.Next),
        modifier = modifier.padding(vertical = 2.dp).let { if (focus != null) it.focusRequester(focus) else it }.testTag(tag),
    )
}

@Composable
private fun SwitchLine(label: String, checked: Boolean, tag: String, onChange: (Boolean) -> Unit) {
    Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.padding(vertical = 2.dp)) {
        Text(label, Modifier.weight(1f))
        Switch(checked = checked, onCheckedChange = onChange, modifier = Modifier.testTag(tag))
    }
}

@Composable
private fun IconGlyphButton(glyph: String, label: String, palette: AppPalette, onClick: () -> Unit) {
    IconButton(onClick = onClick, modifier = Modifier.semantics { contentDescription = label }) {
        Glyph(glyph, palette.primaryColor)
    }
}

/** A picker over an Int editable that holds the option's index (the view model's *SelectedIndex properties). */
@Composable
internal fun IndexPicker(label: String, options: List<String>, editable: com.adamessenmacher.oahu.runtime.OahuEditable<Int>, tag: String) {
    var selected by editable.collectAsMutableState()
    RowIndexPicker(label, options, selected, tag) { selected = it }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun RowIndexPicker(label: String, options: List<String>, selected: Int, tag: String = "", onSelect: (Int) -> Unit) {
    var open by remember { mutableStateOf(false) }
    ExposedDropdownMenuBox(expanded = open, onExpandedChange = { open = it }, modifier = Modifier.padding(vertical = 2.dp)) {
        OutlinedTextField(
            value = options.getOrNull(selected) ?: "",
            onValueChange = {},
            readOnly = true,
            label = { Text(label) },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = open) },
            modifier = Modifier.fillMaxWidth().menuAnchor(ExposedDropdownMenuAnchorType.PrimaryNotEditable).testTag(tag),
        )
        ExposedDropdownMenu(expanded = open, onDismissRequest = { open = false }) {
            options.forEachIndexed { i, text ->
                DropdownMenuItem(text = { Text(text) }, onClick = {
                    onSelect(i)
                    open = false
                })
            }
        }
    }
}

