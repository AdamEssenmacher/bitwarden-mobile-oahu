package com.oahu.appapp

import com.adamessenmacher.oahu.compose.toColor
import android.util.Log
import androidx.compose.foundation.background
import androidx.compose.foundation.border
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
import androidx.compose.foundation.text.input.TextObfuscationMode
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuAnchorType
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.IconButton
import androidx.compose.material3.OutlinedSecureTextField
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Slider
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.adamessenmacher.oahu.compose.collectAsMutableState
import com.adamessenmacher.oahu.compose.rememberTextFieldState
import kotlin.math.roundToInt

/**
 * The password and username generator, hosted in the MAUI GeneratorPage (the Generator tab, and the modal add/edit
 * opens to pick a value). The page keeps its title, toolbar items (Select, Password history) and dialogs. Every
 * option is an editable value: the view model saves it, normalizes it against the password policy, and the control
 * shows its answer.
 */
@Composable
fun GeneratorScreen(model: GeneratorModel) {
    val palette by model.palette.collectAsState()
    val copy by model.copy.collectAsState()
    val layout by model.layout.collectAsState()
    val haptics = LocalHapticFeedback.current
    LaunchedEffect(model) {
        // A one-shot event: the value was copied (the MAUI toast still shows).
        model.copied.collect { haptics.performHapticFeedback(HapticFeedbackType.LongPress) }
    }
    PaletteTheme(palette) {
        Column(
            Modifier.fillMaxSize().background(palette.backgroundColor).imePadding()
                .verticalScroll(rememberScrollState()).padding(horizontal = 16.dp),
        ) {
            if (layout.policyInEffect) {
                Text(
                    copy.policyInEffect,
                    color = palette.mutedColor, fontWeight = FontWeight.Bold, fontSize = 13.sp,
                    modifier = Modifier.padding(top = 12.dp).fillMaxWidth()
                        .border(1.dp, palette.primaryColor).padding(10.dp),
                )
            }
            GeneratedValue(model, copy, palette, layout.isUsername)
            HorizontalDivider(color = palette.separatorColor)
            if (layout.showKindPicker) {
                var kind by model.kind.collectAsMutableState()
                Picker(copy.whatToGenerate, kind, copy.kinds.map { it.value to it.label }) { kind = it }
            }
            Header(copy.options, palette)
            if (layout.isUsername) UsernameOptions(model, copy, layout, palette) else PasswordOptions(model, copy, palette)
        }
    }
}

@Composable
private fun GeneratedValue(model: GeneratorModel, copy: GeneratorCopy, palette: AppPalette, isUsername: Boolean) {
    val runs by model.generated.collectAsState()
    Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.padding(vertical = 12.dp)) {
        Text(
            colored(runs, palette), fontFamily = FontFamily.Monospace, fontSize = 20.sp,
            modifier = Modifier.weight(1f),
        )
        IconButton(onClick = { model.copyValue() },
            modifier = Modifier.semantics { contentDescription = if (isUsername) copy.copyUsername else copy.copyPassword }) {
            Glyph(BwIcons.CLONE, palette.primaryColor)
        }
        IconButton(onClick = { model.regenerate() },
            modifier = Modifier.semantics {
                contentDescription = if (isUsername) copy.generateUsername else copy.generatePassword
            }) {
            Glyph(BwIcons.GENERATE, palette.primaryColor)
        }
    }
}

private fun colored(runs: List<TextRun>, palette: AppPalette): AnnotatedString = buildAnnotatedString {
    runs.forEach { run ->
        val color = when (run.kind) {
            TextRunKind.Letters -> palette.textColor
            TextRunKind.Digits -> palette.passwordNumber.toColor()
            TextRunKind.Symbols -> palette.passwordSpecial.toColor()
        }
        withStyle(SpanStyle(color = color)) { append(run.text) }
    }
}

