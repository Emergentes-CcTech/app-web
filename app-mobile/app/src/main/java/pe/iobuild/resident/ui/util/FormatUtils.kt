package pe.iobuild.resident.ui.util

import androidx.annotation.StringRes
import pe.iobuild.resident.R
import java.time.LocalDate
import java.time.YearMonth
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle
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

    // "2026-10" → "Octubre 2026" / "October 2026", según el idioma activo de la app
    fun formatPeriod(period: String?): String {
        if (period.isNullOrBlank()) return "—"
        return try {
            val locale = Locale.getDefault()
            YearMonth.parse(period).format(DateTimeFormatter.ofPattern("LLLL yyyy", locale))
                .replaceFirstChar { it.titlecase(locale) }
        } catch (e: Exception) {
            period
        }
    }

    // "2026-10-30" → "30 oct 2026" / "Oct 30, 2026"
    fun formatDate(date: String?): String {
        if (date.isNullOrBlank()) return "—"
        return try {
            LocalDate.parse(date.take(10)).format(DateTimeFormatter.ofLocalizedDate(FormatStyle.MEDIUM).withLocale(Locale.getDefault()))
        } catch (e: Exception) {
            date
        }
    }

    @StringRes
    fun statusLabelRes(status: String?): Int = when (status?.lowercase()) {
        "paid" -> R.string.status_paid
        "overdue" -> R.string.status_overdue
        else -> R.string.status_pending
    }
}
