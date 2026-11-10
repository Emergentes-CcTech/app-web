package pe.iobuild.resident.ui.util

import java.text.NumberFormat
import java.util.Locale

object FormatUtils {

    private val penFormatter = NumberFormat.getNumberInstance(Locale("es", "PE")).apply {
        minimumFractionDigits = 2
        maximumFractionDigits = 2
    }

    fun formatPen(amount: Double?): String {
        val v = amount ?: 0.0
        return "S/ ${penFormatter.format(v)}"
    }

    fun formatAliquot(pct: Double?): String {
        val v = pct ?: 0.0
        return String.format(Locale.US, "%.4f%%", v)
    }

    fun formatPeriod(period: String?): String {
        if (period.isNullOrBlank()) return "—"
        val parts = period.split("-")
        if (parts.size != 2) return period
        val year = parts[0]
        val monthName = when (parts[1]) {
            "01" -> "Enero"
            "02" -> "Febrero"
            "03" -> "Marzo"
            "04" -> "Abril"
            "05" -> "Mayo"
            "06" -> "Junio"
            "07" -> "Julio"
            "08" -> "Agosto"
            "09" -> "Septiembre"
            "10" -> "Octubre"
            "11" -> "Noviembre"
            "12" -> "Diciembre"
            else -> parts[1]
        }
        return "$monthName $year"
    }

    fun statusLabel(status: String?): String {
        return when (status?.lowercase()) {
            "paid" -> "Pagado"
            "overdue" -> "Vencido"
            else -> "Pendiente"
        }
    }
}