@Composable
private fun PasswordOptions(model: GeneratorModel, copy: GeneratorCopy, palette: AppPalette) {
    val locks by model.locks.collectAsState()
    var passwordType by model.passwordType.collectAsMutableState()
    Picker(copy.passwordType, passwordType, copy.passwordKinds.map { it.value to it.label }) { passwordType = it }
    if (passwordType == PasswordKind.Passphrase) {
        var words by model.numberOfWords.collectAsMutableState()
        StepperRow(copy.numberOfWords, words, 3, 20, palette) { words = it }
        OutlinedTextField(
            state = model.wordSeparator.rememberTextFieldState(),
            label = { Text(copy.wordSeparator) },
            lineLimits = TextFieldLineLimits.SingleLine,
            // MAUI: spell check and prediction off, NoEmojiKeyboardEffect. Ascii keeps the emoji key off most keyboards.
            keyboardOptions = KeyboardOptions(autoCorrectEnabled = false, keyboardType = KeyboardType.Ascii),
            modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp),
        )
        var capitalize by model.capitalize.collectAsMutableState()
        SwitchRow(copy.capitalize, capitalize, !locks.capitalize) { capitalize = it }
        var includeNumber by model.includeNumber.collectAsMutableState()
        SwitchRow(copy.includeNumber, includeNumber, !locks.includeNumber) { includeNumber = it }
    } else {
        var length by model.length.collectAsMutableState()
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text(copy.length, Modifier.weight(1f))
            Text("$length", color = palette.mutedColor)
        }
        // The view model's Int is the slider's value, so the thumb follows the rounded answer; releasing it saves
        // the options (MAUI's DragCompleted).
        Slider(
            value = length.toFloat(),
            onValueChange = {
                val proposed = it.roundToInt()
                Log.d("OAHUDEBUG", "slider onValueChange $it -> $proposed")
                length = proposed
            },
            onValueChangeFinished = { model.lengthEditingEnded() },
            valueRange = 5f..128f,
        )
        var uppercase by model.uppercase.collectAsMutableState()
        SwitchRow(copy.uppercaseLabel, uppercase, !locks.uppercase, copy.uppercaseDescription) { uppercase = it }
        var lowercase by model.lowercase.collectAsMutableState()
        SwitchRow(copy.lowercaseLabel, lowercase, !locks.lowercase, copy.lowercaseDescription) { lowercase = it }
        var numbers by model.numbers.collectAsMutableState()
        SwitchRow(copy.numbersLabel, numbers, !locks.numbers, copy.numbersDescription) { numbers = it }
        var special by model.special.collectAsMutableState()
        SwitchRow(copy.specialLabel, special, !locks.special, copy.specialDescription) { special = it }
        var minNumbers by model.minNumbers.collectAsMutableState()
        StepperRow(copy.minNumbers, minNumbers, 0, 5, palette) { minNumbers = it }
        var minSpecial by model.minSpecial.collectAsMutableState()
        StepperRow(copy.minSpecial, minSpecial, 0, 5, palette) { minSpecial = it }
        var avoid by model.avoidAmbiguous.collectAsMutableState()
        SwitchRow(copy.avoidAmbiguous, avoid, true) { avoid = it }
    }
}

@Composable
private fun UsernameOptions(model: GeneratorModel, copy: GeneratorCopy, layout: GeneratorLayout, palette: AppPalette) {
    var usernameType by model.usernameType.collectAsMutableState()
    Row(verticalAlignment = Alignment.CenterVertically) {
        Column(Modifier.weight(1f)) {
            Picker(copy.usernameType, usernameType, copy.usernameKinds.map { it.value to it.label }) { usernameType = it }
        }
        IconButton(onClick = { model.showUsernameTypeHelp() },
            modifier = Modifier.semantics { contentDescription = copy.usernameTypeHelp }) {
            Glyph(BwIcons.QUESTION_CIRCLE, palette.primaryColor)
        }
    }
    if (layout.usernameTypeDescription.isNotEmpty()) {
        Text(layout.usernameTypeDescription, color = palette.mutedColor, fontSize = 13.sp)
    }
    when (usernameType) {
        UsernameKind.PlusAddressedEmail -> {
            EmailField(copy.emailRequired, model.plusAddressedEmail)
            if (layout.showEmailType) {
                var type by model.plusAddressedEmailType.collectAsMutableState()
                Picker(copy.emailType, type, copy.emailKinds.map { it.value to it.label }) { type = it }
                LabeledValue(copy.website, layout.emailWebsite ?: "", palette)
            }
        }
        UsernameKind.CatchAllEmail -> {
            EmailField(copy.domainRequired, model.catchAllDomain)
            if (layout.showEmailType) {
                var type by model.catchAllEmailType.collectAsMutableState()
                Picker(copy.emailType, type, copy.emailKinds.map { it.value to it.label }) { type = it }
                LabeledValue(copy.website, layout.emailWebsite ?: "", palette)
            }
        }
        UsernameKind.ForwardedEmailAlias -> {
            var service by model.service.collectAsMutableState()
            Picker(copy.service, service, copy.services.map { it.value to it.label }) { service = it }
            layout.apiSecretLabel?.let { label ->
                // MAUI: an Entry whose IsPassword followed the eye. One secure field whose obfuscation follows it, so
                // the field keeps focus and cursor when it changes.
                Row(verticalAlignment = Alignment.CenterVertically) {
                    OutlinedSecureTextField(
                        state = model.apiSecret.rememberTextFieldState(),
                        label = { Text(label) },
                        textObfuscationMode = if (layout.showApiSecret) TextObfuscationMode.Visible else TextObfuscationMode.RevealLastTyped,
                        modifier = Modifier.weight(1f),
                    )
                    IconButton(onClick = { model.toggleApiSecret() },
                        modifier = Modifier.semantics { contentDescription = copy.toggleVisibility }) {
                        Glyph(if (layout.showApiSecret) BwIcons.EYE_SLASH else BwIcons.EYE, palette.primaryColor)
                    }
                }
            }
            if (service == ForwardedService.AnonAddy) EmailField(copy.domainRequired, model.addyIoDomain)
            if (service == ForwardedService.ForwardEmail) EmailField(copy.domainRequired, model.forwardEmailDomain)
        }
        UsernameKind.RandomWord -> {
            var capitalize by model.capitalizeRandomWord.collectAsMutableState()
            SwitchRow(copy.capitalize, capitalize, true) { capitalize = it }
            var includeNumber by model.includeNumberRandomWord.collectAsMutableState()
            SwitchRow(copy.includeNumber, includeNumber, true) { includeNumber = it }
        }
    }
}

