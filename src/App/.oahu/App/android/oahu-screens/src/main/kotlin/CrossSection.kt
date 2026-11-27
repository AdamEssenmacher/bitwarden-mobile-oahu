package com.oahu.appapp

import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.platform.LocalContext

// Step 8 (docs/oahu/08-cross-section.md): native channels between a parent screen and the sections it mounts with
// OahuMount. OahuMount calls the mounted screen's composable inside the parent's composition, so CompositionLocals
// flow down into it and the focus tree, IME actions and bring-into-view are shared with the parent. Nothing here is
// OAHU API. Turned on with the intent extra bwOahuCross=native; without it each screen keeps its own requesters.

/** The form's fields by id ("<section>.<field>"), registered by whichever screen draws them, and which one has focus. */
class FormFocusRegistry {
    val requesters = mutableMapOf<String, FocusRequester>()
    var focused by mutableStateOf<String?>(null)

    /** Focuses a field by id, wherever it is drawn; false when nothing registered it. */
    fun requestFocus(id: String): Boolean = requesters[id]?.let { it.requestFocus(); true } ?: false
}

val LocalFormFocus = staticCompositionLocalOf<FormFocusRegistry?> { null }

/** Registers this field with the parent's registry, when the parent provides one. */
@Composable
fun Modifier.formField(id: String): Modifier {
    val registry = LocalFormFocus.current ?: return this
    val requester = remember(id) { FocusRequester() }
    DisposableEffect(registry, id) {
        registry.requesters[id] = requester
        onDispose { registry.requesters.remove(id) }
    }
    return focusRequester(requester).onFocusChanged {
        if (it.isFocused) registry.focused = id else if (registry.focused == id) registry.focused = null
    }
}

/** The DEBUG switch, read from the activity's intent (the MAUI page reads the same extra in C#). */
@Composable
fun crossSectionNative(): Boolean {
    val context = LocalContext.current
    return remember(context) { context.activity()?.intent?.getStringExtra("bwOahuCross") == "native" }
}

private tailrec fun Context.activity(): Activity? = when (this) {
    is Activity -> this
    is ContextWrapper -> baseContext.activity()
    else -> null
}
