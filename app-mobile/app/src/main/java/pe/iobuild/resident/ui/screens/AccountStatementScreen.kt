package pe.iobuild.resident.ui.screens

import androidx.compose.ui.res.stringResource
import pe.iobuild.resident.R
import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ReceiptLong
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import pe.iobuild.resident.data.model.AccountUnitFacts
import pe.iobuild.resident.data.model.StatementSummary
import pe.iobuild.resident.ui.theme.*
import pe.iobuild.resident.ui.util.FormatUtils
import pe.iobuild.resident.ui.viewmodel.ResidentViewModel

/**
 * Vista 2 del Propietario: "Estado de Cuenta" (equivalente a `account-statement.view.vue`).
 * Consume el endpoint BFF `/api/v1/mobile/resident/account-statement`:
 * - Aislamiento estricto a la unidad del propietario (sin selectores de unidades ajenas).
 * - Hero de cuota pendiente/vencida con botón "Pagar con tarjeta" (Stripe Checkout) o banner "Estás al día".
 * - Ficha legal de la unidad asignada (Departamento, Condominio, Alícuota Ley N.° 27157, Titular).
 * - Historial de cuotas con acciones "Ver desglose" y "Comprobante" compartible.
 */
@Composable
fun AccountStatementScreen(
    viewModel: ResidentViewModel,
    onOpenBreakdown: (Int) -> Unit
) {
    val context = LocalContext.current
    val statement = viewModel.statementData
    var receiptDialogItem by remember { mutableStateOf<StatementSummary?>(null) }

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
                    text = stringResource(R.string.statement_title),
                    fontSize = 24.sp,
                    fontWeight = FontWeight.ExtraBold,
                    color = IoBuildSlate900
                )
                Text(
                    text = stringResource(R.string.statement_subtitle),
                    fontSize = 12.sp,
                    color = IoBuildSlate500
                )
            }
        }

        // Notificación de pago exitoso o aviso de Stripe
        if (!viewModel.paymentNotice.isNullOrBlank()) {
            Surface(
                color = IoBuildEmerald50,
                shape = RoundedCornerShape(12.dp),
                border = androidx.compose.foundation.BorderStroke(1.dp, IoBuildEmerald500),
                modifier = Modifier.fillMaxWidth()
            ) {
                Row(
                    modifier = Modifier.padding(12.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.weight(1f)
                    ) {
                        Icon(Icons.Default.CheckCircle, null, tint = IoBuildEmerald600, modifier = Modifier.size(20.dp))
                        Spacer(Modifier.width(8.dp))
                        Text(
                            text = viewModel.paymentNotice ?: "",
                            color = IoBuildEmerald700,
                            fontSize = 13.sp,
                            fontWeight = FontWeight.SemiBold
                        )
                    }
                    IconButton(onClick = { viewModel.clearPaymentNotice() }) {
                        Icon(Icons.Default.Close, contentDescription = stringResource(R.string.action_close), modifier = Modifier.size(16.dp))
                    }
                }
            }
        }

        if (viewModel.statementLoading && statement == null) {
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

        if (!viewModel.statementError.isNullOrBlank() && statement == null) {
            ErrorBanner(
                message = viewModel.statementError ?: stringResource(R.string.err_load_statement),
                onRetry = { viewModel.loadAccountStatement() }
            )
            return@Column
        }

        // ── 1. Hero Section: Cuota Pendiente o "Estás al día" ──
        val pending = statement?.pendingStatement
        if (pending != null) {
            val isOverdue = pending.effectiveStatus.lowercase() == "overdue"
            val heroBg = if (isOverdue) IoBuildRed50 else IoBuildAmber50
            val heroBorder = if (isOverdue) IoBuildRed500 else IoBuildAmber500

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
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = stringResource(R.string.statement_pending_period, FormatUtils.formatPeriod(pending.period)),
                            fontSize = 13.sp,
                            fontWeight = FontWeight.SemiBold,
                            color = IoBuildSlate800
                        )
                        StatusBadge(status = pending.effectiveStatus)
                    }

                    Text(
                        text = FormatUtils.formatPen(pending.amount),
                        fontSize = 34.sp,
                        fontWeight = FontWeight.ExtraBold,
                        color = IoBuildSlate900
                    )

                    val dueDetails = buildString {
                        append(stringResource(R.string.due_on, FormatUtils.formatDate(pending.dueDate)))
                        if (pending.daysOverdue > 0) {
                            append(stringResource(R.string.statement_days_overdue, pending.daysOverdue))
                        }
                    }
                    Text(
                        text = dueDetails,
                        fontSize = 12.sp,
                        color = IoBuildSlate700
                    )

                    Button(
                        onClick = {
                            viewModel.payPendingStatement(pending.id) { checkoutUrl ->
                                try {
                                    context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(checkoutUrl)))
                                } catch (_: Exception) {
                                }
                            }
                        },
                        enabled = !viewModel.paymentInProgress,
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(48.dp),
                        colors = ButtonDefaults.buttonColors(containerColor = IoBuildEmerald600),
                        shape = RoundedCornerShape(12.dp)
                    ) {
                        if (viewModel.paymentInProgress) {
                            CircularProgressIndicator(
                                color = Color.White,
                                strokeWidth = 2.dp,
                                modifier = Modifier.size(18.dp)
                            )
                            Spacer(Modifier.width(8.dp))
                        } else {
                            Icon(Icons.Default.CreditCard, contentDescription = null, modifier = Modifier.size(18.dp))
                            Spacer(Modifier.width(8.dp))
                        }
                        Text(stringResource(R.string.statement_pay_card), fontWeight = FontWeight.Bold, fontSize = 15.sp)
                    }
                }
            }
        } else {
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(16.dp),
                colors = CardDefaults.cardColors(containerColor = IoBuildEmerald50),
                border = androidx.compose.foundation.BorderStroke(1.5.dp, IoBuildEmerald500)
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(18.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Icon(
                        imageVector = Icons.Default.CheckCircle,
                        contentDescription = null,
                        tint = IoBuildEmerald600,
                        modifier = Modifier.size(28.dp)
                    )
                    Spacer(Modifier.width(12.dp))
                    Column {
                        Text(
                            text = stringResource(R.string.statement_up_to_date),
                            fontSize = 18.sp,
                            fontWeight = FontWeight.ExtraBold,
                            color = IoBuildEmerald700
                        )
                        Text(
                            text = stringResource(R.string.statement_up_to_date_body),
                            fontSize = 12.sp,
                            color = IoBuildEmerald700.copy(alpha = 0.85f)
                        )
                    }
                }
            }
        }

        // ── 2. Ficha de Unidad Aislada (unit-facts: Departamento, Condominio, Alícuota, Titular) ──
        statement?.unit?.let { u ->
            UnitIsolationFactsCard(unit = u)
        }

        // ── 3. Historial de Cuotas ──
        Text(
            text = stringResource(R.string.statement_history),
            fontSize = 18.sp,
            fontWeight = FontWeight.Bold,
            color = IoBuildSlate900
        )

        val history = statement?.history.orEmpty()
        if (history.isEmpty()) {
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(14.dp),
                colors = CardDefaults.cardColors(containerColor = Color.White)
            ) {
                Text(
                    text = stringResource(R.string.statement_empty),
                    modifier = Modifier.padding(20.dp),
                    color = IoBuildSlate500
                )
            }
        } else {
            history.forEach { item ->
                StatementHistoryCard(
                    item = item,
                    onViewBreakdown = {
                        onOpenBreakdown(item.settlementLineId.takeIf { it > 0 } ?: item.id)
                    },
                    onShowReceipt = {
                        receiptDialogItem = item
                    }
                )
            }
        }
    }

    // Modal de Comprobante Descargable / Compartible
    receiptDialogItem?.let { paidItem ->
        val unit = statement?.unit
        val receiptText = remember(paidItem, unit) {
            buildString {
                appendLine(context.getString(R.string.receipt_header))
                appendLine("-----------------------------------------")
                appendLine(context.getString(R.string.receipt_unit, unit?.unitNumber ?: "—", unit?.condominiumName ?: "—"))
                appendLine(context.getString(R.string.receipt_holder, unit?.holderName ?: "—", FormatUtils.formatAliquot(unit?.aliquotPercentage)))
                appendLine(context.getString(R.string.receipt_period, FormatUtils.formatPeriod(paidItem.period)))
                appendLine(context.getString(R.string.receipt_concept, context.getString(R.string.common_area_fee)))
                appendLine(context.getString(R.string.receipt_amount, FormatUtils.formatPen(paidItem.amount)))
                appendLine(context.getString(R.string.receipt_paid_on, FormatUtils.formatDate(paidItem.paidAt)))
                appendLine(context.getString(R.string.receipt_method, paidItem.method ?: "—"))
                appendLine(context.getString(R.string.receipt_reference, paidItem.reference ?: "—"))
            }
        }

        AlertDialog(
            onDismissRequest = { receiptDialogItem = null },
            icon = {
                Icon(Icons.AutoMirrored.Filled.ReceiptLong, contentDescription = null, tint = IoBuildEmerald600)
            },
            title = {
                Text(stringResource(R.string.receipt_title), fontWeight = FontWeight.Bold)
            },
            text = {
                Surface(
                    color = IoBuildSlate100,
                    shape = RoundedCornerShape(10.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Text(
                        text = receiptText,
                        fontFamily = FontFamily.Monospace,
                        fontSize = 12.sp,
                        color = IoBuildSlate800,
                        modifier = Modifier.padding(12.dp)
                    )
                }
            },
            confirmButton = {
                Button(
                    onClick = {
                        val sendIntent = Intent().apply {
                            action = Intent.ACTION_SEND
                            putExtra(Intent.EXTRA_TEXT, receiptText)
                            type = "text/plain"
                        }
                        context.startActivity(Intent.createChooser(sendIntent, context.getString(R.string.receipt_share_title)))
                    },
                    colors = ButtonDefaults.buttonColors(containerColor = IoBuildEmerald600)
                ) {
                    Icon(Icons.Default.Share, contentDescription = null, modifier = Modifier.size(16.dp))
                    Spacer(Modifier.width(6.dp))
                    Text(stringResource(R.string.receipt_share))
                }
            },
            dismissButton = {
                TextButton(onClick = { receiptDialogItem = null }) {
                    Text(stringResource(R.string.action_close))
                }
            }
        )
    }
}

