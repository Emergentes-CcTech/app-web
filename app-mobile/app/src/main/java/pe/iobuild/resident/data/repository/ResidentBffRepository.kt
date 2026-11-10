package pe.iobuild.resident.data.repository

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import pe.iobuild.resident.data.local.SessionManager
import pe.iobuild.resident.data.model.*
import pe.iobuild.resident.data.remote.ApiClientProvider

/**
 * Repositorio que implementa el patrón Backend for Frontend (BFF) para la App Android del Propietario.
 *
 * Estrategia de consulta:
 * 1. Invoca en 1 sola llamada los nuevos endpoints BFF del backend (.NET 9): `/api/v1/mobile/resident/...`
 * 2. Si el contenedor Docker actual aún no ha sido reconstruido (HTTP 404), ejecuta automáticamente
 *    la agregación BFF del lado del cliente contra los endpoints REST actuales (`/units`, `/condominiums`,
 *    `/payments`, `/settlement_lines`, `/profiles`), garantizando compatibilidad inmediata al 100%.
 */
class ResidentBffRepository(
    private val sessionManager: SessionManager,
    private val clientProvider: ApiClientProvider
) {
    private val api get() = clientProvider.getApi()

    private fun isResidentRole(role: String?): Boolean {
        val norm = role?.trim()?.lowercase() ?: ""
        return norm in listOf("resident", "residente", "owner", "propietario")
    }

    // ── 1. Autenticación exclusiva para Propietario / Residente ──

    suspend fun loginResident(email: String, password: String): Result<AuthResponse> = withContext(Dispatchers.IO) {
        try {
            val response = api.signIn(SignInRequest(email = email.trim().lowercase(), password = password))
            if (!response.isSuccessful || response.body() == null) {
                return@withContext Result.failure(Exception("Correo o contraseña incorrectos."))
            }
            val auth = response.body()!!
            if (!isResidentRole(auth.role)) {
                return@withContext Result.failure(
                    Exception("Esta aplicación móvil es exclusiva para propietarios de departamento. Para gestionar condominios, usa la consola web.")
                )
            }
            sessionManager.saveAuthSession(
                token = auth.token,
                userId = auth.id,
                email = auth.email,
                role = auth.role,
                projectId = auth.projectId,
                unitId = auth.unitId,
                unitNumber = auth.unitNumber,
                aliquotPercentage = auth.aliquotPercentage
            )
            Result.success(auth)
        } catch (e: Exception) {
            Result.failure(Exception("No se pudo conectar al servidor (${sessionManager.apiBaseUrl}). Verifica que el backend esté activo.", e))
        }
    }

    suspend fun checkInvitation(email: String): Result<InvitationCheckResponse> = withContext(Dispatchers.IO) {
        try {
            val res = api.checkInvitation(email.trim().lowercase())
            if (res.isSuccessful && res.body() != null) {
                Result.success(res.body()!!)
            } else {
                Result.failure(Exception("No se pudo verificar la invitación de la unidad."))
            }
        } catch (e: Exception) {
            Result.failure(Exception("Error de conexión al verificar el correo.", e))
        }
    }

    suspend fun registerResident(
        email: String,
        password: String,
        fullName: String,
        dni: String,
        phoneNumber: String
    ): Result<AuthResponse> = withContext(Dispatchers.IO) {
        try {
            val normalizedEmail = email.trim().lowercase()
            val signUpRes = api.signUp(SignUpRequest(email = normalizedEmail, password = password, role = "resident"))
            if (!signUpRes.isSuccessful) {
                val msg = when (signUpRes.code()) {
                    403 -> "Este correo no tiene un departamento asignado por la administración."
                    409 -> "Este correo electrónico ya está registrado. Inicia sesión."
                    else -> "No se pudo completar el registro."
                }
                return@withContext Result.failure(Exception(msg))
            }

            val loginRes = loginResident(normalizedEmail, password)
            val auth = loginRes.getOrElse { return@withContext Result.failure(it) }

            val payload = CreateProfilePayload(
                userId = auth.id,
                name = fullName.trim(),
                username = normalizedEmail.substringBefore("@"),
                phoneNumber = phoneNumber.trim()
            )
            val createProf = api.createProfile(payload)
            if (createProf.code() == 409) {
                val existing = api.getProfilesByUser(auth.id).body()?.firstOrNull()
                if (existing != null && existing.id > 0) {
                    api.updateProfile(existing.id, payload)
                }
            }
            sessionManager.displayName = fullName.trim()
            Result.success(auth)
        } catch (e: Exception) {
            Result.failure(Exception(e.message ?: "Error al registrar propietario.", e))
        }
    }

    suspend fun logout() = withContext(Dispatchers.IO) {
        try {
            api.signOut()
        } catch (_: Exception) {
        } finally {
            sessionManager.clearSession()
        }
    }

    // ── 2. BFF Overview ("Mi Consumo") ──

    suspend fun fetchOverview(): Result<ResidentOverviewResponse> = withContext(Dispatchers.IO) {
        try {
            val bffRes = api.getResidentOverview()
            if (bffRes.isSuccessful && bffRes.body() != null) {
                val body = bffRes.body()!!
                body.unit?.let {
                    sessionManager.unitId = it.id
                    sessionManager.unitNumber = it.unitNumber
                    sessionManager.aliquotPercentage = it.aliquotPercentage
                    if (!it.residentFullName.isNullOrBlank()) {
                        sessionManager.displayName = it.residentFullName
                    }
                }
                body.condominium?.let { sessionManager.projectId = it.id }
                return@withContext Result.success(body)
            }

            // Fallback Client-Side BFF si el endpoint /mobile/resident/overview devuelve 404
            val units = api.getUnitsByResident(sessionManager.userId).body().orEmpty()
            val myUnit = units.firstOrNull()
                ?: return@withContext Result.success(ResidentOverviewResponse(hasUnit = false))

            val condoId = myUnit.condominiumId ?: myUnit.projectId ?: 1
            val condo = api.getCondominiumById(condoId).body()
            val payments = api.getPaymentsByUnit(myUnit.id).body().orEmpty()
                .map { p ->
                    val eff = p.status.lowercase()
                    val isPaid = eff == "paid"
                    p.copy(
                        effectiveStatus = eff,
                        method = if (isPaid && !p.method.isNullOrBlank()) p.method else null,
                        reference = if (isPaid && !p.reference.isNullOrBlank()) p.reference else null
                    )
                }
                .sortedByDescending { it.period ?: "" }

            val unitSummary = ResidentUnitSummary(
                id = myUnit.id,
                unitNumber = myUnit.unitNumber ?: myUnit.number ?: "—",
                floor = myUnit.floor,
                areaM2 = myUnit.areaM2,
                aliquotPercentage = myUnit.aliquotPercentage ?: myUnit.participationPct ?: 0.0,
                residentFullName = myUnit.residentFullName
            )
            sessionManager.unitId = unitSummary.id
            sessionManager.unitNumber = unitSummary.unitNumber
            sessionManager.projectId = condoId
            sessionManager.aliquotPercentage = unitSummary.aliquotPercentage

            val areas = condo?.commonAreas.orEmpty().map { a ->
                CommonAreaConsumptionRow(
                    id = a.id,
                    name = a.name,
                    type = a.type.lowercase(),
                    subsystem = a.type,
                    location = a.location,
                    monitored = true,
                    metersOnline = 1,
                    metersTotal = 1,
                    waterM3 = 0.0,
                    energyKwh = 0.0,
                    waterSharePct = 0.0,
                    energySharePct = 0.0
                )
            }

            Result.success(
                ResidentOverviewResponse(
                    hasUnit = true,
                    unit = unitSummary,
                    condominium = condo?.let {
                        CondominiumSummary(
                            id = it.id,
                            name = it.name,
                            address = it.address,
                            district = it.district,
                            totalUnits = it.totalUnits
                        )
                    },
                    latestStatement = payments.firstOrNull(),
                    recentStatements = payments.take(3),
                    commonConsumption = CommonConsumptionSummary(
                        period = payments.firstOrNull()?.period ?: "",
                        waterM3 = 0.0,
                        energyKwh = 0.0,
                        previousWaterM3 = 0.0,
                        previousEnergyKwh = 0.0,
                        waterChangePct = null,
                        energyChangePct = null,
                        projectedWaterM3 = 0.0,
                        projectedEnergyKwh = 0.0,
                        monitoredAreasCount = areas.size,
                        totalAreasCount = areas.size,
                        areas = areas
                    )
                )
            )
        } catch (e: Exception) {
            Result.failure(Exception("No se pudo cargar el resumen de consumo.", e))
        }
    }

    // ── 3. BFF Account Statement ("Estado de Cuenta") & Confirmación de Pago Stripe ──

    suspend fun fetchAccountStatement(): Result<ResidentAccountStatementResponse> = withContext(Dispatchers.IO) {
        try {
            val bffRes = api.getResidentAccountStatement()
            if (bffRes.isSuccessful && bffRes.body() != null) {
                return@withContext Result.success(bffRes.body()!!)
            }

            // Fallback Client-Side BFF
            val units = api.getUnitsByResident(sessionManager.userId).body().orEmpty()
            val myUnit = units.firstOrNull()
                ?: return@withContext Result.success(ResidentAccountStatementResponse())
            val condoId = myUnit.condominiumId ?: myUnit.projectId ?: 1
            val condo = api.getCondominiumById(condoId).body()
            val history = api.getPaymentsByUnit(myUnit.id).body().orEmpty()
                .map { p ->
                    val eff = p.status.lowercase()
                    val isPaid = eff == "paid"
                    p.copy(
                        effectiveStatus = eff,
                        method = if (isPaid && !p.method.isNullOrBlank()) p.method else null,
                        reference = if (isPaid && !p.reference.isNullOrBlank()) p.reference else null
                    )
                }
                .sortedByDescending { it.period ?: "" }
            val pending = history.filter { it.effectiveStatus != "paid" }.minByOrNull { it.dueDate ?: "9999-99-99" }

            Result.success(
                ResidentAccountStatementResponse(
                    unit = AccountUnitFacts(
                        id = myUnit.id,
                        unitNumber = myUnit.unitNumber ?: myUnit.number ?: "—",
                        condominiumId = condoId,
                        condominiumName = condo?.name,
                        aliquotPercentage = myUnit.aliquotPercentage ?: myUnit.participationPct ?: 0.0,
                        holderName = myUnit.residentFullName ?: sessionManager.displayName.ifBlank { null },
                        floor = myUnit.floor,
                        areaM2 = myUnit.areaM2
                    ),
                    pendingStatement = pending,
                    history = history
                )
            )
        } catch (e: Exception) {
            Result.failure(Exception("No se pudo cargar tu estado de cuenta.", e))
        }
    }

    suspend fun payStatementWithStripe(paymentId: Int): Result<CheckoutSessionResponse> = withContext(Dispatchers.IO) {
        try {
            val res = api.createCheckoutSession(paymentId)
            if (res.isSuccessful && res.body() != null) {
                Result.success(res.body()!!)
            } else {
                Result.failure(Exception("No se pudo iniciar la sesión de pago en Stripe."))
            }
        } catch (e: Exception) {
            Result.failure(Exception("Error al procesar el pago con el servidor.", e))
        }
    }

    suspend fun confirmCheckoutSession(paymentId: Int, sessionId: String): Result<ConfirmCheckoutResponse> = withContext(Dispatchers.IO) {
        try {
            val res = api.confirmCheckoutSession(paymentId, ConfirmCheckoutRequest(sessionId = sessionId))
            if (res.isSuccessful && res.body() != null) {
                Result.success(res.body()!!)
            } else {
                Result.failure(Exception("No se pudo confirmar la sesión de pago en Stripe."))
            }
        } catch (e: Exception) {
            Result.failure(Exception("Error al confirmar el pago con el servidor.", e))
        }
    }

    // ── 4. BFF Fee Explanation ("Mi Cuota Actual" / Desglose en 3 pasos) ──

    suspend fun fetchFeeExplanation(lineId: Int? = null): Result<ResidentFeeExplanationResponse> = withContext(Dispatchers.IO) {
        try {
            val bffRes = api.getResidentFeeExplanation(lineId)
            if (bffRes.isSuccessful && bffRes.body() != null) {
                return@withContext Result.success(bffRes.body()!!)
            }

            // Fallback Client-Side BFF
            val units = api.getUnitsByResident(sessionManager.userId).body().orEmpty()
            val myUnit = units.firstOrNull()
                ?: return@withContext Result.success(ResidentFeeExplanationResponse())
            val lines = api.getSettlementLinesByUnit(myUnit.id).body().orEmpty()
                .sortedByDescending { it.period ?: "" }
            if (lines.isEmpty()) {
                return@withContext Result.success(ResidentFeeExplanationResponse())
            }
            val targetId = lineId ?: lines.first().id
            val exp = api.getLineExplanation(targetId).body()
            Result.success(
                ResidentFeeExplanationResponse(
                    availableLines = lines.map {
                        AvailableFeeLine(
                            lineId = it.id,
                            period = it.period,
                            amount = it.amount,
                            status = "pending"
                        )
                    },
                    explanation = exp
                )
            )
        } catch (e: Exception) {
            Result.failure(Exception("No se pudo cargar la explicación de tu cuota.", e))
        }
    }

    // ── 5. BFF Profile ("Mi Perfil") & Asistente IA ──

    suspend fun fetchProfile(): Result<ResidentProfileBffResponse> = withContext(Dispatchers.IO) {
        try {
            val bffRes = api.getResidentProfile()
            if (bffRes.isSuccessful && bffRes.body() != null) {
                val prof = bffRes.body()!!
                if (!prof.fullName.isNullOrBlank()) sessionManager.displayName = prof.fullName
                if (!prof.photoUrl.isNullOrBlank()) sessionManager.photoUrl = prof.photoUrl
                return@withContext Result.success(prof)
            }

            // Fallback Client-Side BFF
            val profiles = api.getProfilesByUser(sessionManager.userId).body().orEmpty()
            val rawProf = profiles.firstOrNull()
            val units = api.getUnitsByResident(sessionManager.userId).body().orEmpty()
            val myUnit = units.firstOrNull()
            val condoId = myUnit?.condominiumId ?: myUnit?.projectId ?: 1
            val condo = if (myUnit != null) api.getCondominiumById(condoId).body() else null

            val fullName = rawProf?.fullName ?: rawProf?.name ?: myUnit?.residentFullName
            if (!fullName.isNullOrBlank()) sessionManager.displayName = fullName

            Result.success(
                ResidentProfileBffResponse(
                    profileId = rawProf?.id,
                    userId = sessionManager.userId,
                    fullName = fullName,
                    email = sessionManager.email.ifBlank { null },
                    secondEmail = rawProf?.secondEmail,
                    phoneNumber = rawProf?.phoneNumber,
                    photoUrl = rawProf?.photoUrl,
                    role = "resident",
                    unit = myUnit?.let {
                        ProfileUnitBadge(
                            id = it.id,
                            unitNumber = it.unitNumber ?: it.number ?: "—",
                            condominiumName = condo?.name,
                            floor = it.floor,
                            areaM2 = it.areaM2,
                            aliquotPercentage = it.aliquotPercentage ?: it.participationPct ?: 0.0
                        )
                    },
                    sessions = null
                )
            )
        } catch (e: Exception) {
            Result.failure(Exception("No se pudo cargar tu perfil.", e))
        }
    }

    suspend fun updateProfile(
        profileId: Int?,
        fullName: String,
        phoneNumber: String,
        secondEmail: String,
        photoUrl: String?
    ): Result<ResidentProfileBffResponse> = withContext(Dispatchers.IO) {
        try {
            val payload = CreateProfilePayload(
                userId = sessionManager.userId,
                name = fullName.trim(),
                username = sessionManager.email.substringBefore("@").ifBlank { "residente" },
                phoneNumber = phoneNumber.trim(),
                secondEmail = secondEmail.trim().ifBlank { null },
                photoUrl = photoUrl?.trim()?.ifBlank { null }
            )
            if (profileId != null && profileId > 0) {
                api.updateProfile(profileId, payload)
            } else {
                api.createProfile(payload)
            }
            sessionManager.displayName = fullName.trim()
            if (!photoUrl.isNullOrBlank()) sessionManager.photoUrl = photoUrl
            fetchProfile()
        } catch (e: Exception) {
            Result.failure(Exception("No se pudo guardar los cambios del perfil.", e))
        }
    }

    suspend fun askAiAssistant(question: String): Result<AiExplanationResponse> = withContext(Dispatchers.IO) {
        try {
            val req = MobileAiExplainRequest(
                question = question,
                language = sessionManager.language,
                projectId = sessionManager.projectId,
                unitId = sessionManager.unitId.takeIf { it > 0 }
            )
            val bffRes = api.askMobileAi(req)
            if (bffRes.isSuccessful && bffRes.body() != null) {
                return@withContext Result.success(bffRes.body()!!)
            }
            if (bffRes.code() == 429) {
                return@withContext Result.failure(
                    Exception("Has alcanzado el límite de consultas por minuto al asistente IA. Intenta nuevamente en unos segundos.")
                )
            }
            val stdRes = api.askStandardAi(req)
            if (stdRes.isSuccessful && stdRes.body() != null) {
                return@withContext Result.success(stdRes.body()!!)
            }
            Result.failure(Exception("No se pudo obtener respuesta del asistente IoBuild."))
        } catch (e: Exception) {
            Result.failure(Exception("Error al consultar al asistente IoBuild.", e))
        }
    }
}
