package com.oahu.appapp

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.size
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.TextUnit
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import coil3.compose.SubcomposeAsyncImage

/** Bitwarden's icon font (res/font/bwi_font.ttf, a copy of src/Core/Resources/Fonts/bwi-font.ttf). */
internal val BwiFont = FontFamily(Font(R.font.bwi_font))

/** Code points of BitwardenIcons (C#) for the screens' own buttons; row glyphs come from C#. */
internal object BwIcons {
    const val CLONE = ""
    const val SHARE_SQUARE = ""
    const val CHECK_CIRCLE = ""
    const val EYE = ""
    const val EYE_SLASH = ""
    const val DOWNLOAD = ""
    const val COLLECTION = ""
    const val PAPERCLIP = ""
    const val LINK = ""
    const val SQUARE = ""
    const val CHECK_SQUARE = ""
    const val GENERATE = "\ue90e"
    const val QUESTION_CIRCLE = "\ue923"
    const val CAMERA = "\ue968"
    const val COG = "\ue910"
}

internal fun argb(value: Long): Color = Color(value.toInt())

internal val AppPalette.backgroundColor get() = argb(background)
internal val AppPalette.textColor get() = argb(text)
internal val AppPalette.mutedColor get() = argb(muted)
internal val AppPalette.primaryColor get() = argb(primary)
internal val AppPalette.dangerColor get() = argb(danger)
internal val AppPalette.headerBackgroundColor get() = argb(listHeaderBackground)
internal val AppPalette.headerTextColor get() = argb(listHeaderText)
internal val AppPalette.separatorColor get() = argb(separator)

/** Material colors from the app's own theme (Light, Dark, Black, Nord, Solarized Dark), sent by C#. */
@Composable
internal fun PaletteTheme(palette: AppPalette, content: @Composable () -> Unit) {
    val scheme = if (palette.isDark) {
        darkColorScheme(
            primary = palette.primaryColor, background = palette.backgroundColor, surface = palette.backgroundColor,
            onBackground = palette.textColor, onSurface = palette.textColor, onSurfaceVariant = palette.mutedColor,
            outlineVariant = palette.separatorColor, error = palette.dangerColor,
        )
    } else {
        lightColorScheme(
            primary = palette.primaryColor, background = palette.backgroundColor, surface = palette.backgroundColor,
            onBackground = palette.textColor, onSurface = palette.textColor, onSurfaceVariant = palette.mutedColor,
            outlineVariant = palette.separatorColor, error = palette.dangerColor,
        )
    }
    MaterialTheme(colorScheme = scheme, content = content)
}

@Composable
internal fun Glyph(glyph: String, color: Color, size: TextUnit = 22.sp, modifier: Modifier = Modifier) {
    Text(glyph, fontFamily = BwiFont, fontSize = size, color = color, modifier = modifier)
}

/** The website icon by URL (Coil), with the type glyph while it loads, on failure and when there is none. */
@Composable
internal fun CipherIcon(url: String?, glyph: String, palette: AppPalette, modifier: Modifier = Modifier) {
    Box(modifier.size(40.dp), contentAlignment = Alignment.Center) {
        if (url == null) {
            Glyph(glyph, palette.mutedColor)
        } else {
            SubcomposeAsyncImage(
                model = url,
                contentDescription = null,
                modifier = Modifier.size(22.dp),
                loading = { Glyph(glyph, palette.mutedColor) },
                error = { Glyph(glyph, palette.mutedColor) },
            )
        }
    }
}

/** The verification code's countdown (MAUI: CircularProgressbarView with the seconds inside). */
@Composable
internal fun TotpRing(seconds: String, progress: Double, color: Color, track: Color, size: Dp = 32.dp) {
    Box(Modifier.size(size), contentAlignment = Alignment.Center) {
        CircularProgressIndicator(
            progress = { (progress / 100.0).toFloat().coerceIn(0f, 1f) },
            modifier = Modifier.size(size),
            color = color,
            trackColor = track,
            strokeWidth = 3.dp,
        )
        Text(seconds, fontSize = 11.sp, color = color)
    }
}
