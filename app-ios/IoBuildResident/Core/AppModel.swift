import SwiftUI
import Observation

/// Mensaje ya traducido para mostrar al propietario.
struct UserFacingError: Error {
    let message: String
    init(_ message: String) { self.message = message }
}

/// Página de pago de Stripe abierta dentro de la app.
struct CheckoutPage: Identifiable {
    let id = UUID()
    let url: URL
}

/// Estado y acciones de la app del propietario (equivale al ViewModel + repositorio de Android).
@MainActor
@Observable
final class AppModel {
    enum Tab: Hashable { case consumption, payments, fee, profile }

    let session: SessionStore
    private let api: APIClient

    // Sesión y registro
    var isAuthenticated: Bool
    var authLoading = false
    var authError: String?
    var registerStep = 1
    var invitedUnits: [InvitedUnit] = []

    var selectedTab: Tab = .consumption

    // Mi consumo
    var overview: ResidentOverview?
    var overviewLoading = false
    var overviewError: String?

    // Asistente IA
    var aiLoading = false
    var aiAnswer: AIExplanation?
    var aiError: String?

    // Estado de cuenta y pagos
    var statement: AccountStatement?
    var statementLoading = false
    var statementError: String?
    var paymentInProgress = false
    var paymentNotice: String?
    var paymentNoticeIsError = false
    var checkout: CheckoutPage?

    // Mi cuota
    var fee: FeeExplanationResponse?
    var feeLoading = false
    var feeError: String?
    var selectedLineId: Int?

    // Perfil
    var profile: ResidentProfile?
    var profileLoading = false
    var profileSaving = false
    var profileError: String?
    var profileMessage: String?

    init() {
        let store = SessionStore()
        session = store
        api = APIClient(session: store)
        isAuthenticated = store.isAuthenticated
        if isAuthenticated { refreshAll() }
    }

    // MARK: - Carga general

    func refreshAll() {
        Task { await reloadAll() }
    }

    func reloadAll() async {
        let lineId = selectedLineId
        async let overviewTask: Void = loadOverview()
        async let statementTask: Void = loadStatement()
        async let feeTask: Void = loadFee(lineId)
        async let profileTask: Void = loadProfile()
        _ = await (overviewTask, statementTask, feeTask, profileTask)
    }

    // MARK: - Inicio de sesión y registro

    func signIn(email: String, password: String) async {
        guard !email.isBlank, !password.isEmpty else {
            authError = L10n.t("err_login_empty")
            return
        }
        authLoading = true
        authError = nil
        defer { authLoading = false }
        do {
            try await login(email: email, password: password)
            isAuthenticated = true
            refreshAll()
        } catch let error as UserFacingError {
            authError = error.message
        } catch {
            authError = L10n.t("err_invalid_credentials")
        }
    }

    private func login(email: String, password: String) async throws {
        let auth: AuthResponse
        do {
            auth = try await api.post("sessions", body: SignInRequest(email: email.trimmed.lowercased(), password: password))
        } catch APIError.status {
            throw UserFacingError(L10n.t("err_wrong_credentials"))
        } catch {
            throw UserFacingError(L10n.t("err_connection", connectionDetail))
        }
        // La app es solo para propietarios: la administración usa la consola web
        let role = (auth.role ?? "").lowercased()
        guard ["resident", "residente", "owner", "propietario"].contains(role) else {
            throw UserFacingError(L10n.t("err_owners_only"))
        }
        session.saveAuth(auth)
    }

    private var connectionDetail: String {
        #if DEBUG
        return " (\(session.baseURL))"
        #else
        return ""
        #endif
    }

    /// Paso 1 del registro: el correo debe haber sido invitado por la administración.
    func verifyInvitation(email: String, password: String, confirmation: String) async {
        let normalized = email.trimmed.lowercased()
        guard normalized.contains("@"), normalized.contains(".") else { authError = L10n.t("err_email_invalid"); return }
        guard password.count >= 8 else { authError = L10n.t("err_password_short"); return }
        guard password == confirmation else { authError = L10n.t("err_password_mismatch"); return }

        authLoading = true
        authError = nil
        defer { authLoading = false }
        do {
            let check: InvitationCheck = try await api.get("authentication/invitation", query: [URLQueryItem(name: "email", value: normalized)])
            if check.alreadyRegistered == true {
                authError = L10n.t("err_email_registered")
            } else if check.assigned != true {
                authError = L10n.t("err_not_invited")
            } else {
                invitedUnits = check.units ?? []
                registerStep = 2
            }
        } catch APIError.status {
            authError = L10n.t("err_invitation_check")
        } catch {
            authError = L10n.t("err_invitation_connection")
        }
    }

    func backToRegisterStep1() {
        registerStep = 1
        authError = nil
    }

