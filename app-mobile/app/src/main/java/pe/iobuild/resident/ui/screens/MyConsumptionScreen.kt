package pe.iobuild.resident.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import pe.iobuild.resident.data.model.CommonAreaConsumptionRow
import pe.iobuild.resident.data.model.StatementSummary
import pe.iobuild.resident.ui.theme.*
import pe.iobuild.resident.ui.util.FormatUtils
import pe.iobuild.resident.ui.viewmodel.ResidentViewModel

/**
 * Vista 1 del Propietario: "Mi Consumo" (equivalente a `resident-dashboard.component.vue`).
 * Consume el endpoint BFF `/api/v1/mobile/resident/overview` en 1 sola llamada:
 * - Hero card con la última liquidación y acceso en 1 clic al desglose (RNF-006).
 * - Reporte de consumo de áreas comunes del condominio (Agua m³, Energía kWh, Proyección y Áreas monitoreadas).
 * - Barras comparativas por área común.
 * - Asistente IA ("Pregúntale a IoBuild") integrado con chips de sugerencias para el residente.
 * - Últimas 3 liquidaciones con acceso directo al Estado de Cuenta.
 */
@Composable
fun MyConsumptionScreen(
    viewModel: ResidentViewModel,
    onOpenBreakdown: (Int?) -> Unit,
    onNavigateToAccountStatement: () -> Unit
) {
    val overview = viewModel.overviewData
    var selectedMetric by remember { mutableStateOf("water") } // "water" | "energy"
    var aiQuestionInput by remember { mutableStateOf("") }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // Encabezado con título y departamento del propietario
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column {
                Text(
                    text = "Mi consumo",
                    fontSize = 24.sp,
                    fontWeight = FontWeight.ExtraBold,
                    color = IoBuildSlate900
                )
                overview?.unit?.let { u ->
                    Text(
                        text = "Departamento ${u.unitNumber} · Alícuota ${FormatUtils.formatAliquot(u.aliquotPercentage)}",
                        fontSize = 13.sp,
                        color = IoBuildSlate500
                    )
                }
            }
            IconButton(onClick = { viewModel.loadOverview() }) {
                Icon(Icons.Default.Refresh, contentDescription = "Actualizar", tint = IoBuildEmerald600)
            }
        }

        if (viewModel.overviewLoading && overview == null) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(vertical = 48.dp),
                contentAlignment = Alignment.Center
            ) {
                CircularProgressIndicator(color = IoBuildEmerald600)
            }
            return@Column
        }

        if (!viewModel.overviewError.isNullOrBlank() && overview == null) {
            ErrorBanner(
                message = viewModel.overviewError ?: "No se pudo cargar la información.",
                onRetry = { viewModel.loadOverview() }
            )
            return@Column
        }

        if (overview != null && !overview.hasUnit) {
            Card(
                colors = CardDefaults.cardColors(containerColor = Color.White),
                shape = RoundedCornerShape(16.dp),
                modifier = Modifier.fillMaxWidth()
            ) {
                Text(
                    text = "Tu cuenta aún no está vinculada a un departamento. Pide a la administración tu registro en el padrón.",
                    modifier = Modifier.padding(20.dp),
                    color = IoBuildSlate700
                )
            }
            return@Column
        }

        // ── 1. Hero Card: Mi Liquidación Actual + Botón "Ver desglose" (RNF-006) ──
        val latest = overview?.latestStatement
        val isPaid = latest?.effectiveStatus?.lowercase() == "paid"
        val isOverdue = latest?.effectiveStatus?.lowercase() == "overdue"
        val heroBg = when {
            isPaid -> IoBuildEmerald50
            isOverdue -> IoBuildRed50
            else -> Color.White
        }
        val heroBorder = when {
            isPaid -> IoBuildEmerald500
            isOverdue -> IoBuildRed500
            else -> IoBuildSlate200
        }

        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = heroBg),
            border = androidx.compose.foundation.BorderStroke(1.5.dp, heroBorder)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(18.dp),
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                if (latest != null) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = "Mi liquidación de ${FormatUtils.formatPeriod(latest.period)}",
                            fontSize = 13.sp,
                            fontWeight = FontWeight.Medium,
                            color = IoBuildSlate700
                        )
                        StatusBadge(status = latest.effectiveStatus)
                    }

                    Text(
                        text = FormatUtils.formatPen(latest.amount),
                        fontSize = 32.sp,
                        fontWeight = FontWeight.ExtraBold,
                        color = IoBuildSlate900
                    )

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = if (isPaid && !latest.paidAt.isNullOrBlank()) {
                                "Pagada el ${latest.paidAt}"
                            } else {
                                "Vence: ${latest.dueDate ?: "—"}"
                            },
                            fontSize = 12.sp,
                            color = IoBuildSlate500
                        )
                        Button(
                            onClick = { onOpenBreakdown(latest.settlementLineId.takeIf { it > 0 } ?: latest.id) },
                            colors = ButtonDefaults.buttonColors(containerColor = IoBuildEmerald600),
                            shape = RoundedCornerShape(10.dp)
                        ) {
                            Icon(Icons.Default.Search, contentDescription = null, modifier = Modifier.size(16.dp))
                            Spacer(Modifier.width(6.dp))
                            Text("Ver desglose", fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
                        }
                    }
                } else {
                    Text(
                        text = "Aún no tienes liquidaciones emitidas.",
                        color = IoBuildSlate500,
                        fontSize = 14.sp
                    )
                }
            }
        }

        // ── 2. Reporte de Consumo de Áreas Comunes del Condominio ──
        val consumption = overview?.commonConsumption
        if (consumption != null) {
            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = "Consumo de áreas comunes este mes",
                            fontSize = 18.sp,
                            fontWeight = FontWeight.Bold,
                            color = IoBuildSlate900
                        )
                        Text(
                            text = "${overview.condominium?.name ?: "Condominio"} · ${FormatUtils.formatPeriod(consumption.period)}",
                            fontSize = 12.sp,
                            color = IoBuildSlate500
                        )
                    }

                    // Selector Agua / Energía
                    Row(
                        modifier = Modifier
                            .clip(RoundedCornerShape(10.dp))
                            .background(IoBuildSlate200.copy(alpha = 0.6f))
                            .padding(2.dp)
                    ) {
                        FilterChipButton(
                            label = "Agua",
                            selected = selectedMetric == "water",
                            onClick = { selectedMetric = "water" }
                        )
                        FilterChipButton(
                            label = "Energía",
                            selected = selectedMetric == "energy",
                            onClick = { selectedMetric = "energy" }
                        )
                    }
                }

                // 4 Tarjetas KPI de Áreas Comunes (2x2 Grid móvil)
                val waterChangeStr = consumption.waterChangePct?.let {
                    "${if (it > 0) "+" else ""}$it% vs mes ant."
                } ?: "Sin mes previo"

                val energyChangeStr = consumption.energyChangePct?.let {
                    "${if (it > 0) "+" else ""}$it% vs mes ant."
                } ?: "Sin mes previo"

                val projValue = if (selectedMetric == "water") {
                    "${consumption.projectedWaterM3} m³"
                } else {
                    "${consumption.projectedEnergyKwh} kWh"
                }

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    KpiStatCard(
                        modifier = Modifier.weight(1f),
                        title = "Agua común",
                        value = "${consumption.waterM3} m³",
                        subtitle = waterChangeStr,
                        icon = Icons.Default.WaterDrop,
                        iconTint = IoBuildEmerald600,
                        iconBg = IoBuildEmerald50
                    )
                    KpiStatCard(
                        modifier = Modifier.weight(1f),
                        title = "Energía común",
                        value = "${consumption.energyKwh} kWh",
                        subtitle = energyChangeStr,
                        icon = Icons.Default.Bolt,
                        iconTint = IoBuildAmber500,
                        iconBg = IoBuildAmber50
                    )
                }

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    KpiStatCard(
                        modifier = Modifier.weight(1f),
                        title = "Proyección cierre",
                        value = projValue,
                        subtitle = "Si el ritmo sigue igual",
                        icon = Icons.Default.TrendingUp,
                        iconTint = IoBuildTeal500,
                        iconBg = IoBuildEmerald50
                    )
                    KpiStatCard(
                        modifier = Modifier.weight(1f),
                        title = "Áreas monitoreadas",
                        value = "${consumption.monitoredAreasCount} / ${consumption.totalAreasCount}",
                        subtitle = "Con medidor IoT activo",
                        icon = Icons.Default.Sensors,
                        iconTint = IoBuildEmerald700,
                        iconBg = IoBuildEmerald50
                    )
                }

                // Desglose visual por Área Común (Barras horizontales + Estado del medidor)
                if (consumption.areas.isNotEmpty()) {
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(14.dp),
                        colors = CardDefaults.cardColors(containerColor = Color.White),
                        border = androidx.compose.foundation.BorderStroke(1.dp, IoBuildSlate200)
                    ) {
                        Column(
                            modifier = Modifier.padding(16.dp),
                            verticalArrangement = Arrangement.spacedBy(12.dp)
                        ) {
                            Text(
                                text = "Detalle por área común (${if (selectedMetric == "water") "m³" else "kWh"})",
                                fontWeight = FontWeight.Bold,
                                fontSize = 15.sp,
                                color = IoBuildSlate900
                            )

                            consumption.areas.forEach { area ->
                                CommonAreaItemRow(area = area, metric = selectedMetric)
                                HorizontalDivider(color = IoBuildSlate100)
                            }

                            Text(
                                text = "Estos consumos de los sensores IoT de áreas comunes sustentan el prorrateo de tu departamento.",
                                fontSize = 11.sp,
                                color = IoBuildSlate500
                            )
                        }
                    }
                }
            }
        }

        // ── 3. Asistente Cognitivo "Pregúntale a IoBuild" (Audiencia: Residente) ──
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = Color.White),
            border = androidx.compose.foundation.BorderStroke(1.dp, IoBuildEmerald500.copy(alpha = 0.35f))
        ) {
            Column(
                modifier = Modifier.padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        imageVector = Icons.Default.AutoAwesome,
                        contentDescription = null,
                        tint = IoBuildEmerald600,
                        modifier = Modifier.size(20.dp)
                    )
                    Spacer(Modifier.width(8.dp))
                    Text(
                        text = "Pregúntale a IoBuild",
                        fontWeight = FontWeight.Bold,
                        fontSize = 16.sp,
                        color = IoBuildSlate900
                    )
                }
                Text(
                    text = "Explica la última cuota emitida con lenguaje claro a partir de las mediciones y alertas del edificio.",
                    fontSize = 12.sp,
                    color = IoBuildSlate500
                )

                // Chips de preguntas sugeridas para el propietario
                val residentSuggestions = listOf(
                    "¿Por qué varió mi cuota este mes?",
                    "¿Cómo se calcula mi parte del gasto común?",
                    "¿Hubo alguna fuga en el edificio?"
                )
                Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    residentSuggestions.forEach { suggestion ->
                        SuggestionChip(
                            onClick = {
                                aiQuestionInput = suggestion
                                viewModel.askAi(suggestion)
                            },
                            label = { Text(suggestion, fontSize = 12.sp) },
                            colors = SuggestionChipDefaults.suggestionChipColors(
                                containerColor = IoBuildEmerald50,
                                labelColor = IoBuildEmerald700
                            )
                        )
                    }
                }

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    OutlinedTextField(
                        value = aiQuestionInput,
                        onValueChange = { aiQuestionInput = it },
                        placeholder = { Text("Escribe tu pregunta sobre el consumo...", fontSize = 12.sp) },
                        singleLine = true,
                        modifier = Modifier.weight(1f),
                        shape = RoundedCornerShape(10.dp)
                    )
                    Button(
                        onClick = { viewModel.askAi(aiQuestionInput) },
                        enabled = !viewModel.aiLoading && aiQuestionInput.isNotBlank(),
                        colors = ButtonDefaults.buttonColors(containerColor = IoBuildEmerald600),
                        shape = RoundedCornerShape(10.dp)
                    ) {
                        if (viewModel.aiLoading) {
                            CircularProgressIndicator(
                                color = Color.White,
                                strokeWidth = 2.dp,
                                modifier = Modifier.size(16.dp)
                            )
                        } else {
                            Icon(Icons.Default.Send, contentDescription = "Preguntar", modifier = Modifier.size(16.dp))
                        }
                    }
                }

                if (!viewModel.aiError.isNullOrBlank()) {
                    Text(
                        text = viewModel.aiError ?: "",
                        color = IoBuildRed500,
                        fontSize = 12.sp
                    )
                }

                viewModel.aiAnswer?.let { ans ->
                    Surface(
                        color = IoBuildSlate100,
                        shape = RoundedCornerShape(12.dp),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Column(
                            modifier = Modifier.padding(14.dp),
                            verticalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            if (!ans.period.isNullOrBlank()) {
                                Text(
                                    text = "Periodo analizado: ${FormatUtils.formatPeriod(ans.period)}",
                                    fontSize = 11.sp,
                                    fontWeight = FontWeight.Bold,
                                    color = IoBuildEmerald700
                                )
                            }
                            Text(
                                text = ans.summary,
                                fontSize = 13.sp,
                                fontWeight = FontWeight.SemiBold,
                                color = IoBuildSlate900
                            )
                            if (ans.waterAnalysis.isNotBlank()) {
                                Text("• Agua: ${ans.waterAnalysis}", fontSize = 12.sp, color = IoBuildSlate700)
                            }
                            if (ans.energyAnalysis.isNotBlank()) {
                                Text("• Energía: ${ans.energyAnalysis}", fontSize = 12.sp, color = IoBuildSlate700)
                            }
                            if (ans.aliquotImpact.isNotBlank()) {
                                Text("• Tu alícuota: ${ans.aliquotImpact}", fontSize = 12.sp, color = IoBuildSlate700)
                            }
                            ans.recommendations?.takeIf { it.isNotEmpty() }?.let { recs ->
                                Text(
                                    text = "Recomendaciones:",
                                    fontSize = 12.sp,
                                    fontWeight = FontWeight.Bold,
                                    color = IoBuildSlate800
                                )
                                recs.forEach { r ->
                                    Text("  – $r", fontSize = 12.sp, color = IoBuildSlate700)
                                }
                            }
                        }
                    }
                }
            }
        }

        // ── 4. Últimas 3 Liquidaciones ──
        val recent = overview?.recentStatements.orEmpty()
        if (recent.isNotEmpty()) {
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(14.dp),
                colors = CardDefaults.cardColors(containerColor = Color.White),
                border = androidx.compose.foundation.BorderStroke(1.dp, IoBuildSlate200)
            ) {
                Column(
                    modifier = Modifier.padding(16.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = "Últimas liquidaciones",
                            fontWeight = FontWeight.Bold,
                            fontSize = 16.sp,
                            color = IoBuildSlate900
                        )
                        TextButton(onClick = onNavigateToAccountStatement) {
                            Text("Estado de cuenta", color = IoBuildEmerald600, fontSize = 13.sp)
                        }
                    }

                    recent.forEach { item ->
                        RecentStatementRow(item = item, onClick = {
                            onOpenBreakdown(item.settlementLineId.takeIf { it > 0 } ?: item.id)
                        })
                        HorizontalDivider(color = IoBuildSlate100)
                    }
                }
            }
        }
    }
}

