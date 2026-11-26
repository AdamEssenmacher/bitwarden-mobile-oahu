package com.oahu.appapp

import com.adamessenmacher.oahu.compose.toColor
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.IconButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/**
 * Viewing one cipher (CipherDetailsPage). The MAUI page keeps its title, the Attachments, Clone, Move to
 * organization, Collections and Delete toolbar items, the floating edit button and every dialog (re-prompt, check
 * password, download). Hidden values arrive only while shown.
 */
@Composable
fun CipherDetailsScreen(model: CipherDetailsModel) {
    val palette by model.palette.collectAsState()
    val copy by model.copy.collectAsState()
    val details by model.details.collectAsState()
    val totp by model.totp.collectAsState()

    PaletteTheme(palette) {
        Box(Modifier.fillMaxSize().background(palette.backgroundColor)) {
            val d = details
            if (d == null) {
                CircularProgressIndicator(Modifier.align(Alignment.Center), color = palette.primaryColor)
            } else {
                // Bottom padding: the MAUI floating edit button sits over this screen (was _mainLayout.Padding).
                LazyColumn(Modifier.fillMaxSize(), contentPadding = PaddingValues(bottom = 75.dp)) {
                    item(key = "info") {
                        DetailsBox(copy.itemInformation, palette) {
                            ValueRow(copy.name, d.name, palette)
                            d.login?.let { LoginRows(it, totp, copy, palette, model) }
                            d.card?.let { CardRows(it, copy, palette, model) }
                            d.identity?.let { IdentityRows(it, copy, palette) }
                        }
                    }
                    if (d.uris.isNotEmpty()) {
                        item(key = "uris") {
                            DetailsBox(copy.uris, palette) {
                                d.uris.forEachIndexed { index, uri ->
                                    ValueRow(uri.label, uri.value, palette) {
                                        if (uri.canLaunch) {
                                            GlyphButton(BwIcons.SHARE_SQUARE, copy.launch, palette) { model.launchUri(index) }
                                        }
                                        GlyphButton(BwIcons.CLONE, copy.copy, palette) { model.copyUri(index) }
                                    }
                                }
                            }
                        }
                    }
                    d.notes?.let { notes ->
                        item(key = "notes") {
                            DetailsBox(copy.notes, palette) {
                                SelectionContainer {
                                    Text(notes, color = palette.textColor, modifier = Modifier.padding(vertical = 8.dp))
                                }
                            }
                        }
                    }
                    if (d.fields.isNotEmpty()) {
                        item(key = "fields") {
                            DetailsBox(copy.customFields, palette) {
                                d.fields.forEachIndexed { index, field -> FieldRowView(index, field, copy, palette, model) }
                            }
                        }
                    }
                    if (d.attachments.isNotEmpty()) {
                        item(key = "attachments") {
                            DetailsBox(copy.attachments, palette) {
                                d.attachments.forEach { a ->
                                    Row(Modifier.fillMaxWidth().padding(vertical = 4.dp),
                                        verticalAlignment = Alignment.CenterVertically) {
                                        Text(a.fileName, color = palette.textColor, modifier = Modifier.weight(1f))
                                        Text(a.size, color = palette.mutedColor, fontSize = 13.sp)
                                        GlyphButton(BwIcons.DOWNLOAD, copy.download, palette) { model.downloadAttachment(a.id) }
                                    }
                                }
                            }
                        }
                    }
                    item(key = "footer") {
                        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                            d.footer.forEach { line ->
                                val text = buildAnnotatedString {
                                    withStyle(SpanStyle(fontWeight = FontWeight.Bold)) { append(line.label) }
                                    append(" ")
                                    if (line.link) {
                                        withStyle(SpanStyle(color = palette.primaryColor)) { append(line.value) }
                                    } else {
                                        append(line.value)
                                    }
                                }
                                Text(text, color = palette.mutedColor, fontSize = 13.sp,
                                    modifier = if (line.link) Modifier.clickable { model.openPasswordHistory() } else Modifier)
                            }
                        }
                    }
                }
            }
        }
    }
}