    /// Paso 2: crea la cuenta (el backend la vincula a la unidad invitada) y completa el perfil.
    func completeRegistration(email: String, password: String, fullName: String, phone: String, acceptedPrivacy: Bool) async {
        guard fullName.trimmed.count >= 2 else { authError = L10n.t("err_full_name"); return }
        guard phone.trimmed.count >= 7 else { authError = L10n.t("err_phone"); return }
        guard acceptedPrivacy else { authError = L10n.t("err_privacy"); return }

        authLoading = true
        authError = nil
        defer { authLoading = false }
        let normalized = email.trimmed.lowercased()
        do {
            try await api.send("users", method: "POST", body: SignUpRequest(email: normalized, password: password, role: "Resident"))
        } catch APIError.status(let code) {
            switch code {
            case 403: authError = L10n.t("err_no_unit")
            case 409: authError = L10n.t("err_email_registered")
            default: authError = L10n.t("err_register")
            }
            return
        } catch {
            authError = L10n.t("err_connection", connectionDetail)
            return
        }

        do {
            try await login(email: normalized, password: password)
        } catch let error as UserFacingError {
            authError = error.message
            return
        } catch {
            authError = L10n.t("err_register_owner")
            return
        }

        // El perfil inicial ya lo crea el backend con los datos de la administración: aquí se completa con lo que confirma el propietario
        let payload = ProfilePayload(userId: session.userId, name: fullName.trimmed,
                                     username: String(normalized.split(separator: "@").first ?? "residente"),
                                     phoneNumber: phone.trimmed, secondEmail: nil, photoUrl: nil)
        await upsertProfile(payload, knownProfileId: nil)
        session.displayName = fullName.trimmed
        registerStep = 1
        isAuthenticated = true
        refreshAll()
    }

    func signOut() async {
        try? await api.send("sessions/current", method: "DELETE", body: Optional<EmptyBody>.none)
        session.clear()
        isAuthenticated = false
        overview = nil
        statement = nil
        fee = nil
        profile = nil
        aiAnswer = nil
        selectedLineId = nil
        selectedTab = .consumption
    }

    /// Un 401 en cualquier carga significa que la sesión venció: se vuelve al inicio de sesión.
    private func handleLoadError(_ error: Error, fallbackKey: String) -> String {
        if let apiError = error as? APIError, apiError.statusCode == 401 {
            Task { await signOut() }
        }
        return L10n.t(fallbackKey)
    }

    // MARK: - Mi consumo

    func loadOverview() async {
        overviewLoading = true
        overviewError = nil
        defer { overviewLoading = false }
        do {
            let data: ResidentOverview = try await api.get("mobile/resident/overview")
            overview = data
            if let unit = data.unit {
                if let id = unit.id { session.unitId = id }
                if let name = unit.residentFullName, !name.isBlank { session.displayName = name }
            }
            if let id = data.condominium?.id { session.projectId = id }
        } catch {
            overviewError = handleLoadError(error, fallbackKey: "err_load_overview")
        }
    }

    func askAI(_ question: String) async {
        guard !question.isBlank, !aiLoading else { return }
        aiLoading = true
        aiError = nil
        defer { aiLoading = false }
        let language = Bundle.main.preferredLocalizations.first == "en" ? "en" : "es"
        do {
            aiAnswer = try await api.post("mobile/resident/ai-explain", body: AIQuestion(question: question.trimmed, billingPeriod: nil, language: language))
        } catch APIError.status(429) {
            aiError = L10n.t("err_ai_limit")
        } catch APIError.status {
            aiError = L10n.t("err_ai_no_answer")
        } catch {
            aiError = L10n.t("err_ai_error")
        }
    }

    // MARK: - Estado de cuenta y pagos con Stripe

    func loadStatement() async {
        statementLoading = true
        statementError = nil
        defer { statementLoading = false }
        do {
            statement = try await api.get("mobile/resident/account-statement")
        } catch {
            statementError = handleLoadError(error, fallbackKey: "err_load_statement")
        }
    }

    /// Inicia el pago de una cuota: abre Stripe Checkout dentro de la app y guarda la sesión para confirmarla al volver.
    func pay(statementId: Int) async {
        guard !paymentInProgress else { return }
        paymentInProgress = true
        paymentNotice = nil
        defer { paymentInProgress = false }
        do {
            let checkoutSession: CheckoutSession = try await api.post("payments/\(statementId)/checkout-session", body: EmptyBody())
            let paymentId = checkoutSession.paymentId ?? statementId
            if checkoutSession.status?.lowercased() == "paid" {
                session.pendingCheckout = nil
                showPaymentNotice(L10n.t("msg_payment_processed"))
                await reloadAfterPayment()
            } else if let link = checkoutSession.url ?? checkoutSession.checkoutUrl, let url = URL(string: link) {
                if let sessionId = checkoutSession.sessionId, !sessionId.isEmpty {
                    session.pendingCheckout = (paymentId, sessionId)
                }
                checkout = CheckoutPage(url: url)
            } else if let sessionId = checkoutSession.sessionId, !sessionId.isEmpty {
                session.pendingCheckout = (paymentId, sessionId)
                paymentInProgress = false
                await confirmPendingCheckout()
            } else {
                showPaymentNotice(L10n.t("msg_payment_started"))
                await loadStatement()
            }
        } catch APIError.status {
            showPaymentNotice(L10n.t("err_stripe_start"), isError: true)
        } catch {
            showPaymentNotice(L10n.t("err_payment_server"), isError: true)
        }
    }