@Composable
private fun FilterChipButton(
    label: String,
    selected: Boolean,
    onClick: () -> Unit
) {
    Surface(
        onClick = onClick,
        shape = RoundedCornerShape(8.dp),
        color = if (selected) Color.White else Color.Transparent,
        shadowElevation = if (selected) 2.dp else 0.dp
    ) {
        Text(
            text = label,
            fontSize = 12.sp,
            fontWeight = if (selected) FontWeight.Bold else FontWeight.Medium,
            color = if (selected) IoBuildEmerald700 else IoBuildSlate700,
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp)
        )
    }
}

@Composable
private fun KpiStatCard(
    modifier: Modifier = Modifier,
    title: String,
    value: String,
    subtitle: String,
    icon: ImageVector,
    iconTint: Color,
    iconBg: Color
) {
    Card(
        modifier = modifier,
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        border = androidx.compose.foundation.BorderStroke(1.dp, IoBuildSlate200)
    ) {
        Column(
            modifier = Modifier.padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = title,
                    fontSize = 12.sp,
                    color = IoBuildSlate500,
                    fontWeight = FontWeight.Medium
                )
                Box(
                    modifier = Modifier
                        .size(30.dp)
                        .clip(CircleShape)
                        .background(iconBg),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(icon, contentDescription = null, tint = iconTint, modifier = Modifier.size(16.dp))
                }
            }
            Text(
                text = value,
                fontSize = 20.sp,
                fontWeight = FontWeight.ExtraBold,
                color = IoBuildSlate900
            )
            Text(
                text = subtitle,
                fontSize = 11.sp,
                color = IoBuildSlate500
            )
        }
    }
}

