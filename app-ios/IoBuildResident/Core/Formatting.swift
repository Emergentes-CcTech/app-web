import Foundation

/// Textos traducidos (Localizable.xcstrings, español e inglés). Las claves son las mismas que en la app Android.
enum L10n {
    static func t(_ key: String, _ args: CVarArg...) -> String {
        let format = NSLocalizedString(key, comment: "")
        guard !args.isEmpty else { return format }
        return String(format: format, locale: Fmt.appLocale, arguments: args)
    }
}

/// Formatos de montos, porcentajes, periodos y fechas en el idioma activo de la app.
enum Fmt {
    /// Idioma en que se muestra la app (el que iOS eligió entre español e inglés).
    static var appLocale: Locale {
        let language = Bundle.main.preferredLocalizations.first ?? "es"
        return Locale(identifier: language == "en" ? "en_US" : "es_PE")
    }

    private static let penFormatter: NumberFormatter = {
        let formatter = NumberFormatter()
        formatter.locale = Locale(identifier: "es_PE")
        formatter.numberStyle = .decimal
        formatter.minimumFractionDigits = 2
        formatter.maximumFractionDigits = 2
        return formatter
    }()

    static func pen(_ value: Double?) -> String {
        "S/ " + (penFormatter.string(from: NSNumber(value: value ?? 0)) ?? "0.00")
    }

    static func aliquot(_ value: Double?) -> String {
        String(format: "%.4f%%", locale: Locale(identifier: "en_US_POSIX"), value ?? 0)
    }

    static func number(_ value: Double?, digits: Int = 1) -> String {
        let formatter = NumberFormatter()
        formatter.locale = appLocale
        formatter.numberStyle = .decimal
        formatter.minimumFractionDigits = 0
        formatter.maximumFractionDigits = digits
        return formatter.string(from: NSNumber(value: value ?? 0)) ?? "0"
    }

    /// "2026-10" → "Octubre 2026" / "October 2026"
    static func period(_ period: String?) -> String {
        guard let period, !period.isEmpty else { return "—" }
        let parser = DateFormatter()
        parser.locale = Locale(identifier: "en_US_POSIX")
        parser.dateFormat = "yyyy-MM"
        guard let date = parser.date(from: String(period.prefix(7))) else { return period }
        let output = DateFormatter()
        output.locale = appLocale
        output.dateFormat = "LLLL yyyy"
        let text = output.string(from: date)
        return text.prefix(1).uppercased() + text.dropFirst()
    }

    /// "2026-10-30" → "30 oct 2026" / "Oct 30, 2026"
    static func date(_ value: String?) -> String {
        guard let value, value.count >= 10 else { return "—" }
        let parser = DateFormatter()
        parser.locale = Locale(identifier: "en_US_POSIX")
        parser.dateFormat = "yyyy-MM-dd"
        guard let date = parser.date(from: String(value.prefix(10))) else { return value }
        let output = DateFormatter()
        output.locale = appLocale
        output.dateStyle = .medium
        return output.string(from: date)
    }

    static func statusLabel(_ status: String?) -> String {
        switch (status ?? "").lowercased() {
        case "paid": return L10n.t("status_paid")
        case "overdue": return L10n.t("status_overdue")
        default: return L10n.t("status_pending")
        }
    }

    static func signedPercent(_ value: Double) -> String {
        (value > 0 ? "+" : "") + number(value)
    }
}

extension String {
    var trimmed: String { trimmingCharacters(in: .whitespacesAndNewlines) }
    var isBlank: Bool { trimmed.isEmpty }
}
