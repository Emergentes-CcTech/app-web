package pe.iobuild.resident.data.local

import android.content.Context
import android.content.SharedPreferences
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey
import pe.iobuild.resident.BuildConfig

/**
 * Almacena la sesión autenticada del Propietario/Residente (JWT Token, ID, Correo, Unidad, Alícuota),
 * la sesión de pago pendiente de Stripe Checkout y la URL del servidor Backend/BFF
 * utilizando `EncryptedSharedPreferences` respaldado por Android Keystore (AES256-GCM).
 */
class SessionManager(context: Context) {

    private val prefs: SharedPreferences = createSecurePreferences(context.applicationContext)

    private fun createSecurePreferences(appContext: Context): SharedPreferences {
        return try {
            val masterKey = MasterKey.Builder(appContext)
                .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
                .build()
            EncryptedSharedPreferences.create(
                appContext,
                "iobuild_resident_secure_session",
                masterKey,
                EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
                EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
            )
        } catch (_: Exception) {
            appContext.getSharedPreferences("iobuild_resident_session_fallback", Context.MODE_PRIVATE)
        }
    }

    companion object {
        private const val KEY_TOKEN = "jwt_token"
        private const val KEY_USER_ID = "user_id"
        private const val KEY_EMAIL = "user_email"
        private const val KEY_ROLE = "user_role"
        private const val KEY_NAME = "user_name"
        private const val KEY_PHOTO_URL = "user_photo_url"
        private const val KEY_UNIT_ID = "unit_id"
        private const val KEY_UNIT_NUMBER = "unit_number"
        private const val KEY_PROJECT_ID = "project_id"
        private const val KEY_ALIQUOT = "aliquot_pct"
        private const val KEY_API_BASE_URL = "api_base_url_override"
        private const val KEY_LANGUAGE = "app_language"

        // Estado persistido de Checkout Stripe pendiente de confirmar al volver a la app (onResume)
        private const val KEY_PENDING_CHECKOUT_PAYMENT_ID = "pending_checkout_payment_id"
        private const val KEY_PENDING_CHECKOUT_SESSION_ID = "pending_checkout_session_id"

        // Preferencias de notificación exclusivas de Residente (RF-003)
        private const val PREF_NOTIF_EMAIL = "notif_email"
        private const val PREF_NOTIF_NEW_SETTLEMENT = "notif_new_settlement"
        private const val PREF_NOTIF_DUE_DATES = "notif_due_dates"
        private const val PREF_NOTIF_CONDO_ALERTS = "notif_condo_alerts"
    }

    var token: String?
        get() = prefs.getString(KEY_TOKEN, null)
        set(value) = prefs.edit().putString(KEY_TOKEN, value).apply()

    var userId: Int
        get() = prefs.getInt(KEY_USER_ID, 0)
        set(value) = prefs.edit().putInt(KEY_USER_ID, value).apply()

    var email: String
        get() = prefs.getString(KEY_EMAIL, "") ?: ""
        set(value) = prefs.edit().putString(KEY_EMAIL, value).apply()

    var role: String
        get() = prefs.getString(KEY_ROLE, "resident") ?: "resident"
        set(value) = prefs.edit().putString(KEY_ROLE, value).apply()

    var displayName: String
        get() = prefs.getString(KEY_NAME, "") ?: ""
        set(value) = prefs.edit().putString(KEY_NAME, value).apply()

    var photoUrl: String
        get() = prefs.getString(KEY_PHOTO_URL, "") ?: ""
        set(value) = prefs.edit().putString(KEY_PHOTO_URL, value).apply()

    var unitId: Int
        get() = prefs.getInt(KEY_UNIT_ID, 0)
        set(value) = prefs.edit().putInt(KEY_UNIT_ID, value).apply()

    var unitNumber: String
        get() = prefs.getString(KEY_UNIT_NUMBER, "") ?: ""
        set(value) = prefs.edit().putString(KEY_UNIT_NUMBER, value).apply()

    var projectId: Int
        get() = prefs.getInt(KEY_PROJECT_ID, 1)
        set(value) = prefs.edit().putInt(KEY_PROJECT_ID, value).apply()

    var aliquotPercentage: Double
        get() = Double.fromBits(prefs.getLong(KEY_ALIQUOT, (0.0).toBits()))
        set(value) = prefs.edit().putLong(KEY_ALIQUOT, value.toBits()).apply()