@Composable
private fun EmailField(label: String, editable: com.adamessenmacher.oahu.runtime.OahuEditable<String>) {
    OutlinedTextField(
        state = editable.rememberTextFieldState(),
        label = { Text(label) },
        lineLimits = TextFieldLineLimits.SingleLine,
        keyboardOptions = KeyboardOptions(autoCorrectEnabled = false, keyboardType = KeyboardType.Email),
        modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp),
    )
}

@Composable
private fun Header(text: String, palette: AppPalette) {
    Text(text.uppercase(), color = palette.mutedColor, fontSize = 13.sp, fontWeight = FontWeight.Bold,
        modifier = Modifier.padding(top = 16.dp, bottom = 4.dp))
}

@Composable
private fun LabeledValue(label: String, value: String, palette: AppPalette) {
    Row(Modifier.padding(vertical = 8.dp)) {
        Text(label, Modifier.weight(1f))
        Text(value, color = palette.mutedColor)
    }
}

@Composable
private fun SwitchRow(label: String, checked: Boolean, enabled: Boolean, description: String? = null, onChange: (Boolean) -> Unit) {
    Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.padding(vertical = 2.dp)) {
        Text(label, Modifier.weight(1f).semantics { description?.let { contentDescription = it } })
        Switch(checked = checked, onCheckedChange = onChange, enabled = enabled)
    }
}

/** Material3 has no stepper: minus and plus buttons; the view model may clamp the result. */
@Composable
private fun StepperRow(label: String, value: Int, min: Int, max: Int, palette: AppPalette, onChange: (Int) -> Unit) {
    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(4.dp)) {
        Text(label, Modifier.weight(1f))
        Text("$value", color = palette.mutedColor)
        TextButton(onClick = { onChange(value - 1) }, enabled = value > min) { Text("−", fontSize = 20.sp) }
        TextButton(onClick = { onChange(value + 1) }, enabled = value < max) { Text("+", fontSize = 20.sp) }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun <T> Picker(label: String, selected: T, options: List<Pair<T, String>>, onSelect: (T) -> Unit) {
    var open by remember { mutableStateOf(false) }
    ExposedDropdownMenuBox(expanded = open, onExpandedChange = { open = it }, modifier = Modifier.padding(vertical = 4.dp)) {
        OutlinedTextField(
            value = options.firstOrNull { it.first == selected }?.second ?: "",
            onValueChange = {},
            readOnly = true,
            label = { Text(label) },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = open) },
            modifier = Modifier.fillMaxWidth().menuAnchor(ExposedDropdownMenuAnchorType.PrimaryNotEditable),
        )
        ExposedDropdownMenu(expanded = open, onDismissRequest = { open = false }) {
            options.forEach { (value, text) ->
                DropdownMenuItem(text = { Text(text) }, onClick = {
                    onSelect(value)
                    open = false
                })
            }
        }
    }
}