@Composable
private fun CommonAreaItemRow(
    area: CommonAreaConsumptionRow,
    metric: String
) {
    val isWater = metric == "water"
    val valueText = if (!area.monitored) "—" else if (isWater) "${area.waterM3} m³" else "${area.energyKwh} kWh"
    val sharePct = if (isWater) area.waterSharePct else area.energySharePct
    val barColor = if (isWater) IoBuildEmerald500 else IoBuildAmber500
    val isOnline = area.monitored && area.metersOnline == area.metersTotal

    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    imageVector = when (area.type) {
                        "cistern", "pool" -> Icons.Default.WaterDrop
                        "pump_room" -> Icons.Default.Settings
                        "elevator" -> Icons.Default.SwapVert
                        else -> Icons.Default.Bolt
                    },
                    contentDescription = null,
                    tint = IoBuildEmerald600,
                    modifier = Modifier.size(16.dp)
                )
                Spacer(Modifier.width(6.dp))
                Text(
                    text = area.name,
                    fontSize = 13.sp,
                    fontWeight = FontWeight.SemiBold,
                    color = IoBuildSlate900
                )
            }

            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Text(
                    text = valueText,
                    fontSize = 13.sp,
                    fontWeight = FontWeight.Bold,
                    color = IoBuildSlate900
                )
                Surface(
                    color = if (!area.monitored) IoBuildSlate100 else if (isOnline) IoBuildEmerald50 else IoBuildRed50,
                    shape = RoundedCornerShape(6.dp)
                ) {
                    Text(
                        text = if (!area.monitored) "Sin medidor" else if (isOnline) "En línea" else "Sin señal",
                        fontSize = 10.sp,
                        fontWeight = FontWeight.SemiBold,
                        color = if (!area.monitored) IoBuildSlate500 else if (isOnline) IoBuildEmerald700 else IoBuildRed500,
                        modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp)
                    )
                }
            }
        }

        if (area.monitored && sharePct > 0) {
            LinearProgressIndicator(
                progress = { (sharePct.toFloat() / 100f).coerceIn(0f, 1f) },
                modifier = Modifier
                    .fillMaxWidth()
                    .height(6.dp)
                    .clip(RoundedCornerShape(50)),
                color = barColor,
                trackColor = IoBuildSlate100
            )
        }
    }
}

