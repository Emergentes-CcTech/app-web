package pe.iobuild.resident.ui.screens

import androidx.compose.ui.res.stringResource
import pe.iobuild.resident.R
import android.content.Intent
import android.net.Uri
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
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import pe.iobuild.resident.data.model.FeeExplanationDetail
import pe.iobuild.resident.ui.theme.*
import pe.iobuild.resident.ui.util.FormatUtils
import pe.iobuild.resident.ui.viewmodel.ResidentViewModel
import kotlin.math.abs
import kotlin.math.roundToInt

/**
 * Vista 3 del Propietario: "Mi Cuota Actual" / "¿Cómo se calculó mi cuota?" (equivalente a `audit.view.vue` + `fee-explanation.vue`).
 * Consume el endpoint BFF `/api/v1/mobile/resident/fee-explanation`:
 * - Selector de periodo liquidado (si tiene más de un mes).
 * - Paso 1: Lo que costó el edificio en el mes (Recibos matriz de Agua, Luz y Otros gastos comunes).
 * - Paso 2: Tu parte según tu Alícuota Legal (Ley N.° 27157) con fórmula explícita.
 * - Paso 3: Así se compone tu cuota (barra de proporción + importes individuales).
 * - Comparación con el mes anterior, alertas con costo del periodo, enlaces a los recibos escaneados y glosario legal.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun FeeExplanationScreen(
    viewModel: ResidentViewModel,
    onGoToPay: () -> Unit,
    onAskAiOnDashboard: () -> Unit
) {
    val context = LocalContext.current
    val feeResp = viewModel.feeData
    val explanation = feeResp?.explanation
    val lines = feeResp?.availableLines.orEmpty()
    var dropdownExpanded by remember { mutableStateOf(false) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // Encabezado
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = stringResource(R.string.fee_title),
                    fontSize = 22.sp,
                    fontWeight = FontWeight.ExtraBold,
                    color = IoBuildSlate900
                )
                Text(
                    text = stringResource(R.string.fee_subtitle),
                    fontSize = 12.sp,
                    color = IoBuildSlate500
                )
            }
        }

        // Selector de Periodo (cuando hay más de una liquidación)
        if (lines.size > 1) {
            val currentSelected = lines.find { it.lineId == viewModel.selectedLineId } ?: lines.first()
            ExposedDropdownMenuBox(
                expanded = dropdownExpanded,
                onExpandedChange = { dropdownExpanded = !dropdownExpanded }
            ) {
                OutlinedTextField(
                    value = "${FormatUtils.formatPeriod(currentSelected.period)} · ${FormatUtils.formatPen(currentSelected.amount)}",
                    onValueChange = {},
                    readOnly = true,
                    label = { Text(stringResource(R.string.fee_pick_month)) },
                    trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = dropdownExpanded) },
                    modifier = Modifier
                        .menuAnchor()
                        .fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp),
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedContainerColor = Color.White,
                        unfocusedContainerColor = Color.White
                    )
                )
                ExposedDropdownMenu(
                    expanded = dropdownExpanded,
                    onDismissRequest = { dropdownExpanded = false }
                ) {
                    lines.forEach { item ->
                        DropdownMenuItem(
                            text = {
                                Text("${FormatUtils.formatPeriod(item.period)} · ${FormatUtils.formatPen(item.amount)}")
                            },
                            onClick = {
                                dropdownExpanded = false
                                viewModel.loadFeeExplanation(item.lineId)
                            }
                        )
                    }
                }
            }
        }

        if (viewModel.feeLoading && explanation == null) {
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

        if (!viewModel.feeError.isNullOrBlank() && explanation == null) {
            ErrorBanner(
                message = viewModel.feeError ?: stringResource(R.string.err_load_fee),
                onRetry = { viewModel.loadFeeExplanation(viewModel.selectedLineId) }
            )
            return@Column
        }

        if (explanation == null) {
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(14.dp),
                colors = CardDefaults.cardColors(containerColor = Color.White)
            ) {
                Text(
                    text = stringResource(R.string.fee_empty),
                    modifier = Modifier.padding(20.dp),
                    color = IoBuildSlate500
                )
            }
            return@Column
        }

        FeeExplanationContent(
            explanation = explanation,
            onGoToPay = onGoToPay,
            onAskAi = onAskAiOnDashboard,
            onOpenUrl = { url ->
                try {
                    context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)))
                } catch (_: Exception) {
                }
            }
        )
    }
}

@Composable
private fun FeeExplanationContent(
    explanation: FeeExplanationDetail,
    onGoToPay: () -> Unit,
    onAskAi: () -> Unit,
    onOpenUrl: (String) -> Unit
) {
    val monthLabel = FormatUtils.formatPeriod(explanation.period)
    val prevMonthLabel = FormatUtils.formatPeriod(explanation.previousPeriod)
    val isPaid = explanation.status.lowercase() == "paid"

    val totalMine = explanation.mine.total.coerceAtLeast(0.01)
    val waterSharePct = ((explanation.mine.water / totalMine) * 1000.0).roundToInt() / 10.0
    val energySharePct = ((explanation.mine.energy / totalMine) * 1000.0).roundToInt() / 10.0
    val otherSharePct = ((explanation.mine.other / totalMine) * 1000.0).roundToInt() / 10.0

    // ── Hero de la Cuota ──
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(
            containerColor = if (isPaid) IoBuildEmerald50 else Color.White
        ),
        border = androidx.compose.foundation.BorderStroke(
            1.5.dp,
            if (isPaid) IoBuildEmerald500 else IoBuildSlate200
        )
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(18.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = stringResource(R.string.fee_your_fee, monthLabel, explanation.unitNumber ?: "—"),
                    fontSize = 13.sp,
                    color = IoBuildSlate700,
                    fontWeight = FontWeight.Medium
                )
                Spacer(Modifier.height(4.dp))
                Text(
                    text = FormatUtils.formatPen(explanation.mine.total),
                    fontSize = 32.sp,
                    fontWeight = FontWeight.ExtraBold,
                    color = IoBuildSlate900
                )
                Spacer(Modifier.height(4.dp))
                Text(
                    text = if (isPaid) {
                        if (!explanation.paidAt.isNullOrBlank()) stringResource(R.string.paid_on, FormatUtils.formatDate(explanation.paidAt)) else stringResource(R.string.status_paid)
                    } else {
                        stringResource(R.string.due_on, FormatUtils.formatDate(explanation.dueDate))
                    },
                    fontSize = 12.sp,
                    color = IoBuildSlate500
                )
            }
            if (!isPaid) {
                Button(
                    onClick = onGoToPay,
                    colors = ButtonDefaults.buttonColors(containerColor = IoBuildEmerald600),
                    shape = RoundedCornerShape(10.dp)
                ) {
                    Icon(Icons.Default.AccountBalanceWallet, null, modifier = Modifier.size(16.dp))
                    Spacer(Modifier.width(6.dp))
                    Text(stringResource(R.string.fee_go_pay))
                }
            }
        }
    }

    // ── PASO 1: Lo que costó el edificio ──
    StepCard(stepNumber = 1, title = stringResource(R.string.fee_step1_title, monthLabel)) {
        Text(
            text = stringResource(R.string.fee_step1_body),
            fontSize = 13.sp,
            color = IoBuildSlate500
        )
        Spacer(Modifier.height(8.dp))
        ConceptRow(label = stringResource(R.string.fee_water_bill), amount = explanation.common.water, dotColor = IoBuildEmerald500)
        ConceptRow(label = stringResource(R.string.fee_power_bill), amount = explanation.common.energy, dotColor = IoBuildAmber500)
        if (explanation.common.other > 0) {
            ConceptRow(label = stringResource(R.string.fee_other), amount = explanation.common.other, dotColor = IoBuildSlate500)
        }
        HorizontalDivider(modifier = Modifier.padding(vertical = 6.dp), color = IoBuildSlate200)
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Text(stringResource(R.string.fee_total_common), fontWeight = FontWeight.Bold, fontSize = 14.sp, color = IoBuildSlate900)
            Text(FormatUtils.formatPen(explanation.common.total), fontWeight = FontWeight.ExtraBold, fontSize = 14.sp, color = IoBuildSlate900)
        }
    }

    // ── PASO 2: Tu parte según tu alícuota legal ──
    val aliquotFormatted = FormatUtils.formatAliquot(explanation.aliquot)
    StepCard(stepNumber = 2, title = stringResource(R.string.fee_step2_title, aliquotFormatted)) {
        Text(
            text = stringResource(R.string.fee_step2_body, FormatUtils.formatPen(explanation.aliquot)),
            fontSize = 13.sp,
            color = IoBuildSlate500
        )
        Spacer(Modifier.height(8.dp))
        Surface(
            color = IoBuildEmerald50,
            shape = RoundedCornerShape(10.dp),
            modifier = Modifier.fillMaxWidth()
        ) {
            Text(
                text = "${FormatUtils.formatPen(explanation.common.total)} × $aliquotFormatted = ${FormatUtils.formatPen(explanation.mine.total)}",
                fontSize = 15.sp,
                fontWeight = FontWeight.ExtraBold,
                color = IoBuildEmerald700,
                modifier = Modifier.padding(12.dp)
            )
        }
    }

    // ── PASO 3: Así se compone tu cuota ──
    StepCard(stepNumber = 3, title = stringResource(R.string.fee_step3_title)) {
        // Barra apilada de proporciones
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .height(14.dp)
                .clip(RoundedCornerShape(50))
                .background(IoBuildSlate100)
        ) {
            if (waterSharePct > 0) {
                Box(
                    modifier = Modifier
                        .weight(waterSharePct.toFloat().coerceAtLeast(1f))
                        .fillMaxHeight()
                        .background(IoBuildEmerald500)
                )
            }
            if (energySharePct > 0) {
                Box(
                    modifier = Modifier
                        .weight(energySharePct.toFloat().coerceAtLeast(1f))
                        .fillMaxHeight()
                        .background(IoBuildAmber500)
                )
            }
            if (otherSharePct > 0) {
                Box(
                    modifier = Modifier
                        .weight(otherSharePct.toFloat().coerceAtLeast(1f))
                        .fillMaxHeight()
                        .background(IoBuildSlate500)
                )
            }
        }

        Spacer(Modifier.height(8.dp))
        ConceptRow(
            label = stringResource(R.string.fee_share_water, waterSharePct.toString()),
            amount = explanation.mine.water,
            dotColor = IoBuildEmerald500
        )
        ConceptRow(
            label = stringResource(R.string.fee_share_power, energySharePct.toString()),
            amount = explanation.mine.energy,
            dotColor = IoBuildAmber500
        )
        if (explanation.mine.other > 0 || explanation.common.other > 0) {
            ConceptRow(
                label = stringResource(R.string.fee_share_other, otherSharePct.toString()),
                amount = explanation.mine.other,
                dotColor = IoBuildSlate500
            )
        }
        HorizontalDivider(modifier = Modifier.padding(vertical = 6.dp), color = IoBuildSlate200)
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Text(stringResource(R.string.fee_yours), fontWeight = FontWeight.Bold, fontSize = 15.sp, color = IoBuildSlate900)
            Text(FormatUtils.formatPen(explanation.mine.total), fontWeight = FontWeight.ExtraBold, fontSize = 15.sp, color = IoBuildSlate900)
        }
    }

    // ── Comparación con el mes anterior ──
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        border = androidx.compose.foundation.BorderStroke(1.dp, IoBuildSlate200)
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            Text(
                text = stringResource(R.string.fee_compare_with, prevMonthLabel),
                fontWeight = FontWeight.Bold,
                fontSize = 16.sp,
                color = IoBuildSlate900
            )
            val prev = explanation.previous
            if (prev == null || prev.totalSoles <= 0) {
                Text(
                    text = stringResource(R.string.fee_no_previous, prevMonthLabel),
                    fontSize = 13.sp,
                    color = IoBuildSlate500
                )
            } else {
                val delta = explanation.mine.total - prev.totalSoles
                val pct = ((delta / prev.totalSoles) * 1000.0).roundToInt() / 10.0
                val isUp = delta > 0
                Text(
                    text = "${FormatUtils.formatPen(prev.totalSoles)} → ${FormatUtils.formatPen(explanation.mine.total)} (${if (isUp) "+" else "−"}${FormatUtils.formatPen(abs(delta))} · ${abs(pct)}%)",
                    fontSize = 14.sp,
                    fontWeight = FontWeight.Bold,
                    color = if (isUp) Color(0xFFB91C1C) else IoBuildEmerald700
                )
            }
        }
    }

    // ── Alertas con costo en el mes ──
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
            Text(
                text = stringResource(R.string.fee_alerts_title, monthLabel),
                fontWeight = FontWeight.Bold,
                fontSize = 16.sp,
                color = IoBuildSlate900
            )
            if (explanation.alerts.isEmpty()) {
                Text(
                    text = stringResource(R.string.fee_no_alerts),
                    fontSize = 13.sp,
                    color = IoBuildSlate500
                )
            } else {
                explanation.alerts.forEach { a ->
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(vertical = 4.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Column(modifier = Modifier.weight(1f)) {
                            Text(
                                text = a.title ?: a.alertType,
                                fontWeight = FontWeight.SemiBold,
                                fontSize = 13.sp,
                                color = IoBuildSlate900
                            )
                            Text(
                                text = stringResource(R.string.fee_alert_cost, FormatUtils.formatPen(a.overcost), FormatUtils.formatPen(a.myShare)),
                                fontSize = 12.sp,
                                color = IoBuildSlate500
                            )
                        }
                        Surface(
                            color = if (a.isOpen) IoBuildAmber50 else IoBuildEmerald50,
                            shape = RoundedCornerShape(6.dp)
                        ) {
                            Text(
                                text = stringResource(if (a.isOpen) R.string.fee_alert_open else R.string.fee_alert_resolved),
                                fontSize = 11.sp,
                                fontWeight = FontWeight.Bold,
                                color = if (a.isOpen) Color(0xFFB45309) else IoBuildEmerald700,
                                modifier = Modifier.padding(horizontal = 8.dp, vertical = 3.dp)
                            )
                        }
                    }
                }
            }
        }
    }

    // ── De dónde salen los datos (Evidencia de Recibos Matriz) ──
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
            Text(
                text = stringResource(R.string.fee_sources_title),
                fontWeight = FontWeight.Bold,
                fontSize = 16.sp,
                color = IoBuildSlate900
            )
            Text(
                text = stringResource(R.string.fee_sources_body, explanation.common.waterM3.toString(), explanation.common.energyKwh.toString()),
                fontSize = 13.sp,
                color = IoBuildSlate500
            )
            val hasWaterUrl = !explanation.common.waterReceiptUrl.isNullOrBlank()
            val hasEnergyUrl = !explanation.common.energyReceiptUrl.isNullOrBlank()
            if (hasWaterUrl || hasEnergyUrl) {
                Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                    if (hasWaterUrl) {
                        OutlinedButton(onClick = { onOpenUrl(explanation.common.waterReceiptUrl!!) }) {
                            Icon(Icons.Default.Description, null, modifier = Modifier.size(16.dp))
                            Spacer(Modifier.width(6.dp))
                            Text(stringResource(R.string.fee_view_water_bill), fontSize = 12.sp)
                        }
                    }
                    if (hasEnergyUrl) {
                        OutlinedButton(onClick = { onOpenUrl(explanation.common.energyReceiptUrl!!) }) {
                            Icon(Icons.Default.Description, null, modifier = Modifier.size(16.dp))
                            Spacer(Modifier.width(6.dp))
                            Text(stringResource(R.string.fee_view_power_bill), fontSize = 12.sp)
                        }
                    }
                }
            } else {
                Text(
                    text = stringResource(R.string.fee_no_bills),
                    fontSize = 12.sp,
                    color = IoBuildSlate500
                )
            }
        }
    }

    // ── Glosario Legal (Ley N.° 27157) ──
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        border = androidx.compose.foundation.BorderStroke(1.dp, IoBuildSlate200)
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Text(
                text = stringResource(R.string.fee_glossary_title),
                fontWeight = FontWeight.Bold,
                fontSize = 16.sp,
                color = IoBuildSlate900
            )
            GlossaryItem(
                term = stringResource(R.string.glossary_aliquot),
                definition = stringResource(R.string.glossary_aliquot_def)
            )
            GlossaryItem(
                term = stringResource(R.string.glossary_common),
                definition = stringResource(R.string.glossary_common_def)
            )
            GlossaryItem(
                term = stringResource(R.string.glossary_master_bill),
                definition = stringResource(R.string.glossary_master_bill_def)
            )

            TextButton(onClick = onAskAi) {
                Icon(Icons.Default.AutoAwesome, null, tint = IoBuildEmerald600, modifier = Modifier.size(16.dp))
                Spacer(Modifier.width(6.dp))
                Text(stringResource(R.string.fee_ask_ai), color = IoBuildEmerald600, fontWeight = FontWeight.Bold)
            }
        }
    }
}

@Composable
private fun StepCard(
    stepNumber: Int,
    title: String,
    content: @Composable ColumnScope.() -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        border = androidx.compose.foundation.BorderStroke(1.dp, IoBuildSlate200)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
            verticalAlignment = Alignment.Top
        ) {
            Box(
                modifier = Modifier
                    .size(32.dp)
                    .clip(CircleShape)
                    .background(IoBuildEmerald500),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = stepNumber.toString(),
                    color = Color.White,
                    fontWeight = FontWeight.Bold,
                    fontSize = 14.sp
                )
            }
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = title,
                    fontWeight = FontWeight.Bold,
                    fontSize = 16.sp,
                    color = IoBuildSlate900
                )
                Spacer(Modifier.height(6.dp))
                content()
            }
        }
    }
}

@Composable
private fun ConceptRow(
    label: String,
    amount: Double,
    dotColor: Color
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 3.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                modifier = Modifier
                    .size(10.dp)
                    .clip(CircleShape)
                    .background(dotColor)
            )
            Spacer(Modifier.width(8.dp))
            Text(text = label, fontSize = 13.sp, color = IoBuildSlate700)
        }
        Text(
            text = FormatUtils.formatPen(amount),
            fontSize = 13.sp,
            fontWeight = FontWeight.SemiBold,
            color = IoBuildSlate900
        )
    }
}

@Composable
private fun GlossaryItem(term: String, definition: String) {
    Column {
        Text(text = term, fontWeight = FontWeight.Bold, fontSize = 13.sp, color = IoBuildSlate900)
        Text(text = definition, fontSize = 12.sp, color = IoBuildSlate500)
    }
}
