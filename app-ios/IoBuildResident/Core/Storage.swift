import Foundation
import Security

enum AppConfig {
    /// Servidor de producción. En compilaciones Debug se puede cambiar desde la pantalla de inicio de sesión.
    static let productionBaseURL = "https://iobuild.me/api/v1/"
}

/// Guarda valores sensibles (el JWT) en el Llavero de iOS, solo en este dispositivo.
enum KeychainStore {
    private static let service = "pe.iobuild.resident"

    static func set(_ value: String?, for key: String) {
        let base: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: key,
        ]
        SecItemDelete(base as CFDictionary)
        guard let value, let data = value.data(using: .utf8) else { return }
        var item = base
        item[kSecValueData as String] = data
        item[kSecAttrAccessible as String] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
        SecItemAdd(item as CFDictionary, nil)
    }

    static func get(_ key: String) -> String? {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: key,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne,
        ]
        var result: AnyObject?
        guard SecItemCopyMatching(query as CFDictionary, &result) == errSecSuccess,
              let data = result as? Data else { return nil }
        return String(data: data, encoding: .utf8)
    }
}

/// Sesión del propietario: el token va en el Llavero; los datos no sensibles, en UserDefaults.
final class SessionStore {
    private let defaults = UserDefaults.standard

    private enum Key {
        static let token = "jwt_token"
        static let userId = "user_id"
        static let email = "user_email"
        static let role = "user_role"
        static let displayName = "user_name"
        static let unitId = "unit_id"
        static let projectId = "project_id"
        static let baseURL = "api_base_url"
        static let pendingPaymentId = "pending_checkout_payment_id"
        static let pendingSessionId = "pending_checkout_session_id"
    }

    var token: String? {
        get { KeychainStore.get(Key.token) }
        set { KeychainStore.set(newValue, for: Key.token) }
    }

    var userId: Int {
        get { defaults.integer(forKey: Key.userId) }
        set { defaults.set(newValue, forKey: Key.userId) }
    }

    var email: String {
        get { defaults.string(forKey: Key.email) ?? "" }
        set { defaults.set(newValue, forKey: Key.email) }
    }

    var displayName: String {
        get { defaults.string(forKey: Key.displayName) ?? "" }
        set { defaults.set(newValue, forKey: Key.displayName) }
    }

    var unitId: Int {
        get { defaults.integer(forKey: Key.unitId) }
        set { defaults.set(newValue, forKey: Key.unitId) }
    }

    var projectId: Int {
        get { defaults.integer(forKey: Key.projectId) }
        set { defaults.set(newValue, forKey: Key.projectId) }
    }

    /// URL base del API. Las compilaciones Release usan siempre producción.
    var baseURL: String {
        get {
            #if DEBUG
            if let custom = defaults.string(forKey: Key.baseURL), !custom.isEmpty {
                return Self.normalized(custom)
            }
            #endif
            return AppConfig.productionBaseURL
        }
        set { defaults.set(Self.normalized(newValue), forKey: Key.baseURL) }
    }

    var isUsingCustomServer: Bool { baseURL != AppConfig.productionBaseURL }

    func resetServer() { defaults.removeObject(forKey: Key.baseURL) }

    var isAuthenticated: Bool { !(token ?? "").isEmpty && userId > 0 }

    /// Pago de Stripe abierto y aún sin confirmar en el backend (se confirma al volver a la app).
    var pendingCheckout: (paymentId: Int, sessionId: String)? {
        get {
            let paymentId = defaults.integer(forKey: Key.pendingPaymentId)
            guard paymentId > 0, let sessionId = defaults.string(forKey: Key.pendingSessionId), !sessionId.isEmpty else { return nil }
            return (paymentId, sessionId)
        }
        set {
            if let newValue {
                defaults.set(newValue.paymentId, forKey: Key.pendingPaymentId)
                defaults.set(newValue.sessionId, forKey: Key.pendingSessionId)
            } else {
                defaults.removeObject(forKey: Key.pendingPaymentId)
                defaults.removeObject(forKey: Key.pendingSessionId)
            }
        }
    }

    func saveAuth(_ auth: AuthResponse) {
        token = auth.token
        userId = auth.id
        email = auth.email ?? ""
        defaults.set(auth.role ?? "", forKey: Key.role)
        if let projectId = auth.projectId { self.projectId = projectId }
        if let unitId = auth.unitId { self.unitId = unitId }
    }

    func clear() {
        token = nil
        pendingCheckout = nil
        for key in [Key.userId, Key.email, Key.role, Key.displayName, Key.unitId, Key.projectId] {
            defaults.removeObject(forKey: key)
        }
    }

    static func normalized(_ url: String) -> String {
        let trimmed = url.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.hasSuffix("/") ? trimmed : trimmed + "/"
    }
}