@Composable
private fun RecentStatementRow(
    item: StatementSummary,
    onClick: () -> Unit
) {
    Surface(
        onClick = onClick,
        color = Color.Transparent,
        modifier = Modifier.fillMaxWidth()
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(vertical = 8.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                text = FormatUtils.formatPeriod(item.period),
                fontSize = 14.sp,
                fontWeight = FontWeight.Medium,
                color = IoBuildSlate900
            )
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                Text(
                    text = FormatUtils.formatPen(item.amount),
                    fontSize = 14.sp,
                    fontWeight = FontWeight.Bold,
                    color = IoBuildSlate900
                )
                StatusBadge(status = item.effectiveStatus)
            }
        }
    }
}

@Composable
fun StatusBadge(status: String?) {
    val norm = status?.lowercase() ?: "pending"
    val (bg, fg) = when (norm) {
        "paid" -> IoBuildEmerald50 to IoBuildEmerald700
        "overdue" -> IoBuildRed50 to Color(0xFFB91C1C)
        else -> IoBuildAmber50 to Color(0xFFB45309)
    }
    Surface(
        color = bg,
        shape = RoundedCornerShape(8.dp),
        border = androidx.compose.foundation.BorderStroke(1.dp, fg.copy(alpha = 0.25f))
    ) {
        Text(
            text = FormatUtils.statusLabel(norm),
            color = fg,
            fontSize = 11.sp,
            fontWeight = FontWeight.Bold,
            modifier = Modifier.padding(horizontal = 8.dp, vertical = 3.dp)
        )
    }
}

@Composable
fun ErrorBanner(
    message: String,
    onRetry: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = IoBuildRed50),
        border = androidx.compose.foundation.BorderStroke(1.dp, IoBuildRed500)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                text = message,
                color = Color(0xFFB91C1C),
                fontSize = 13.sp,
                modifier = Modifier.weight(1f)
            )
            TextButton(onClick = onRetry) {
                Text("Reintentar", fontWeight = FontWeight.Bold, color = Color(0xFFB91C1C))
            }
        }
    }
}