    // El servidor es el de la compilación (producción). Solo una compilación debug acepta un servidor
    // distinto elegido a mano para pruebas; la clave anterior se ignora para no arrastrar el 10.0.2.2 guardado
    var apiBaseUrl: String
        get() {
            val override = if (BuildConfig.DEBUG) prefs.getString(KEY_API_BASE_URL, null) else null
            val raw = override?.takeIf { it.isNotBlank() } ?: BuildConfig.DEFAULT_API_BASE_URL
            return if (raw.endsWith("/")) raw else "$raw/"
        }
        set(value) {
            val trimmed = value.trim()
            if (trimmed.isBlank() || trimmed.trimEnd('/') == BuildConfig.DEFAULT_API_BASE_URL.trimEnd('/')) {
                prefs.edit().remove(KEY_API_BASE_URL).apply()
                return
            }
            prefs.edit().putString(KEY_API_BASE_URL, if (trimmed.endsWith("/")) trimmed else "$trimmed/").apply()
        }

    val isUsingCustomServer: Boolean
        get() = apiBaseUrl.trimEnd('/') != BuildConfig.DEFAULT_API_BASE_URL.trimEnd('/')

    var language: String
        get() = prefs.getString(KEY_LANGUAGE, "es") ?: "es"
        set(value) = prefs.edit().putString(KEY_LANGUAGE, value).apply()

    var pendingCheckoutPaymentId: Int
        get() = prefs.getInt(KEY_PENDING_CHECKOUT_PAYMENT_ID, 0)
        set(value) = prefs.edit().putInt(KEY_PENDING_CHECKOUT_PAYMENT_ID, value).apply()

    var pendingCheckoutSessionId: String?
        get() = prefs.getString(KEY_PENDING_CHECKOUT_SESSION_ID, null)
        set(value) = prefs.edit().putString(KEY_PENDING_CHECKOUT_SESSION_ID, value).apply()

    fun savePendingCheckout(paymentId: Int, sessionId: String) {
        prefs.edit()
            .putInt(KEY_PENDING_CHECKOUT_PAYMENT_ID, paymentId)
            .putString(KEY_PENDING_CHECKOUT_SESSION_ID, sessionId)
            .apply()
    }

    fun clearPendingCheckout() {
        prefs.edit()
            .remove(KEY_PENDING_CHECKOUT_PAYMENT_ID)
            .remove(KEY_PENDING_CHECKOUT_SESSION_ID)
            .apply()
    }

    var notifEmail: Boolean
        get() = prefs.getBoolean(PREF_NOTIF_EMAIL, true)
        set(value) = prefs.edit().putBoolean(PREF_NOTIF_EMAIL, value).apply()

    var notifNewSettlement: Boolean
        get() = prefs.getBoolean(PREF_NOTIF_NEW_SETTLEMENT, true)
        set(value) = prefs.edit().putBoolean(PREF_NOTIF_NEW_SETTLEMENT, value).apply()

    var notifDueDates: Boolean
        get() = prefs.getBoolean(PREF_NOTIF_DUE_DATES, true)
        set(value) = prefs.edit().putBoolean(PREF_NOTIF_DUE_DATES, value).apply()

    var notifCondoAlerts: Boolean
        get() = prefs.getBoolean(PREF_NOTIF_CONDO_ALERTS, true)
        set(value) = prefs.edit().putBoolean(PREF_NOTIF_CONDO_ALERTS, value).apply()

    val isAuthenticated: Boolean
        get() = !token.isNullOrBlank() && userId > 0

    fun saveAuthSession(
        token: String,
        userId: Int,
        email: String,
        role: String,
        projectId: Int? = null,
        unitId: Int? = null,
        unitNumber: String? = null,
        aliquotPercentage: Double? = null
    ) {
        prefs.edit().apply {
            putString(KEY_TOKEN, token)
            putInt(KEY_USER_ID, userId)
            putString(KEY_EMAIL, email)
            putString(KEY_ROLE, role)
            if (projectId != null) putInt(KEY_PROJECT_ID, projectId)
            if (unitId != null) putInt(KEY_UNIT_ID, unitId)
            if (!unitNumber.isNullOrBlank()) putString(KEY_UNIT_NUMBER, unitNumber)
            if (aliquotPercentage != null) putLong(KEY_ALIQUOT, aliquotPercentage.toBits())
        }.apply()
    }

    fun clearSession() {
        val customUrl = if (isUsingCustomServer) apiBaseUrl else null
        val currentLang = language
        prefs.edit().clear().apply()
        if (customUrl != null) apiBaseUrl = customUrl
        language = currentLang
    }
}
