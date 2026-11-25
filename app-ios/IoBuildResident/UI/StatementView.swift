import SwiftUI

/// "Estado de cuenta": la cuota pendiente con su pago por Stripe y el historial con comprobantes.
struct StatementView: View {
    @Environment(AppModel.self) private var model
    @State private var receipt: Statement?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                TabTopBar()
                ScreenHeader(title: L10n.t("statement_title"), subtitle: L10n.t("statement_subtitle"))

                if let notice = model.paymentNotice {
                    NoticeBanner(message: notice, isError: model.paymentNoticeIsError) { model.paymentNotice = nil }
                }

                if model.statementLoading && model.statement == nil {
                    LoadingBlock()
                } else if let error = model.statementError, model.statement == nil {
                    ErrorBanner(message: error) { Task { await model.loadStatement() } }
                } else if let statement = model.statement {
                    if let unit = statement.unit { unitCard(unit) }
                    pendingCard(statement.pendingStatement)
                    historyCard(statement.history ?? [])
                }
            }
            .padding(16)
        }
        .background(Theme.background.ignoresSafeArea())
        .refreshable { await model.loadStatement() }
        .toolbar(.hidden, for: .navigationBar)
        .sheet(item: $receipt) { item in
            ReceiptSheet(statement: item, unit: model.statement?.unit)
        }
    }

    private func unitCard(_ unit: AccountUnit) -> some View {
        VStack(spacing: 10) {
            ValueRow(label: L10n.t("statement_unit"), value: L10n.t("profile_condo_unit", unit.condominiumName ?? "—", unit.unitNumber ?? "—"))
            ValueRow(label: L10n.t("statement_aliquot"), value: Fmt.aliquot(unit.aliquotPercentage))
            ValueRow(label: L10n.t("statement_holder"), value: unit.holderName ?? "—")
        }
        .card()
    }

    @ViewBuilder
    private func pendingCard(_ pending: Statement?) -> some View {
        if let pending {
            let overdue = pending.normalizedStatus == "overdue"
            VStack(alignment: .leading, spacing: 10) {
                HStack {
                    Text(L10n.t("statement_pending_period", Fmt.period(pending.period)))
                        .font(.system(size: 13, weight: .semibold)).foregroundStyle(Theme.slate700)
                    Spacer()
                    StatusBadge(status: pending.normalizedStatus)
                }
                Text(Fmt.pen(pending.amount)).font(.system(size: 34, weight: .heavy)).foregroundStyle(Theme.slate900).monospacedDigit()
                Text(L10n.t("due_on", Fmt.date(pending.dueDate))
                     + ((pending.daysOverdue ?? 0) > 0 ? L10n.t("statement_days_overdue", pending.daysOverdue ?? 0) : ""))
                    .font(.system(size: 12)).foregroundStyle(overdue ? Theme.red500 : Theme.slate500)
                Button {
                    Task { await model.pay(statementId: pending.id) }
                } label: {
                    if model.paymentInProgress {
                        ProgressView().tint(.white)
                    } else {
                        Label(L10n.t("statement_pay_card"), systemImage: "creditcard.fill")
                    }
                }
                .buttonStyle(PrimaryButtonStyle())
                .disabled(model.paymentInProgress)
            }
            .card(background: overdue ? Theme.red50 : .white, border: overdue ? Theme.red500 : Theme.slate200)
        } else {
            HStack(spacing: 12) {
                Image(systemName: "checkmark.seal.fill").font(.system(size: 28)).foregroundStyle(Theme.emerald600)
                VStack(alignment: .leading, spacing: 3) {
                    Text(L10n.t("statement_up_to_date")).font(.system(size: 16, weight: .bold)).foregroundStyle(Theme.slate900)
                    Text(L10n.t("statement_up_to_date_body")).font(.system(size: 13)).foregroundStyle(Theme.slate700)
                }
            }
            .card(background: Theme.emerald50, border: Theme.emerald500.opacity(0.35))
        }
    }

    private func historyCard(_ history: [Statement]) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(L10n.t("statement_history")).font(.system(size: 16, weight: .bold)).foregroundStyle(Theme.slate900)
            if history.isEmpty {
                Text(L10n.t("statement_empty")).font(.system(size: 14)).foregroundStyle(Theme.slate500)
            }
            ForEach(history) { item in
                Button {
                    if item.isPaid { receipt = item } else { model.openBreakdown(lineId: item.lineId) }
                } label: {
                    StatementRow(statement: item)
                }
                .buttonStyle(.plain)
                if item.id != history.last?.id { Divider() }
            }
        }
        .card()
    }
}

/// Comprobante de una cuota pagada, para ver y compartir.
private struct ReceiptSheet: View {
    let statement: Statement
    let unit: AccountUnit?
    @Environment(\.dismiss) private var dismiss

    private var lines: [String] {
        var result = [
            L10n.t("receipt_header"),
            L10n.t("receipt_unit", unit?.unitNumber ?? "—", unit?.condominiumName ?? "—"),
            L10n.t("receipt_holder", unit?.holderName ?? "—", Fmt.aliquot(unit?.aliquotPercentage)),
            L10n.t("receipt_period", Fmt.period(statement.period)),
            L10n.t("receipt_concept", statement.concept ?? L10n.t("common_area_fee")),
            L10n.t("receipt_amount", Fmt.pen(statement.amount)),
            L10n.t("receipt_paid_on", Fmt.date(statement.paidAt)),
        ]
        if let method = statement.method, !method.isEmpty { result.append(L10n.t("receipt_method", method)) }
        if let reference = statement.reference, !reference.isEmpty { result.append(L10n.t("receipt_reference", reference)) }
        return result
    }

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 14) {
                    HStack(spacing: 10) {
                        IoBuildLogo(size: 30)
                        Spacer()
                        StatusBadge(status: "paid")
                    }
                    Text(Fmt.pen(statement.amount)).font(.system(size: 32, weight: .heavy)).foregroundStyle(Theme.slate900)
                    ForEach(lines.dropFirst(), id: \.self) { line in
                        Text(line).font(.system(size: 14)).foregroundStyle(Theme.slate700)
                    }
                }
                .card()
                .padding(16)
            }
            .background(Theme.background.ignoresSafeArea())
            .navigationTitle(L10n.t("receipt_title"))
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) { Button(L10n.t("action_close")) { dismiss() } }
                ToolbarItem(placement: .primaryAction) {
                    ShareLink(item: lines.joined(separator: "\n"), subject: Text(L10n.t("receipt_title"))) {
                        Label(L10n.t("receipt_share"), systemImage: "square.and.arrow.up")
                    }
                }
            }
        }
        .presentationDetents([.medium, .large])
    }
}
