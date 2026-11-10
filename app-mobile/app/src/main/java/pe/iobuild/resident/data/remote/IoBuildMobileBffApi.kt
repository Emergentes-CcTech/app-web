package pe.iobuild.resident.data.remote

import pe.iobuild.resident.data.model.*
import retrofit2.Response
import retrofit2.http.*

/**
 * Interfaz Retrofit que conecta la App Android con:
 * 1. Los endpoints Backend for Frontend (BFF) en `/api/v1/mobile/resident/...`
 * 2. Los endpoints de IAM, Pagos Stripe y Perfiles del mismo backend .NET 9
 * 3. Los endpoints REST granulares (como respaldo automático si el backend aún no fue reiniciado)
 */
interface IoBuildMobileBffApi {

    // ── Autenticación y Registro (IAM) ──

    @POST("sessions")
    suspend fun signIn(@Body request: SignInRequest): Response<AuthResponse>

    @DELETE("sessions/current")
    suspend fun signOut(): Response<Unit>

    @GET("authentication/invitation")
    suspend fun checkInvitation(@Query("email") email: String): Response<InvitationCheckResponse>

    @POST("users")
    suspend fun signUp(@Body request: SignUpRequest): Response<Map<String, Any?>>

    // ── Endpoints BFF Móvil del Propietario (/api/v1/mobile/resident/*) ──

    @GET("mobile/resident/overview")
    suspend fun getResidentOverview(): Response<ResidentOverviewResponse>

    @GET("mobile/resident/account-statement")
    suspend fun getResidentAccountStatement(): Response<ResidentAccountStatementResponse>

    @GET("mobile/resident/fee-explanation")
    suspend fun getResidentFeeExplanation(
        @Query("lineId") lineId: Int? = null
    ): Response<ResidentFeeExplanationResponse>

    @GET("mobile/resident/profile")
    suspend fun getResidentProfile(): Response<ResidentProfileBffResponse>

    @POST("mobile/resident/ai-explain")
    suspend fun askMobileAi(
        @Body request: MobileAiExplainRequest
    ): Response<AiExplanationResponse>

    // ── Acciones de Escritura (Pagos Stripe & Perfil) ──

    @POST("payments/{paymentId}/checkout-session")
    suspend fun createCheckoutSession(
        @Path("paymentId") paymentId: Int
    ): Response<CheckoutSessionResponse>

    @POST("payments/{paymentId}/checkout-session/confirm")
    suspend fun confirmCheckoutSession(
        @Path("paymentId") paymentId: Int,
        @Body request: ConfirmCheckoutRequest
    ): Response<ConfirmCheckoutResponse>

    @POST("profiles")
    suspend fun createProfile(
        @Body request: CreateProfilePayload
    ): Response<RawProfileDto>

    @PUT("profiles/{id}")
    suspend fun updateProfile(
        @Path("id") profileId: Int,
        @Body request: CreateProfilePayload
    ): Response<RawProfileDto>

    // ── Endpoints REST Granulares (Fallback Client-Side BFF) ──

    @GET("units")
    suspend fun getUnitsByResident(
        @Query("residentUserId") residentUserId: Int
    ): Response<List<RawUnitDto>>

    @GET("condominiums/{id}")
    suspend fun getCondominiumById(
        @Path("id") condominiumId: Int
    ): Response<RawCondominiumDto>

    @GET("payments")
    suspend fun getPaymentsByUnit(
        @Query("unitId") unitId: Int
    ): Response<List<StatementSummary>>

    @GET("settlement_lines")
    suspend fun getSettlementLinesByUnit(
        @Query("unitId") unitId: Int
    ): Response<List<RawSettlementLineDto>>

    @GET("settlement_lines/{id}/explanation")
    suspend fun getLineExplanation(
        @Path("id") lineId: Int
    ): Response<FeeExplanationDetail>

    @GET("profiles")
    suspend fun getProfilesByUser(
        @Query("userId") userId: Int
    ): Response<List<RawProfileDto>>

    @POST("analytics/ai/explain")
    suspend fun askStandardAi(
        @Body request: MobileAiExplainRequest
    ): Response<AiExplanationResponse>
}