/** A titled box (MAUI's "box" with a "box-header"). */
@Composable
private fun DetailsBox(title: String, palette: AppPalette, content: @Composable ColumnScope.() -> Unit) {
    Column(Modifier.fillMaxWidth().padding(top = 16.dp)) {
        Text(title.uppercase(), color = palette.boxHeaderText.toColor(), fontSize = 13.sp, fontWeight = FontWeight.SemiBold,
            modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp))
        HorizontalDivider(color = palette.separatorColor)
        Column(Modifier.padding(horizontal = 16.dp), content = content)
    }
}

@Composable
private fun ValueRow(
    label: String,
    value: String,
    palette: AppPalette,
    muted: Boolean = false,
    trailing: @Composable () -> Unit = {},
) {
    Row(Modifier.fillMaxWidth().padding(vertical = 6.dp), verticalAlignment = Alignment.CenterVertically) {
        Column(Modifier.weight(1f)) {
            Text(label, color = palette.mutedColor, fontSize = 13.sp)
            SelectionContainer {
                Text(value, color = if (muted) palette.mutedColor else palette.textColor)
            }
        }
        trailing()
    }
    HorizontalDivider(color = palette.separatorColor)
}

@Composable
private fun SecretRow(
    label: String,
    secret: SecretValue,
    copy: CipherDetailsCopy,
    palette: AppPalette,
    onToggle: () -> Unit,
    onCopy: () -> Unit,
    extra: @Composable () -> Unit = {},
) {
    Row(Modifier.fillMaxWidth().padding(vertical = 6.dp), verticalAlignment = Alignment.CenterVertically) {
        Column(Modifier.weight(1f)) {
            Text(label, color = palette.mutedColor, fontSize = 13.sp)
            val revealed = secret.revealed
            if (revealed != null) {
                SelectionContainer {
                    Text(colored(revealed, palette), fontFamily = FontFamily.Monospace)
                }
            } else {
                Text(secret.masked, color = palette.textColor, fontFamily = FontFamily.Monospace)
            }
        }
        extra()
        if (secret.canReveal) {
            GlyphButton(if (secret.revealed == null) BwIcons.EYE else BwIcons.EYE_SLASH,
                if (secret.revealed == null) copy.show else copy.hide, palette, onToggle)
        }
        if (secret.canCopy) {
            GlyphButton(BwIcons.CLONE, copy.copy, palette, onCopy)
        }
    }
    HorizontalDivider(color = palette.separatorColor)
}

/** GeneratedValueFormatter's coloring: letters in the text color, digits and symbols in their own. */
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
private fun LoginRows(
    login: LoginDetails,
    totp: TotpDisplay?,
    copy: CipherDetailsCopy,
    palette: AppPalette,
    model: CipherDetailsModel,
) {
    login.username?.let {
        ValueRow(copy.username, it, palette) {
            GlyphButton(BwIcons.CLONE, copy.copy, palette) { model.copyValue("LoginUsername") }
        }
    }
    login.password?.let {
        SecretRow(copy.password, it, copy, palette, { model.togglePassword() }, { model.copyValue("LoginPassword") }) {
            if (login.canCheckPassword) {
                GlyphButton(BwIcons.CHECK_CIRCLE, copy.checkPassword, palette) { model.checkPassword() }
            }
        }
    }
    login.passkeyCreated?.let { ValueRow(copy.passkey, it, palette, muted = true) }
    if (login.hasTotp && totp != null) {
        val color = if (totp.low) palette.dangerColor else palette.primaryColor
        Row(Modifier.fillMaxWidth().padding(vertical = 6.dp), verticalAlignment = Alignment.CenterVertically) {
            Column(Modifier.weight(1f)) {
                Text(copy.verificationCode, color = palette.mutedColor, fontSize = 13.sp)
                if (totp.premiumRequired) {
                    Text(copy.premiumRequired, color = palette.dangerColor, fontSize = 13.sp)
                } else {
                    Text(totp.code ?: "", color = if (totp.low) palette.dangerColor else palette.textColor,
                        fontFamily = FontFamily.Monospace, fontSize = 22.sp)
                }
            }
            if (!totp.premiumRequired) {
                TotpRing(totp.seconds, totp.progress, color, palette.separatorColor, 36.dp)
            }
            if (totp.canCopy) {
                GlyphButton(BwIcons.CLONE, copy.copy, palette) { model.copyValue("LoginTotp") }
            }
        }
        HorizontalDivider(color = palette.separatorColor)
    }
}