@Composable
private fun UnitIsolationFactsCard(unit: AccountUnitFacts) {
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
            Row(modifier = Modifier.fillMaxWidth()) {
                FactCell(
                    modifier = Modifier.weight(1f),
                    label = stringResource(R.string.statement_unit),
                    value = unit.unitNumber
                )
                FactCell(
                    modifier = Modifier.weight(1f),
                    label = stringResource(R.string.condominium),
                    value = unit.condominiumName ?: "—"
                )
            }
            HorizontalDivider(color = IoBuildSlate100)
            Row(modifier = Modifier.fillMaxWidth()) {
                FactCell(
                    modifier = Modifier.weight(1f),
                    label = stringResource(R.string.statement_aliquot),
                    value = FormatUtils.formatAliquot(unit.aliquotPercentage)
                )
                FactCell(
                    modifier = Modifier.weight(1f),
                    label = stringResource(R.string.statement_holder),
                    value = unit.holderName ?: "—"
                )
            }
        }
    }
}

@Composable
private fun FactCell(
    modifier: Modifier = Modifier,
    label: String,
    value: String
) {
    Column(modifier = modifier) {
        Text(
            text = label,
            fontSize = 11.sp,
            color = IoBuildSlate500
        )
        Spacer(Modifier.height(2.dp))
        Text(
            text = value,
            fontSize = 14.sp,
            fontWeight = FontWeight.Bold,
            color = IoBuildSlate900
        )
    }
}

