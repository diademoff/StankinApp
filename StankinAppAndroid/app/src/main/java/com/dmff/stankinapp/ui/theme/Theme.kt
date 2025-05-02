package com.dmff.stankinapp.ui.theme

import android.os.Build
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.platform.LocalContext

private val DarkColorScheme = darkColors(
    primary = Purple80,
    secondary = PurpleGrey80
)

private val LightColorScheme = lightColors(
    primary = Purple40,
    secondary = PurpleGrey40
    /* Другие цвета по умолчанию можно оставить или переопределить */
)

@Composable
fun StankinAppTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    // Dynamic color не поддерживается в material
    content: @Composable () -> Unit
) {
    val colors = if (darkTheme) {
        DarkColorScheme
    } else {
        LightColorScheme
    }

    MaterialTheme(
        colors = colors,
        content = content
    )
}