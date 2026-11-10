package pe.iobuild.resident.data.model

// ── 1. Autenticación y Registro (IAM + Profiles) ──

data class SignInRequest(
    val email: String,
    val password: String
)

data class AuthResponse(
    val id: Int,
    val email: String,
    val role: String,
    val token: String,
    val projectId: Int? = null,
    val unitId: Int? = null,
    val unitNumber: String? = null,
    val aliquotPercentage: Double? = null
)

data class SignUpRequest(
    val email: String,
    val password: String,
    val role: String = "resident"
)

data class InvitationCheckResponse(
    val assigned: Boolean = false,
    val alreadyRegistered: Boolean = false,
    val units: List<InvitedUnitInfo>? = emptyList()
)

data class InvitedUnitInfo(
    val unitNumber: String? = null,
    val condominiumName: String? = null
)

data class CreateProfilePayload(
    val userId: Int,
    val name: String,
    val username: String,
    val phoneNumber: String? = null,
    val secondEmail: String? = null,
    val photoUrl: String? = null
)

// ── 2. BFF Overview ("Mi Consumo" / Inicio del Propietario) ──

data class ResidentOverviewResponse(
    val hasUnit: Boolean = true,
    val unit: ResidentUnitSummary? = null,
    val condominium: CondominiumSummary? = null,
    val latestStatement: StatementSummary? = null,
    val recentStatements: List<StatementSummary> = emptyList(),
    val commonConsumption: CommonConsumptionSummary? = null
)

data class ResidentUnitSummary(
    val id: Int = 0,
    val unitNumber: String = "—",
    val floor: Int? = null,
    val areaM2: Double? = null,
    val aliquotPercentage: Double = 0.0,
    val residentFullName: String? = null
)

data class CondominiumSummary(
    val id: Int = 0,
    val name: String = "—",
    val address: String? = null,
    val district: String? = null,
    val totalUnits: Int = 0
)

data class StatementSummary(
    val id: Int = 0,
    val settlementLineId: Int = 0,
    val unitId: Int = 0,
    val condominiumId: Int = 0,
    val period: String? = null,
    val concept: String? = "Cuota de áreas comunes",
    val amount: Double = 0.0,
    val dueDate: String? = null,
    val paidAt: String? = null,
    val status: String = "pending",
    val effectiveStatus: String = "pending",
    val daysOverdue: Int = 0,
    val method: String? = null,
    val reference: String? = null
)

data class CommonConsumptionSummary(
    val period: String = "",
    val waterM3: Double = 0.0,
    val energyKwh: Double = 0.0,
    val previousWaterM3: Double = 0.0,
    val previousEnergyKwh: Double = 0.0,
    val waterChangePct: Double? = null,
    val energyChangePct: Double? = null,
    val projectedWaterM3: Double = 0.0,
    val projectedEnergyKwh: Double = 0.0,
    val monitoredAreasCount: Int = 0,
    val totalAreasCount: Int = 0,
    val areas: List<CommonAreaConsumptionRow> = emptyList(),
    val dailySeries: List<DailyConsumptionPoint> = emptyList()
)

data class CommonAreaConsumptionRow(
    val id: Int = 0,
    val name: String = "",
    val type: String = "cistern",
    val subsystem: String? = null,
    val location: String? = null,
    val monitored: Boolean = false,
    val metersOnline: Int = 0,
    val metersTotal: Int = 0,
    val waterM3: Double = 0.0,
    val energyKwh: Double = 0.0,
    val waterSharePct: Double = 0.0,
    val energySharePct: Double = 0.0,
    val lastReadingAt: String? = null
)

data class DailyConsumptionPoint(
    val date: String = "",
    val dayLabel: String = "",
    val waterM3: Double = 0.0,
    val energyKwh: Double = 0.0
)

// ── 3. BFF Account Statement ("Estado de Cuenta") ──

data class ResidentAccountStatementResponse(
    val unit: AccountUnitFacts? = null,
    val pendingStatement: StatementSummary? = null,
    val history: List<StatementSummary> = emptyList()
)

data class AccountUnitFacts(
    val id: Int = 0,
    val unitNumber: String = "—",
    val condominiumId: Int = 0,
    val condominiumName: String? = null,
    val aliquotPercentage: Double = 0.0,
    val holderName: String? = null,
    val floor: Int? = null,
    val areaM2: Double? = null
)

data class CheckoutSessionResponse(
    val url: String? = null,
    val checkoutUrl: String? = null,
    val sessionId: String? = null,
    val paymentId: Int? = null,
    val amount: Double? = null,
    val status: String? = null
)

data class ConfirmCheckoutRequest(
    val sessionId: String
)

data class ConfirmCheckoutResponse(
    val paymentId: Int? = null,
    val status: String? = null,
    val stripePaymentStatus: String? = null
)

