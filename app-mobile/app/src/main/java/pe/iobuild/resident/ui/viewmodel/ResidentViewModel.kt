package pe.iobuild.resident.ui.viewmodel

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.launch
import pe.iobuild.resident.data.local.SessionManager
import pe.iobuild.resident.data.model.*
import pe.iobuild.resident.data.repository.ResidentBffRepository

/**
 * ViewModel principal que orquesta el estado reactivo de las vistas exclusivas
 * del Propietario/Residente consumiendo la capa Backend for Frontend (BFF).
 */
class ResidentViewModel(
    val sessionManager: SessionManager,
    private val repository: ResidentBffRepository
) : ViewModel() {

    // Estado de sesión
    var isAuthenticated by mutableStateOf(sessionManager.isAuthenticated)
        private set

    var authLoading by mutableStateOf(false)
        private set

    var authError by mutableStateOf<String?>(null)
        private set

    // Estado de Registro en 2 pasos (Propietario pre-invitado)
    var registerStep by mutableStateOf(1)
        private set
    var invitedUnits by mutableStateOf<List<InvitedUnitInfo>>(emptyList())
        private set

    // 1. Estado BFF Overview ("Mi Consumo")
    var overviewLoading by mutableStateOf(false)
        private set
    var overviewError by mutableStateOf<String?>(null)
        private set
    var overviewData by mutableStateOf<ResidentOverviewResponse?>(null)
        private set

    // Asistente IA ("Pregúntale a IoBuild")
    var aiLoading by mutableStateOf(false)
        private set
    var aiAnswer by mutableStateOf<AiExplanationResponse?>(null)
        private set
    var aiError by mutableStateOf<String?>(null)
        private set

    // 2. Estado BFF Account Statement ("Estado de Cuenta")
    var statementLoading by mutableStateOf(false)
        private set
    var statementError by mutableStateOf<String?>(null)
        private set
    var statementData by mutableStateOf<ResidentAccountStatementResponse?>(null)
        private set
    var paymentInProgress by mutableStateOf(false)
        private set
    var paymentNotice by mutableStateOf<String?>(null)
        private set

    // 3. Estado BFF Fee Explanation ("Mi Cuota Actual")
    var feeLoading by mutableStateOf(false)
        private set
    var feeError by mutableStateOf<String?>(null)
        private set
    var feeData by mutableStateOf<ResidentFeeExplanationResponse?>(null)
        private set
    var selectedLineId by mutableStateOf<Int?>(null)
        private set

    // 4. Estado BFF Profile ("Mi Perfil")
    var profileLoading by mutableStateOf(false)
        private set
    var profileSaving by mutableStateOf(false)
        private set
    var profileError by mutableStateOf<String?>(null)
        private set
    var profileSuccessMessage by mutableStateOf<String?>(null)
        private set
    var profileData by mutableStateOf<ResidentProfileBffResponse?>(null)
        private set

    init {
        if (isAuthenticated) {
            refreshAllResidentViews()
        }
    }

    fun clearAuthError() {
        authError = null
    }

    fun clearPaymentNotice() {
        paymentNotice = null
    }

    fun clearProfileMessages() {
        profileError = null
        profileSuccessMessage = null
    }

    fun updateServerBaseUrl(newUrl: String) {
        sessionManager.apiBaseUrl = newUrl
    }

    fun refreshAllResidentViews() {
        loadOverview()
        loadAccountStatement()
        loadFeeExplanation(selectedLineId)
        loadProfile()
    }

    // ── Autenticación y Registro de Propietario ──

    fun signIn(email: String, password: String, onSuccess: () -> Unit = {}) {
        if (email.isBlank() || password.isBlank()) {
            authError = "Ingresa tu correo electrónico y contraseña."
            return
        }
        viewModelScope.launch {
            authLoading = true
            authError = null
            val res = repository.loginResident(email, password)
            authLoading = false
            res.onSuccess {
                isAuthenticated = true
                refreshAllResidentViews()
                onSuccess()
            }.onFailure { err ->
                authError = err.message ?: "Credenciales inválidas."
            }
        }
    }

    fun verifyInvitationForStep2(email: String, password: String, confirmPass: String) {
        if (email.isBlank() || !email.contains("@")) {
            authError = "Ingresa un correo electrónico válido."
            return
        }
        if (password.length < 8) {
            authError = "La contraseña debe tener al menos 8 caracteres."
            return
        }
        if (password != confirmPass) {
            authError = "Las contraseñas no coinciden."
            return
        }
        viewModelScope.launch {
            authLoading = true
            authError = null
            val res = repository.checkInvitation(email)
            authLoading = false
            res.onSuccess { check ->
                when {
                    check.alreadyRegistered -> {
                        authError = "Este correo electrónico ya está registrado. Inicia sesión."
                    }
                    !check.assigned -> {
                        authError = "Este correo no está registrado en ningún condominio. Pide a tu administración que te registre como propietario."
                    }
                    else -> {
                        invitedUnits = check.units.orEmpty()
                        registerStep = 2
                    }
                }
            }.onFailure { err ->
                authError = err.message
            }
        }
    }

    fun backToRegisterStep1() {
        registerStep = 1
        authError = null
    }

    fun completeResidentRegistration(
        email: String,
        password: String,
        fullName: String,
        dni: String,
        phoneNumber: String,
        acceptedPrivacy: Boolean,
        onSuccess: () -> Unit
    ) {
        if (fullName.trim().length < 2) {
            authError = "Ingresa tu nombre completo."
            return
        }
        if (dni.trim().length != 8 || !dni.trim().all { it.isDigit() }) {
            authError = "El DNI debe tener exactamente 8 dígitos."
            return
        }
        if (phoneNumber.trim().length < 7) {
            authError = "Ingresa un número telefónico válido."
            return
        }
        if (!acceptedPrivacy) {
            authError = "Debes aceptar las políticas de privacidad y de datos de telemetría."
            return
        }

        viewModelScope.launch {
            authLoading = true
            authError = null
            val res = repository.registerResident(email, password, fullName, dni, phoneNumber)
            authLoading = false
            res.onSuccess {
                isAuthenticated = true
                registerStep = 1
                refreshAllResidentViews()
                onSuccess()
            }.onFailure { err ->
                authError = err.message
            }
        }
    }

    fun signOut() {
        viewModelScope.launch {
            repository.logout()
            isAuthenticated = false
            overviewData = null
            statementData = null
            feeData = null
            profileData = null
            aiAnswer = null
        }
    }

    // ── 1. BFF Overview ("Mi Consumo") ──

    fun loadOverview() {
        viewModelScope.launch {
            overviewLoading = true
            overviewError = null
            repository.fetchOverview()
                .onSuccess { overviewData = it }
                .onFailure { overviewError = it.message }
            overviewLoading = false
        }
    }

    fun askAi(question: String) {
        if (question.isBlank()) return
        viewModelScope.launch {
            aiLoading = true
            aiError = null
            repository.askAiAssistant(question)
                .onSuccess { aiAnswer = it }
                .onFailure { aiError = it.message }
            aiLoading = false
        }
    }

    // ── 2. BFF Account Statement ("Estado de Cuenta") & Confirmación de Pago Stripe ──

    fun loadAccountStatement() {
        viewModelScope.launch {
            statementLoading = true
            statementError = null
            repository.fetchAccountStatement()
                .onSuccess { statementData = it }
                .onFailure { statementError = it.message }
            statementLoading = false
        }
    }

    fun payPendingStatement(paymentId: Int, onOpenExternalCheckoutUrl: (String) -> Unit) {
        viewModelScope.launch {
            paymentInProgress = true
            paymentNotice = null
            val res = repository.payStatementWithStripe(paymentId)
            paymentInProgress = false
            res.onSuccess { checkout ->
                val statusNorm = checkout.status?.lowercase()
                val redirectUrl = checkout.url ?: checkout.checkoutUrl
                val resolvedPaymentId = checkout.paymentId ?: paymentId
                val sessionId = checkout.sessionId

                if (statusNorm == "paid") {
                    sessionManager.clearPendingCheckout()
                    paymentNotice = "¡Pago procesado y conciliado exitosamente con Stripe!"
                    loadAccountStatement()
                    loadOverview()
                    loadFeeExplanation(selectedLineId)
                } else if (!redirectUrl.isNullOrBlank()) {
                    if (!sessionId.isNullOrBlank()) {
                        sessionManager.savePendingCheckout(resolvedPaymentId, sessionId)
                    }
                    onOpenExternalCheckoutUrl(redirectUrl)
                } else if (!sessionId.isNullOrBlank()) {
                    sessionManager.savePendingCheckout(resolvedPaymentId, sessionId)
                    confirmPendingCheckoutIfNeeded()
                } else {
                    paymentNotice = "Sesión de pago iniciada."
                    loadAccountStatement()
                }
            }.onFailure { err ->
                paymentNotice = err.message ?: "No se pudo iniciar el pago."
            }
        }
    }

    /**
     * Se invoca al volver a la aplicación (`onResume`) para confirmar en el backend
     * la sesión de Stripe Checkout (`POST /api/v1/payments/{paymentId}/checkout-session/confirm`).
     */
    fun confirmPendingCheckoutIfNeeded() {
        if (!isAuthenticated || paymentInProgress) return
        val sessionId = sessionManager.pendingCheckoutSessionId
        val paymentId = sessionManager.pendingCheckoutPaymentId
        if (sessionId.isNullOrBlank() || paymentId <= 0) return

        viewModelScope.launch {
            paymentInProgress = true
            val res = repository.confirmCheckoutSession(paymentId, sessionId)
            paymentInProgress = false
            res.onSuccess { confirm ->
                val statusNorm = confirm.status?.lowercase()
                val stripeStatusNorm = confirm.stripePaymentStatus?.lowercase()
                if (statusNorm == "paid" || stripeStatusNorm == "paid") {
                    sessionManager.clearPendingCheckout()
                    paymentNotice = "¡Pago confirmado y conciliado exitosamente con Stripe!"
                    loadAccountStatement()
                    loadOverview()
                    loadFeeExplanation(selectedLineId)
                } else if (stripeStatusNorm != null) {
                    sessionManager.clearPendingCheckout()
                    paymentNotice = "La sesión de pago en Stripe aún no fue completada."
                    loadAccountStatement()
                }
            }.onFailure { err ->
                sessionManager.clearPendingCheckout()
                paymentNotice = err.message ?: "No se pudo verificar el estado del pago."
                loadAccountStatement()
            }
        }
    }

    // ── 3. BFF Fee Explanation ("Mi Cuota Actual") ──

    fun loadFeeExplanation(lineId: Int? = null) {
        selectedLineId = lineId
        viewModelScope.launch {
            feeLoading = true
            feeError = null
            repository.fetchFeeExplanation(lineId)
                .onSuccess {
                    feeData = it
                    if (selectedLineId == null) {
                        selectedLineId = it.explanation?.lineId
                    }
                }
                .onFailure { feeError = it.message }
            feeLoading = false
        }
    }

    // ── 4. BFF Profile ("Mi Perfil") ──

    fun loadProfile() {
        viewModelScope.launch {
            profileLoading = true
            profileError = null
            repository.fetchProfile()
                .onSuccess { profileData = it }
                .onFailure { profileError = it.message }
            profileLoading = false
        }
    }

    fun saveProfileChanges(
        fullName: String,
        phoneNumber: String,
        secondEmail: String,
        photoUrl: String?
    ) {
        val current = profileData ?: return
        if (fullName.trim().length < 2) {
            profileError = "El nombre completo debe tener al menos 2 caracteres."
            return
        }
        if (phoneNumber.trim().length < 7) {
            profileError = "Ingresa un número de teléfono válido."
            return
        }
        viewModelScope.launch {
            profileSaving = true
            profileError = null
            profileSuccessMessage = null
            repository.updateProfile(
                profileId = current.profileId,
                fullName = fullName,
                phoneNumber = phoneNumber,
                secondEmail = secondEmail,
                photoUrl = photoUrl
            ).onSuccess { updated ->
                profileData = updated
                profileSuccessMessage = "Perfil actualizado correctamente."
                loadAccountStatement()
            }.onFailure { err ->
                profileError = err.message ?: "No se pudo actualizar el perfil."
            }
            profileSaving = false
        }
    }

    class Factory(
        private val sessionManager: SessionManager,
        private val repository: ResidentBffRepository
    ) : ViewModelProvider.Factory {
        @Suppress("UNCHECKED_CAST")
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            return ResidentViewModel(sessionManager, repository) as T
        }
    }
}
