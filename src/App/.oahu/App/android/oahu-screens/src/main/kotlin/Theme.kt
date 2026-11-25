package com.oahu.appapp

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.graphics.vector.path
import androidx.compose.ui.unit.dp

// Bitwarden's primary blues (src/Core/Resources/Styles/Light.xaml and Dark.xaml). The screens follow the system's
// dark mode only: the app's own theme setting (Light, Dark, Black, Nord) does not reach a native screen.
private val LightColors = lightColorScheme(
    primary = Color(0xFF175DDC),
    primaryContainer = Color(0xFFDBE5F6),
    background = Color.White,
    surface = Color.White,
)
private val DarkColors = darkColorScheme(
    primary = Color(0xFF52BDFB),
    primaryContainer = Color(0xFF1F3A66),
    background = Color(0xFF303030),
    surface = Color(0xFF303030),
)

@Composable
internal fun BitwardenTheme(content: @Composable () -> Unit) {
    MaterialTheme(colorScheme = if (isSystemInDarkTheme()) DarkColors else LightColors, content = content)
}

// Material's "add" and "folder" glyphs, drawn here because the module doesn't depend on material-icons.
internal val AddIcon: ImageVector = ImageVector.Builder("Add", 24.dp, 24.dp, 24f, 24f).apply {
    path(fill = SolidColor(Color.Black)) {
        moveTo(19f, 13f); horizontalLineTo(13f); verticalLineTo(19f); horizontalLineTo(11f); verticalLineTo(13f)
        horizontalLineTo(5f); verticalLineTo(11f); horizontalLineTo(11f); verticalLineTo(5f); horizontalLineTo(13f)
        verticalLineTo(11f); horizontalLineTo(19f); close()
    }
}.build()

internal val FolderIcon: ImageVector = ImageVector.Builder("Folder", 24.dp, 24.dp, 24f, 24f).apply {
    path(fill = SolidColor(Color.Black)) {
        moveTo(10f, 4f); horizontalLineTo(4f); curveTo(2.9f, 4f, 2.01f, 4.9f, 2.01f, 6f); lineTo(2f, 18f)
        curveTo(2f, 19.1f, 2.9f, 20f, 4f, 20f); horizontalLineTo(20f); curveTo(21.1f, 20f, 22f, 19.1f, 22f, 18f)
        verticalLineTo(8f); curveTo(22f, 6.9f, 21.1f, 6f, 20f, 6f); horizontalLineTo(12f); lineTo(10f, 4f); close()
    }
}.build()