    /// Stripe volvió a la URL de retorno: si trae el pago y la sesión, se usan para confirmar.
    func checkoutReturned(paymentId: Int?, sessionId: String?) {
        if let paymentId, paymentId > 0, let sessionId, !sessionId.isEmpty, !sessionId.contains("{") {
            session.pendingCheckout = (paymentId, sessionId)
        }
        checkout = nil
    }

    /// Confirma en el backend un pago de Stripe pendiente (al cerrar el pago o al volver a la app).
    func confirmPendingCheckout() async {
        guard isAuthenticated, !paymentInProgress, checkout == nil, let pending = session.pendingCheckout else { return }
        paymentInProgress = true
        defer { paymentInProgress = false }
        do {
            let result: ConfirmCheckout = try await api.post("payments/\(pending.paymentId)/checkout-session/confirm",
                                                              body: ConfirmCheckoutRequest(sessionId: pending.sessionId))
            let status = result.status?.lowercased()
            let stripeStatus = result.stripePaymentStatus?.lowercased()
            session.pendingCheckout = nil
            if status == "paid" || stripeStatus == "paid" {
                showPaymentNotice(L10n.t("msg_payment_confirmed"))
                await reloadAfterPayment()
            } else {
                showPaymentNotice(L10n.t("msg_payment_incomplete"), isError: true)
                await loadStatement()
            }
        } catch {
            session.pendingCheckout = nil
            showPaymentNotice(L10n.t("err_payment_verify"), isError: true)
            await loadStatement()
        }
    }

    private func reloadAfterPayment() async {
        let lineId = selectedLineId
        async let statementTask: Void = loadStatement()
        async let overviewTask: Void = loadOverview()
        async let feeTask: Void = loadFee(lineId)
        _ = await (statementTask, overviewTask, feeTask)
    }

    private func showPaymentNotice(_ message: String, isError: Bool = false) {
        paymentNotice = message
        paymentNoticeIsError = isError
    }

    // MARK: - Mi cuota

    func loadFee(_ lineId: Int?) async {
        selectedLineId = lineId
        feeLoading = true
        feeError = nil
        defer { feeLoading = false }
        var query: [URLQueryItem] = []
        if let lineId { query.append(URLQueryItem(name: "lineId", value: String(lineId))) }
        do {
            let data: FeeExplanationResponse = try await api.get("mobile/resident/fee-explanation", query: query)
            fee = data
            if selectedLineId == nil { selectedLineId = data.explanation?.lineId }
        } catch {
            feeError = handleLoadError(error, fallbackKey: "err_load_fee")
        }
    }

    /// "Ver desglose" desde Mi consumo: abre Mi cuota en esa liquidación.
    func openBreakdown(lineId: Int?) {
        selectedTab = .fee
        Task { await loadFee(lineId) }
    }

    // MARK: - Perfil

    func loadProfile() async {
        profileLoading = true
        profileError = nil
        defer { profileLoading = false }
        do {
            let data: ResidentProfile = try await api.get("mobile/resident/profile")
            profile = data
            if let name = data.fullName, !name.isBlank { session.displayName = name }
        } catch {
            profileError = handleLoadError(error, fallbackKey: "err_load_profile")
        }
    }

    func saveProfile(fullName: String, phone: String, secondEmail: String) async {
        guard fullName.trimmed.count >= 2 else { profileError = L10n.t("err_profile_name"); return }
        guard phone.trimmed.count >= 7 else { profileError = L10n.t("err_profile_phone"); return }
        profileSaving = true
        profileError = nil
        profileMessage = nil
        defer { profileSaving = false }

        let username = String(session.email.split(separator: "@").first ?? "residente")
        let payload = ProfilePayload(userId: session.userId, name: fullName.trimmed, username: username,
                                     phoneNumber: phone.trimmed,
                                     secondEmail: secondEmail.isBlank ? nil : secondEmail.trimmed,
                                     photoUrl: profile?.photoUrl)
        if await upsertProfile(payload, knownProfileId: profile?.profileId) {
            session.displayName = fullName.trimmed
            profileMessage = L10n.t("msg_profile_saved")
            await loadProfile()
            await loadStatement()
        } else {
            profileError = L10n.t("err_profile_save")
        }
    }

    /// Crea el perfil o, si ya existe (409), lo actualiza.
    @discardableResult
    private func upsertProfile(_ payload: ProfilePayload, knownProfileId: Int?) async -> Bool {
        do {
            if let id = knownProfileId, id > 0 {
                try await api.send("profiles/\(id)", method: "PUT", body: payload)
            } else {
                try await api.send("profiles", method: "POST", body: payload)
            }
            return true
        } catch APIError.status(409) {
            guard let existing: [RawProfile] = try? await api.get("profiles", query: [URLQueryItem(name: "userId", value: String(payload.userId))]),
                  let id = existing.first?.id, id > 0 else { return false }
            return (try? await api.send("profiles/\(id)", method: "PUT", body: payload)) != nil
        } catch {
            return false
        }
    }
}