@Composable
private fun StatementHistoryCard(
    item: StatementSummary,
    onViewBreakdown: () -> Unit,
    onShowReceipt: () -> Unit
) {
    val isPaid = item.effectiveStatus.lowercase() == "paid"

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
                Column {
                    Text(
                        text = FormatUtils.formatPeriod(item.period),
                        fontSize = 16.sp,
                        fontWeight = FontWeight.Bold,
                        color = IoBuildSlate900
                    )
                    Text(
                        text = stringResource(R.string.common_area_fee),
                        fontSize = 12.sp,
                        color = IoBuildSlate500
                    )
                }
                StatusBadge(status = item.effectiveStatus)
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = FormatUtils.formatPen(item.amount),
                    fontSize = 20.sp,
                    fontWeight = FontWeight.ExtraBold,
                    color = IoBuildSlate900
                )
                Text(
                    text = if (isPaid && !item.paidAt.isNullOrBlank()) stringResource(R.string.paid_on, FormatUtils.formatDate(item.paidAt)) else stringResource(R.string.due_on, FormatUtils.formatDate(item.dueDate)),
                    fontSize = 12.sp,
                    color = IoBuildSlate500
                )
            }

            HorizontalDivider(color = IoBuildSlate100)

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.End,
                verticalAlignment = Alignment.CenterVertically
            ) {
                TextButton(onClick = onViewBreakdown) {
                    Icon(Icons.Default.Search, contentDescription = null, modifier = Modifier.size(16.dp))
                    Spacer(Modifier.width(4.dp))
                    Text(stringResource(R.string.action_view_breakdown), fontSize = 13.sp)
                }
                if (isPaid) {
                    TextButton(onClick = onShowReceipt) {
                        Icon(Icons.Default.Download, contentDescription = null, modifier = Modifier.size(16.dp))
                        Spacer(Modifier.width(4.dp))
                        Text(stringResource(R.string.statement_receipt), fontSize = 13.sp)
                    }
                }
            }
        }
    }
}