@Composable
private fun CardRows(card: CardDetails, copy: CipherDetailsCopy, palette: AppPalette, model: CipherDetailsModel) {
    card.cardholderName?.let { ValueRow(copy.cardholderName, it, palette) }
    card.number?.let {
        SecretRow(copy.number, it, copy, palette, { model.toggleCardNumber() }, { model.copyValue("CardNumber") })
    }
    card.brand?.let { ValueRow(copy.brand, it, palette) }
    card.expiration?.let { ValueRow(copy.expiration, it, palette) }
    card.code?.let {
        SecretRow(copy.securityCode, it, copy, palette, { model.toggleCardCode() }, { model.copyValue("CardCode") })
    }
}

@Composable
private fun IdentityRows(i: IdentityDetails, copy: CipherDetailsCopy, palette: AppPalette) {
    listOf(
        copy.identityName to i.name, copy.username to i.username, copy.company to i.company, copy.ssn to i.ssn,
        copy.passportNumber to i.passportNumber, copy.licenseNumber to i.licenseNumber, copy.email to i.email,
        copy.phone to i.phone,
    ).forEach { (label, value) -> value?.let { ValueRow(label, it, palette) } }
    if (i.address.isNotEmpty()) {
        ValueRow(copy.address, i.address.joinToString("\n"), palette)
    }
}

@Composable
private fun FieldRowView(index: Int, field: FieldRow, copy: CipherDetailsCopy, palette: AppPalette, model: CipherDetailsModel) {
    when (field) {
        is FieldRow.TextField -> ValueRow(field.name, field.value ?: "", palette) {
            if (field.canCopy) {
                GlyphButton(BwIcons.CLONE, copy.copy, palette) { model.copyField(index) }
            }
        }
        is FieldRow.HiddenField -> SecretRow(field.name, field.value, copy, palette,
            { model.toggleField(index) }, { model.copyField(index) })
        is FieldRow.BooleanField -> {
            Row(Modifier.fillMaxWidth().padding(vertical = 10.dp), verticalAlignment = Alignment.CenterVertically) {
                Text(field.name, color = palette.textColor, modifier = Modifier.weight(1f))
                Glyph(if (field.value) BwIcons.CHECK_SQUARE else BwIcons.SQUARE, palette.mutedColor)
            }
            HorizontalDivider(color = palette.separatorColor)
        }
        is FieldRow.LinkedField -> {
            Column(Modifier.fillMaxWidth().padding(vertical = 6.dp)) {
                Text(field.name, color = palette.mutedColor, fontSize = 13.sp)
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Glyph(BwIcons.LINK, palette.mutedColor, 16.sp)
                    Text(" " + field.linkedTo, color = palette.textColor)
                }
            }
            HorizontalDivider(color = palette.separatorColor)
        }
    }
}

@Composable
private fun GlyphButton(glyph: String, label: String, palette: AppPalette, onClick: () -> Unit) {
    IconButton(onClick = onClick, modifier = Modifier.size(40.dp).semantics { contentDescription = label }) {
        Glyph(glyph, palette.primaryColor, 20.sp)
    }
}
