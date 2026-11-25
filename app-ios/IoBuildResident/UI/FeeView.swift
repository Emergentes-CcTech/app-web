import SwiftUI

/// "Mi cuota": explica en tres pasos de dónde sale lo que paga el propietario.
struct FeeView: View {
    @Environment(AppModel.self) private var model

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                TabTopBar()
                HStack(alignment: .top) {
                    ScreenHeader(title: L10n.t("fee_title"), subtitle: L10n.t("fee_subtitle"))
                    monthPicker
                }
                content
            }
            .padding(16)
        }
        .background(Theme.background.ignoresSafeArea())
        .refreshable { await model.loadFee(model.selectedLineId) }
        .toolbar(.hidden, for: .navigationBar)
    }

    @ViewBuilder
    private var monthPicker: some View {
        if let lines = model.fee?.availableLines, lines.count > 1 {
            Menu {
                ForEach(lines) { line in
                    Button {
                        Task { await model.loadFee(line.lineId) }
                    } label: {
                        if line.lineId == model.selectedLineId {
                            Label(Fmt.period(line.period), systemImage: "checkmark")
                        } else {
                            Text(Fmt.period(line.period))
                        }
                    }
                }
            } label: {
                Label(L10n.t("fee_pick_month"), systemImage: "calendar")
                    .font(.system(size: 13, weight: .semibold))
            }
        }
    }

    @ViewBuilder
    private var content: some View {
        if model.feeLoading && model.fee == nil {
            LoadingBlock()
        } else if let error = model.feeError, model.fee == nil {
            ErrorBanner(message: error) { Task { await model.loadFee(model.selectedLineId) } }
        } else if let explanation = model.fee?.explanation {
            FeeDetail(explanation: explanation, isLoading: model.feeLoading)
        } else {
            Text(L10n.t("fee_empty")).font(.system(size: 15)).foregroundStyle(Theme.slate500).card()
        }
    }
}

private struct FeeDetail: View {
    @Environment(AppModel.self) private var model
    let explanation: FeeExplanation
    let isLoading: Bool

