import SwiftUI

/// Símbolo de IoBuild: el edificio verde con su ventana (mismo trazo que la web y Android).
struct IoBuildLogo: View {
    var size: CGFloat = 40

    var body: some View {
        ZStack {
            BuildingShape().fill(Theme.emerald500)
            WindowShape().fill(.white)
        }
        .frame(width: size * 62 / 64, height: size)
        .accessibilityLabel("IoBuild")
    }

    private struct BuildingShape: Shape {
        func path(in rect: CGRect) -> Path {
            let sx = rect.width / 62, sy = rect.height / 64
            func p(_ x: CGFloat, _ y: CGFloat) -> CGPoint { CGPoint(x: rect.minX + x * sx, y: rect.minY + y * sy) }
            var path = Path()
            path.move(to: p(18.5, 1))
            path.addLine(to: p(57, 1))
            path.addQuadCurve(to: p(61, 5), control: p(61, 1))
            path.addLine(to: p(61, 41.5))
            path.addLine(to: p(40, 63))
            path.addLine(to: p(4.5, 63))
            path.addQuadCurve(to: p(0.5, 59), control: p(0.5, 63))
            path.addLine(to: p(0.5, 27.5))
            path.closeSubpath()
            return path
        }
    }

    private struct WindowShape: Shape {
        func path(in rect: CGRect) -> Path {
            let sx = rect.width / 62, sy = rect.height / 64
            return Path(CGRect(x: rect.minX + 40 * sx, y: rect.minY + 8.5 * sy, width: 11.5 * sx, height: 23.5 * sy))
        }
    }
}

struct StatusBadge: View {
    let status: String?

    var body: some View {
        let colors = palette
        Text(Fmt.statusLabel(status))
            .font(.system(size: 11, weight: .bold))
            .foregroundStyle(colors.fg)
            .padding(.horizontal, 10)
            .padding(.vertical, 4)
            .background(colors.bg, in: Capsule())
    }

    private var palette: (fg: Color, bg: Color) {
        switch (status ?? "").lowercased() {
        case "paid": return (Theme.emerald700, Theme.emerald100)
        case "overdue": return (Theme.red500, Theme.red50)
        default: return (Theme.amber500, Theme.amber50)
        }
    }
}

struct ErrorBanner: View {
    let message: String
    var onRetry: (() -> Void)?

    var body: some View {
        HStack(alignment: .top, spacing: 10) {
            Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(Theme.red500)
            VStack(alignment: .leading, spacing: 8) {
                Text(message).font(.system(size: 14)).foregroundStyle(Theme.slate800)
                if let onRetry {
                    Button(L10n.t("action_retry"), action: onRetry)
                        .font(.system(size: 14, weight: .semibold))
                }
            }
            Spacer(minLength: 0)
        }
        .card(background: Theme.red50, border: Theme.red500.opacity(0.3))
    }
}

/// Aviso con botón de cierre (pagos confirmados, perfil guardado…).
struct NoticeBanner: View {
    let message: String
    var isError = false
    let onClose: () -> Void

    var body: some View {
        HStack(alignment: .top, spacing: 10) {
            Image(systemName: isError ? "exclamationmark.circle.fill" : "checkmark.circle.fill")
                .foregroundStyle(isError ? Theme.red500 : Theme.emerald600)
            Text(message).font(.system(size: 14)).foregroundStyle(Theme.slate800)
            Spacer(minLength: 0)
            Button(action: onClose) {
                Image(systemName: "xmark").font(.system(size: 12, weight: .bold)).foregroundStyle(Theme.slate500)
            }
            .accessibilityLabel(L10n.t("action_close"))
        }
        .card(background: isError ? Theme.red50 : Theme.emerald50, border: (isError ? Theme.red500 : Theme.emerald500).opacity(0.35))
    }
}

struct LoadingBlock: View {
    var body: some View {
        ProgressView()
            .tint(Theme.emerald600)
            .frame(maxWidth: .infinity)
            .padding(.vertical, 48)
    }
}

struct ScreenHeader: View {
    let title: String
    var subtitle: String?

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(title).font(.system(size: 26, weight: .heavy)).foregroundStyle(Theme.slate900)
            if let subtitle, !subtitle.isEmpty {
                Text(subtitle).font(.system(size: 13)).foregroundStyle(Theme.slate500)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

struct KpiCard: View {
    let title: String
    let value: String
    let subtitle: String
    let systemImage: String
    let tint: Color
    let iconBackground: Color

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Image(systemName: systemImage)
                .font(.system(size: 15, weight: .semibold))
                .foregroundStyle(tint)
                .frame(width: 32, height: 32)
                .background(iconBackground, in: RoundedRectangle(cornerRadius: 9, style: .continuous))
            Text(title).font(.system(size: 12)).foregroundStyle(Theme.slate500).lineLimit(1)
            Text(value).font(.system(size: 18, weight: .bold)).foregroundStyle(Theme.slate900).lineLimit(1).minimumScaleFactor(0.7)
            Text(subtitle).font(.system(size: 11)).foregroundStyle(Theme.slate500).lineLimit(2)
        }
        .card(padding: 14)
    }
}

/// Fila etiqueta–valor de los desgloses.
struct ValueRow: View {
    let label: String
    let value: String
    var emphasized = false

    var body: some View {
        HStack {
            Text(label)
                .font(.system(size: 14, weight: emphasized ? .bold : .regular))
                .foregroundStyle(emphasized ? Theme.slate900 : Theme.slate700)
            Spacer()
            Text(value)
                .font(.system(size: 14, weight: emphasized ? .heavy : .semibold))
                .foregroundStyle(Theme.slate900)
                .monospacedDigit()
        }
    }
}

struct StatementRow: View {
    let statement: Statement

    var body: some View {
        HStack(spacing: 12) {
            VStack(alignment: .leading, spacing: 3) {
                Text(Fmt.period(statement.period)).font(.system(size: 15, weight: .semibold)).foregroundStyle(Theme.slate900)
                Text(statement.isPaid && statement.paidAt != nil
                     ? L10n.t("paid_on", Fmt.date(statement.paidAt))
                     : L10n.t("due_on", Fmt.date(statement.dueDate)))
                    .font(.system(size: 12)).foregroundStyle(Theme.slate500)
            }
            Spacer()
            VStack(alignment: .trailing, spacing: 4) {
                Text(Fmt.pen(statement.amount)).font(.system(size: 15, weight: .bold)).foregroundStyle(Theme.slate900).monospacedDigit()
                StatusBadge(status: statement.normalizedStatus)
            }
        }
        .contentShape(Rectangle())
    }
}
