import Foundation

// Contratos del backend IoBuild. Todo campo que el servidor podría omitir es opcional,
// para que un dato faltante nunca impida mostrar la pantalla.

// MARK: - Autenticación y registro

struct SignInRequest: Encodable {
    let email: String
    let password: String
}

struct AuthResponse: Decodable {
    let id: Int
    let email: String?
    let role: String?
    let token: String
    let projectId: Int?
    let unitId: Int?
    let unitNumber: String?
    let aliquotPercentage: Double?
}

struct SignUpRequest: Encodable {
    let email: String
    let password: String
    let role: String
}

struct InvitationCheck: Decodable {
    let assigned: Bool?
    let alreadyRegistered: Bool?
    let units: [InvitedUnit]?
}

struct InvitedUnit: Decodable, Hashable {
    let unitNumber: String?
    let condominiumName: String?
}

struct ProfilePayload: Encodable {
    let userId: Int
    let name: String
    let username: String
    let phoneNumber: String?
    let secondEmail: String?
    let photoUrl: String?
}

struct RawProfile: Decodable {
    let id: Int?
}

// MARK: - Mi consumo (overview)

struct ResidentOverview: Decodable {
    let hasUnit: Bool?
    let unit: UnitSummary?
    let condominium: CondominiumSummary?
    let latestStatement: Statement?
    let recentStatements: [Statement]?
    let commonConsumption: CommonConsumption?
}

struct UnitSummary: Decodable {
    let id: Int?
    let unitNumber: String?
    let floor: Int?
    let areaM2: Double?
    let aliquotPercentage: Double?
    let residentFullName: String?
}

struct CondominiumSummary: Decodable {
    let id: Int?
    let name: String?
    let address: String?
    let district: String?
    let totalUnits: Int?
}

struct Statement: Decodable, Identifiable, Hashable {
    let id: Int
    let settlementLineId: Int?
    let unitId: Int?
    let condominiumId: Int?
    let period: String?
    let concept: String?
    let amount: Double?
    let dueDate: String?
    let paidAt: String?
    let status: String?
    let effectiveStatus: String?
    let daysOverdue: Int?
    let method: String?
    let reference: String?

    var normalizedStatus: String { (effectiveStatus ?? status ?? "pending").lowercased() }
    var isPaid: Bool { normalizedStatus == "paid" }
    var lineId: Int { (settlementLineId ?? 0) > 0 ? settlementLineId! : id }
}

struct CommonConsumption: Decodable {
    let period: String?
    let waterM3: Double?
    let energyKwh: Double?
    let previousWaterM3: Double?
    let previousEnergyKwh: Double?
    let waterChangePct: Double?
    let energyChangePct: Double?
    let projectedWaterM3: Double?
    let projectedEnergyKwh: Double?
    let monitoredAreasCount: Int?
    let totalAreasCount: Int?
    let areas: [AreaConsumption]?
    let dailySeries: [DailyPoint]?
}

struct AreaConsumption: Decodable, Identifiable {
    let id: Int
    let name: String?
    let type: String?
    let subsystem: String?
    let location: String?
    let monitored: Bool?
    let metersOnline: Int?
    let metersTotal: Int?
    let waterM3: Double?
    let energyKwh: Double?
    let waterSharePct: Double?
    let energySharePct: Double?
    let lastReadingAt: String?
}

struct DailyPoint: Decodable, Identifiable {
    let date: String
    let dayLabel: String?
    let waterM3: Double?
    let energyKwh: Double?
    var id: String { date }
}

// MARK: - Estado de cuenta y pagos

struct AccountStatement: Decodable {
    let unit: AccountUnit?
    let pendingStatement: Statement?
    let history: [Statement]?
}

struct AccountUnit: Decodable {
    let id: Int?
    let unitNumber: String?
    let condominiumId: Int?
    let condominiumName: String?
    let aliquotPercentage: Double?
    let holderName: String?
    let floor: Int?
    let areaM2: Double?
}

struct CheckoutSession: Decodable {
    let url: String?
    let checkoutUrl: String?
    let sessionId: String?
    let paymentId: Int?
    let amount: Double?
    let status: String?
}

struct ConfirmCheckoutRequest: Encodable {
    let sessionId: String
}

struct ConfirmCheckout: Decodable {
    let paymentId: Int?
    let status: String?
    let stripePaymentStatus: String?
}

// MARK: - Mi cuota (explicación)

struct FeeExplanationResponse: Decodable {
    let availableLines: [FeeLine]?
    let explanation: FeeExplanation?
}

struct FeeLine: Decodable, Identifiable, Hashable {
    let lineId: Int
    let period: String?
    let amount: Double?
    let status: String?
    var id: Int { lineId }
}

struct FeeExplanation: Decodable {
    let lineId: Int?
    let unitNumber: String?
    let period: String?
    let previousPeriod: String?
    let status: String?
    let dueDate: String?
    let paidAt: String?
    let balance: Double?
    let aliquot: Double?
    let common: CommonExpenses?
    let mine: MyExpenses?
    let previous: PreviousStatement?
    let alerts: [OvercostAlert]?
}

struct CommonExpenses: Decodable {
    let water: Double?
    let energy: Double?
    let other: Double?
    let total: Double?
    let waterM3: Double?
    let energyKwh: Double?
    let waterReceiptUrl: String?
    let energyReceiptUrl: String?
}

struct MyExpenses: Decodable {
    let water: Double?
    let energy: Double?
    let other: Double?
    let total: Double?
}

struct PreviousStatement: Decodable {
    let waterSoles: Double?
    let energySoles: Double?
    let otherSoles: Double?
    let totalSoles: Double?
}

struct OvercostAlert: Decodable, Hashable {
    let alertType: String?
    let title: String?
    let detectedAt: String?
    let isOpen: Bool?
    let overcost: Double?
    let myShare: Double?
}

// MARK: - Perfil y asistente IA

struct ResidentProfile: Decodable {
    let profileId: Int?
    let userId: Int?
    let fullName: String?
    let email: String?
    let secondEmail: String?
    let phoneNumber: String?
    let photoUrl: String?
    let role: String?
    let unit: ProfileUnit?
}

struct ProfileUnit: Decodable {
    let id: Int?
    let unitNumber: String?
    let condominiumName: String?
    let floor: Int?
    let areaM2: Double?
    let aliquotPercentage: Double?
}

struct AIQuestion: Encodable {
    let question: String
    let billingPeriod: String?
    let language: String
}

struct AIExplanation: Decodable {
    let period: String?
    let summary: String?
    let waterAnalysis: String?
    let energyAnalysis: String?
    let aliquotImpact: String?
    let recommendations: [String]?
    let source: String?
}