    private var month: String { Fmt.period(explanation.period) }
    private var common: CommonExpenses? { explanation.common }
    private var mine: MyExpenses? { explanation.mine }
    private var status: String { (explanation.status ?? "pending").lowercased() }

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            hero.opacity(isLoading ? 0.6 : 1)
            stepOne
            stepTwo
            stepThree
            comparison
            alerts
            sources
            glossary
            Button {
                model.selectedTab = .consumption
            } label: {
                Label(L10n.t("fee_ask_ai"), systemImage: "sparkles")
                    .font(.system(size: 14, weight: .semibold))
                    .frame(maxWidth: .infinity)
            }
            .buttonStyle(.bordered)
            .tint(Theme.emerald600)
        }
    }

    private var hero: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Text(L10n.t("fee_your_fee", month, explanation.unitNumber ?? "—"))
                    .font(.system(size: 13, weight: .medium)).foregroundStyle(Theme.slate700)
                Spacer()
                StatusBadge(status: status)
            }
            Text(Fmt.pen(mine?.total)).font(.system(size: 34, weight: .heavy)).foregroundStyle(Theme.slate900).monospacedDigit()
            Text(status == "paid" && explanation.paidAt != nil
                 ? L10n.t("paid_on", Fmt.date(explanation.paidAt))
                 : L10n.t("due_on", Fmt.date(explanation.dueDate)))
                .font(.system(size: 12)).foregroundStyle(Theme.slate500)
            if status != "paid" {
                Button(L10n.t("fee_go_pay")) { model.selectedTab = .payments }
                    .buttonStyle(PrimaryButtonStyle())
            }
        }
        .card(background: status == "paid" ? Theme.emerald50 : .white, border: status == "paid" ? Theme.emerald500 : Theme.slate200)
    }

    private var stepOne: some View {
        StepCard(number: 1, title: L10n.t("fee_step1_title", month), message: L10n.t("fee_step1_body")) {
            ValueRow(label: L10n.t("fee_water_bill"), value: Fmt.pen(common?.water))
            ValueRow(label: L10n.t("fee_power_bill"), value: Fmt.pen(common?.energy))
            if (common?.other ?? 0) > 0 {
                ValueRow(label: L10n.t("fee_other"), value: Fmt.pen(common?.other))
            }
            Divider()
            ValueRow(label: L10n.t("fee_total_common"), value: Fmt.pen(common?.total), emphasized: true)
        }
    }

    private var stepTwo: some View {
        let aliquot = Fmt.aliquot(explanation.aliquot)
        return StepCard(number: 2, title: L10n.t("fee_step2_title", aliquot),
                        message: L10n.t("fee_step2_body", Fmt.pen(explanation.aliquot))) {
            ProgressView(value: min(max((explanation.aliquot ?? 0) / 100, 0), 1))
                .tint(Theme.emerald600)
        }
    }

    private var stepThree: some View {
        let total = mine?.total ?? 0
        func share(_ value: Double?) -> String { total > 0 ? Fmt.number((value ?? 0) / total * 100) : "0" }
        return StepCard(number: 3, title: L10n.t("fee_step3_title"), message: nil) {
            ValueRow(label: L10n.t("fee_share_water", share(mine?.water)), value: Fmt.pen(mine?.water))
            ValueRow(label: L10n.t("fee_share_power", share(mine?.energy)), value: Fmt.pen(mine?.energy))
            if (mine?.other ?? 0) > 0 {
                ValueRow(label: L10n.t("fee_share_other", share(mine?.other)), value: Fmt.pen(mine?.other))
            }
            Divider()
            ValueRow(label: L10n.t("fee_yours"), value: Fmt.pen(total), emphasized: true)
        }
    }

    private var comparison: some View {
        let previousMonth = Fmt.period(explanation.previousPeriod)
        return VStack(alignment: .leading, spacing: 10) {
            Text(L10n.t("fee_compare_with", previousMonth)).font(.system(size: 15, weight: .bold)).foregroundStyle(Theme.slate900)
            if let previous = explanation.previous {
                compareRow(L10n.t("water"), before: previous.waterSoles, now: mine?.water)
                compareRow(L10n.t("energy"), before: previous.energySoles, now: mine?.energy)
                if (previous.otherSoles ?? 0) > 0 || (mine?.other ?? 0) > 0 {
                    compareRow(L10n.t("fee_other"), before: previous.otherSoles, now: mine?.other)
                }
                Divider()
                let delta = (mine?.total ?? 0) - (previous.totalSoles ?? 0)
                ValueRow(label: L10n.t("fee_difference", (delta > 0 ? "+" : "") + Fmt.pen(delta)),
                         value: Fmt.pen(mine?.total), emphasized: true)
            } else {
                Text(L10n.t("fee_no_previous", previousMonth)).font(.system(size: 13)).foregroundStyle(Theme.slate500)
            }
        }
        .card()
    }

    private func compareRow(_ label: String, before: Double?, now: Double?) -> some View {
        HStack {
            Text(label).font(.system(size: 14)).foregroundStyle(Theme.slate700)
            Spacer()
            Text(L10n.t("fee_previous_now", Fmt.pen(before), Fmt.pen(now)))
                .font(.system(size: 12, weight: .medium)).foregroundStyle(Theme.slate700).monospacedDigit()
        }
    }

    private var alerts: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(L10n.t("fee_alerts_title", month)).font(.system(size: 15, weight: .bold)).foregroundStyle(Theme.slate900)
            let items = explanation.alerts ?? []
            if items.isEmpty {
                Label(L10n.t("fee_no_alerts"), systemImage: "checkmark.circle")
                    .font(.system(size: 13)).foregroundStyle(Theme.emerald700)
            }
            ForEach(items, id: \.self) { alert in
                VStack(alignment: .leading, spacing: 4) {
                    HStack {
                        Text(alert.title ?? alert.alertType ?? "—").font(.system(size: 14, weight: .semibold)).foregroundStyle(Theme.slate900)
                        Spacer()
                        Text(L10n.t(alert.isOpen == true ? "fee_alert_open" : "fee_alert_resolved"))
                            .font(.system(size: 11, weight: .bold))
                            .foregroundStyle(alert.isOpen == true ? Theme.amber500 : Theme.emerald700)
                    }
                    Text(L10n.t("fee_alert_cost", Fmt.pen(alert.overcost), Fmt.pen(alert.myShare)))
                        .font(.system(size: 12)).foregroundStyle(Theme.slate500)
                }
                .padding(12)
                .background(Theme.amber50, in: RoundedRectangle(cornerRadius: 12, style: .continuous))
            }
        }
        .card()
    }

    private var sources: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(L10n.t("fee_sources_title")).font(.system(size: 15, weight: .bold)).foregroundStyle(Theme.slate900)
            Text(L10n.t("fee_sources_body", Fmt.number(common?.waterM3, digits: 2), Fmt.number(common?.energyKwh, digits: 2)))
                .font(.system(size: 13)).foregroundStyle(Theme.slate700)
            let water = common?.waterReceiptUrl.flatMap(URL.init(string:))
            let power = common?.energyReceiptUrl.flatMap(URL.init(string:))
            if water == nil && power == nil {
                Text(L10n.t("fee_no_bills")).font(.system(size: 12)).foregroundStyle(Theme.slate500)
            }
            if let water {
                Link(destination: water) { Label(L10n.t("fee_view_water_bill"), systemImage: "doc.richtext") }
                    .font(.system(size: 14, weight: .semibold))
            }
            if let power {
                Link(destination: power) { Label(L10n.t("fee_view_power_bill"), systemImage: "doc.richtext") }
                    .font(.system(size: 14, weight: .semibold))
            }
        }
        .card()
    }

    private var glossary: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(L10n.t("fee_glossary_title")).font(.system(size: 15, weight: .bold)).foregroundStyle(Theme.slate900)
            ForEach([("glossary_aliquot", "glossary_aliquot_def"),
                     ("glossary_common", "glossary_common_def"),
                     ("glossary_master_bill", "glossary_master_bill_def")], id: \.0) { entry in
                DisclosureGroup {
                    Text(L10n.t(entry.1)).font(.system(size: 13)).foregroundStyle(Theme.slate700)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(.top, 4)
                } label: {
                    Text(L10n.t(entry.0)).font(.system(size: 14, weight: .semibold)).foregroundStyle(Theme.slate900)
                }
                .tint(Theme.slate500)
            }
        }
        .card()
    }
}

/// Paso numerado de la explicación de la cuota.
private struct StepCard<Content: View>: View {
    let number: Int
    let title: String
    let message: String?
    @ViewBuilder let content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(alignment: .top, spacing: 10) {
                Text("\(number)")
                    .font(.system(size: 13, weight: .heavy))
                    .foregroundStyle(.white)
                    .frame(width: 26, height: 26)
                    .background(Theme.emerald600, in: Circle())
                VStack(alignment: .leading, spacing: 4) {
                    Text(title).font(.system(size: 15, weight: .bold)).foregroundStyle(Theme.slate900)
                    if let message {
                        Text(message).font(.system(size: 13)).foregroundStyle(Theme.slate700)
                    }
                }
            }
            content
        }
        .card()
    }
}