// ── 4. BFF Fee Explanation ("Mi Cuota Actual" / Desglose en 3 pasos) ──

data class ResidentFeeExplanationResponse(
    val availableLines: List<AvailableFeeLine> = emptyList(),
    val explanation: FeeExplanationDetail? = null
)

data class AvailableFeeLine(
    val lineId: Int = 0,
    val period: String? = null,
    val amount: Double = 0.0,
    val status: String = "pending"
)

data class FeeExplanationDetail(
    val lineId: Int = 0,
    val unitNumber: String = "—",
    val period: String = "",
    val previousPeriod: String? = null,
    val status: String = "pending",
    val dueDate: String? = null,
    val paidAt: String? = null,
    val balance: Double = 0.0,
    val aliquot: Double = 0.0,
    val common: CommonExpenseBreakdown = CommonExpenseBreakdown(),
    val mine: IndividualExpenseBreakdown = IndividualExpenseBreakdown(),
    val previous: PreviousStatementComparison? = null,
    val alerts: List<OvercostAlertItem> = emptyList()
)

data class CommonExpenseBreakdown(
    val water: Double = 0.0,
    val energy: Double = 0.0,
    val other: Double = 0.0,
    val total: Double = 0.0,
    val waterM3: Double = 0.0,
    val energyKwh: Double = 0.0,
    val waterReceiptUrl: String? = null,
    val energyReceiptUrl: String? = null
)

data class IndividualExpenseBreakdown(
    val water: Double = 0.0,
    val energy: Double = 0.0,
    val other: Double = 0.0,
    val total: Double = 0.0
)

data class PreviousStatementComparison(
    val waterSoles: Double = 0.0,
    val energySoles: Double = 0.0,
    val otherSoles: Double = 0.0,
    val totalSoles: Double = 0.0
)

data class OvercostAlertItem(
    val alertType: String = "",
    val title: String? = null,
    val detectedAt: String? = null,
    val isOpen: Boolean = false,
    val overcost: Double = 0.0,
    val myShare: Double = 0.0
)

// ── 5. BFF Profile ("Mi Perfil") & AI Assistant ──

data class ResidentProfileBffResponse(
    val profileId: Int? = null,
    val userId: Int = 0,
    val fullName: String? = null,
    val email: String? = null,
    val secondEmail: String? = null,
    val phoneNumber: String? = null,
    val photoUrl: String? = null,
    val role: String = "resident",
    val unit: ProfileUnitBadge? = null,
    val sessions: List<ActiveSessionItem>? = null
)

data class ProfileUnitBadge(
    val id: Int = 0,
    val unitNumber: String = "—",
    val condominiumName: String? = null,
    val floor: Int? = null,
    val areaM2: Double? = null,
    val aliquotPercentage: Double = 0.0
)

data class ActiveSessionItem(
    val id: Int = 1,
    val device: String? = null,
    val ipAddress: String? = null,
    val lastActive: String? = null,
    val isCurrent: Boolean = true
)

data class MobileAiExplainRequest(
    val question: String,
    val billingPeriod: String? = null,
    val language: String = "es",
    val projectId: Int? = null,
    val unitId: Int? = null
)

data class AiExplanationResponse(
    val period: String? = null,
    val summary: String = "",
    val waterAnalysis: String = "",
    val energyAnalysis: String = "",
    val aliquotImpact: String = "",
    val recommendations: List<String>? = emptyList(),
    val source: String? = "rules"
)

// ── 6. Modelos granulares para Fallback Client-Side BFF ──

data class RawUnitDto(
    val id: Int = 0,
    val projectId: Int? = null,
    val condominiumId: Int? = null,
    val unitNumber: String? = null,
    val number: String? = null,
    val floor: Int? = null,
    val areaM2: Double? = null,
    val aliquotPercentage: Double? = null,
    val participationPct: Double? = null,
    val residentFullName: String? = null
)

data class RawCondominiumDto(
    val id: Int = 0,
    val name: String = "—",
    val address: String? = null,
    val district: String? = null,
    val totalUnits: Int = 0,
    val commonAreas: List<RawCommonAreaDto>? = emptyList()
)

data class RawCommonAreaDto(
    val id: Int = 0,
    val name: String = "",
    val type: String = "cistern",
    val location: String? = null
)

data class RawProfileDto(
    val id: Int = 0,
    val userId: Int = 0,
    val name: String? = null,
    val fullName: String? = null,
    val phoneNumber: String? = null,
    val secondEmail: String? = null,
    val photoUrl: String? = null
)

data class RawSettlementLineDto(
    val id: Int = 0,
    val settlementId: Int = 0,
    val unitId: Int = 0,
    val period: String? = null,
    val participationPct: Double = 0.0,
    val amount: Double = 0.0
)
