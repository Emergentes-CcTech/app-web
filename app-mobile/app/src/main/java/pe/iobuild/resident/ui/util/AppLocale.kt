package pe.iobuild.resident.ui.util

import android.content.Context
import android.content.res.Configuration
import android.content.res.Resources
import android.os.LocaleList
import java.util.Locale

/**
 * Idioma de la app: "system" sigue al teléfono (inglés si el teléfono está en inglés, español en otro caso);
 * "es" y "en" lo fijan. Se guarda aparte de la sesión para que no se borre al cerrar sesión.
 */
object AppLocale {
    const val SYSTEM = "system"
    const val SPANISH = "es"
    const val ENGLISH = "en"

    private const val PREFS = "iobuild_settings"
    private const val KEY = "app_language"

    fun saved(context: Context): String =
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getString(KEY, SYSTEM) ?: SYSTEM

    fun save(context: Context, language: String) {
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().putString(KEY, language).apply()
    }

    private fun localeFor(language: String): Locale? = when (language) {
        SPANISH -> Locale("es", "PE")
        ENGLISH -> Locale("en", "US")
        else -> null
    }

    private fun effectiveLocale(context: Context): Locale =
        localeFor(saved(context)) ?: Resources.getSystem().configuration.locales[0]

    /** Contexto de la actividad con el idioma elegido: de aquí leen los textos de las pantallas. */
    fun wrap(base: Context): Context {
        val locale = localeFor(saved(base)) ?: return base
        Locale.setDefault(locale)
        val config = Configuration(base.resources.configuration)
        config.setLocales(LocaleList(locale))
        return base.createConfigurationContext(config)
    }

    /** Aplica el idioma a los recursos de la aplicación: de aquí leen los mensajes del ViewModel y del repositorio. */
    @Suppress("DEPRECATION")
    fun applyToApp(appContext: Context) {
        val locale = effectiveLocale(appContext)
        Locale.setDefault(locale)
        val resources = appContext.resources
        val config = Configuration(resources.configuration)
        config.setLocales(LocaleList(locale))
        resources.updateConfiguration(config, resources.displayMetrics)
    }
}
