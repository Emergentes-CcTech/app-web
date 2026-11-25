import SwiftUI
import Charts

/// "Mi consumo": la liquidación vigente, el consumo de las áreas comunes del edificio y el asistente IA.
struct ConsumptionView: View {
    @Environment(AppModel.self) private var model
    @State private var metric: Metric = .water
    @State private var question = ""

    enum Metric: Hashable { case water, energy }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                TabTopBar()
                ScreenHeader(title: L10n.t("consumption_title"), subtitle: unitSubtitle)
                content
            }
            .padding(16)
        }
        .background(Theme.background.ignoresSafeArea())
        .refreshable { await model.reloadAll() }
        .toolbar(.hidden, for: .navigationBar)
    }

    private var unitSubtitle: String? {
        guard let unit = model.overview?.unit else { return nil }
        return L10n.t("consumption_unit_aliquot", unit.unitNumber ?? "—", Fmt.aliquot(unit.aliquotPercentage))
    }

    @ViewBuilder
    private var content: some View {
        if model.overviewLoading && model.overview == nil {
            LoadingBlock()
        } else if let error = model.overviewError, model.overview == nil {
            ErrorBanner(message: error) { Task { await model.loadOverview() } }
        } else if let overview = model.overview, overview.hasUnit == false {
            Text(L10n.t("consumption_no_unit")).font(.system(size: 15)).foregroundStyle(Theme.slate700).card()
        } else {
            heroCard
            if let consumption = model.overview?.commonConsumption {
                commonSection(consumption)
            }
            aiCard
            recentCard
        }
    }

    // MARK: Liquidación vigente

    private var heroCard: some View {
        let latest = model.overview?.latestStatement
        let status = latest?.normalizedStatus ?? "pending"
        let background = status == "paid" ? Theme.emerald50 : (status == "overdue" ? Theme.red50 : Color.white)
        let border = status == "paid" ? Theme.emerald500 : (status == "overdue" ? Theme.red500 : Theme.slate200)
        return VStack(alignment: .leading, spacing: 10) {
            if let latest {
                HStack {
                    Text(L10n.t("consumption_my_settlement", Fmt.period(latest.period)))
                        .font(.system(size: 13, weight: .medium)).foregroundStyle(Theme.slate700)
                    Spacer()
                    StatusBadge(status: status)
                }
                Text(Fmt.pen(latest.amount))
                    .font(.system(size: 34, weight: .heavy)).foregroundStyle(Theme.slate900).monospacedDigit()
                HStack {
                    Text(latest.isPaid && latest.paidAt != nil
                         ? L10n.t("paid_on", Fmt.date(latest.paidAt))
                         : L10n.t("due_on", Fmt.date(latest.dueDate)))
                        .font(.system(size: 12)).foregroundStyle(Theme.slate500)
                    Spacer()
                    Button {
                        model.openBreakdown(lineId: latest.lineId)
                    } label: {
                        Label(L10n.t("action_view_breakdown"), systemImage: "magnifyingglass")
                            .font(.system(size: 13, weight: .semibold))
                    }
                    .buttonStyle(.borderedProminent)
                    .tint(Theme.emerald600)
                }
            } else {
                Text(L10n.t("consumption_no_settlements")).font(.system(size: 14)).foregroundStyle(Theme.slate500)
            }
        }
        .card(background: background, border: border)
    }

    // MARK: Consumo de áreas comunes

    @ViewBuilder
    private func commonSection(_ consumption: CommonConsumption) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 2) {
                    Text(L10n.t("consumption_common_title")).font(.system(size: 18, weight: .bold)).foregroundStyle(Theme.slate900)
                    Text(L10n.t("consumption_condo_period", model.overview?.condominium?.name ?? L10n.t("condominium"), Fmt.period(consumption.period)))
                        .font(.system(size: 12)).foregroundStyle(Theme.slate500)
                }
                Spacer()
            }
            Picker("", selection: $metric) {
                Text(L10n.t("water")).tag(Metric.water)
                Text(L10n.t("energy")).tag(Metric.energy)
            }
            .pickerStyle(.segmented)

            LazyVGrid(columns: [GridItem(.flexible(), spacing: 10), GridItem(.flexible(), spacing: 10)], spacing: 10) {
                KpiCard(title: L10n.t("consumption_common_water"),
                        value: "\(Fmt.number(consumption.waterM3, digits: 2)) m³",
                        subtitle: change(consumption.waterChangePct),
                        systemImage: "drop.fill", tint: Theme.emerald600, iconBackground: Theme.emerald50)
                KpiCard(title: L10n.t("consumption_common_energy"),
                        value: "\(Fmt.number(consumption.energyKwh, digits: 2)) kWh",
                        subtitle: change(consumption.energyChangePct),
                        systemImage: "bolt.fill", tint: Theme.amber500, iconBackground: Theme.amber50)
                KpiCard(title: L10n.t("consumption_projection"),
                        value: metric == .water ? "\(Fmt.number(consumption.projectedWaterM3)) m³" : "\(Fmt.number(consumption.projectedEnergyKwh)) kWh",
                        subtitle: L10n.t("consumption_projection_hint"),
                        systemImage: "chart.line.uptrend.xyaxis", tint: Theme.teal500, iconBackground: Theme.emerald50)
                KpiCard(title: L10n.t("consumption_monitored"),
                        value: "\(consumption.monitoredAreasCount ?? 0) / \(consumption.totalAreasCount ?? 0)",
                        subtitle: L10n.t("consumption_monitored_hint"),
                        systemImage: "sensor.fill", tint: Theme.emerald700, iconBackground: Theme.emerald50)
            }

            if let series = consumption.dailySeries, !series.isEmpty {
                dailyChart(series)
            }

            if let areas = consumption.areas, !areas.isEmpty {
                VStack(alignment: .leading, spacing: 12) {
                    Text(L10n.t("consumption_area_detail", metric == .water ? "m³" : "kWh"))
                        .font(.system(size: 15, weight: .bold)).foregroundStyle(Theme.slate900)
                    ForEach(areas) { area in
                        AreaRow(area: area, metric: metric)
                        if area.id != areas.last?.id { Divider() }
                    }
                    Text(L10n.t("consumption_area_note")).font(.system(size: 11)).foregroundStyle(Theme.slate500)
                }
                .card()
            }
        }
    }

    private func dailyChart(_ series: [DailyPoint]) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(L10n.t("consumption_daily")).font(.system(size: 15, weight: .bold)).foregroundStyle(Theme.slate900)
            Chart(series) { point in
                BarMark(
                    x: .value("Día", point.dayLabel ?? String(point.date.suffix(2))),
                    y: .value("Valor", metric == .water ? (point.waterM3 ?? 0) : (point.energyKwh ?? 0))
                )
                .foregroundStyle(metric == .water ? Theme.emerald500 : Theme.amber500)
                .cornerRadius(3)
            }
            .chartXAxis {
                AxisMarks(values: .automatic(desiredCount: 6))
            }
            .frame(height: 160)
        }
        .card()
    }

    private func change(_ pct: Double?) -> String {
        guard let pct else { return L10n.t("consumption_no_previous") }
        return L10n.t("consumption_vs_previous", Fmt.signedPercent(pct))
    }

    // MARK: Asistente IA

    private var aiCard: some View {
        VStack(alignment: .leading, spacing: 10) {
            Label {
                Text(L10n.t("ai_title")).font(.system(size: 16, weight: .bold)).foregroundStyle(Theme.slate900)
            } icon: {
                Image(systemName: "sparkles").foregroundStyle(Theme.emerald600)
            }
            Text(L10n.t("ai_subtitle")).font(.system(size: 12)).foregroundStyle(Theme.slate500)

            ForEach(["ai_suggestion_change", "ai_suggestion_share", "ai_suggestion_leak"], id: \.self) { key in
                Button {
                    question = L10n.t(key)
                    Task { await model.askAI(question) }
                } label: {
                    Text(L10n.t(key))
                        .font(.system(size: 12, weight: .medium))
                        .foregroundStyle(Theme.emerald700)
                        .multilineTextAlignment(.leading)
                        .padding(.horizontal, 12).padding(.vertical, 8)
                        .background(Theme.emerald50, in: RoundedRectangle(cornerRadius: 10, style: .continuous))
                }
                .buttonStyle(.plain)
                .disabled(model.aiLoading)
            }

            HStack(spacing: 8) {
                TextField(L10n.t("ai_placeholder"), text: $question)
                    .font(.system(size: 14))
                    .padding(.horizontal, 12)
                    .frame(minHeight: 42)
                    .background(Theme.slate100, in: RoundedRectangle(cornerRadius: 10, style: .continuous))
                    .submitLabel(.send)
                    .onSubmit { Task { await model.askAI(question) } }
                Button {
                    Task { await model.askAI(question) }
                } label: {
                    Group {
                        if model.aiLoading { ProgressView().tint(.white) } else { Image(systemName: "paperplane.fill") }
                    }
                    .frame(width: 42, height: 42)
                    .foregroundStyle(.white)
                    .background(Theme.emerald600, in: RoundedRectangle(cornerRadius: 10, style: .continuous))
                }
                .disabled(model.aiLoading || question.isBlank)
                .accessibilityLabel(L10n.t("action_ask"))
            }

            if let error = model.aiError {
                Text(error).font(.system(size: 12)).foregroundStyle(Theme.red500)
            }

            if let answer = model.aiAnswer {
                VStack(alignment: .leading, spacing: 8) {
                    if let period = answer.period, !period.isEmpty {
                        Text(L10n.t("ai_period", Fmt.period(period))).font(.system(size: 11, weight: .bold)).foregroundStyle(Theme.emerald700)
                    }
                    if let summary = answer.summary, !summary.isEmpty {
                        Text(summary).font(.system(size: 13, weight: .semibold)).foregroundStyle(Theme.slate900)
                    }
                    if let water = answer.waterAnalysis, !water.isEmpty {
                        Text(L10n.t("ai_water", water)).font(.system(size: 12)).foregroundStyle(Theme.slate700)
                    }
                    if let energy = answer.energyAnalysis, !energy.isEmpty {
                        Text(L10n.t("ai_energy", energy)).font(.system(size: 12)).foregroundStyle(Theme.slate700)
                    }
                    if let impact = answer.aliquotImpact, !impact.isEmpty {
                        Text(L10n.t("ai_aliquot", impact)).font(.system(size: 12)).foregroundStyle(Theme.slate700)
                    }
                    if let recommendations = answer.recommendations, !recommendations.isEmpty {
                        Text(L10n.t("ai_recommendations")).font(.system(size: 12, weight: .bold)).foregroundStyle(Theme.slate800)
                        ForEach(recommendations, id: \.self) { item in
                            Text("– " + item).font(.system(size: 12)).foregroundStyle(Theme.slate700)
                        }
                    }
                }
                .padding(14)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background(Theme.slate100, in: RoundedRectangle(cornerRadius: 12, style: .continuous))
            }
        }
        .card(border: Theme.emerald500.opacity(0.35))
    }

    // MARK: Últimas liquidaciones

    @ViewBuilder
    private var recentCard: some View {
        if let recent = model.overview?.recentStatements, !recent.isEmpty {
            VStack(alignment: .leading, spacing: 10) {
                HStack {
                    Text(L10n.t("consumption_recent")).font(.system(size: 16, weight: .bold)).foregroundStyle(Theme.slate900)
                    Spacer()
                    Button(L10n.t("statement_title")) { model.selectedTab = .payments }
                        .font(.system(size: 13, weight: .semibold))
                }
                ForEach(recent) { item in
                    Button { model.openBreakdown(lineId: item.lineId) } label: { StatementRow(statement: item) }
                        .buttonStyle(.plain)
                    if item.id != recent.last?.id { Divider() }
                }
            }
            .card()
        }
    }
}

private struct AreaRow: View {
    let area: AreaConsumption
    let metric: ConsumptionView.Metric

    var body: some View {
        let value = metric == .water ? area.waterM3 : area.energyKwh
        let share = metric == .water ? area.waterSharePct : area.energySharePct
        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: 2) {
                    Text(area.name ?? "—").font(.system(size: 14, weight: .semibold)).foregroundStyle(Theme.slate900)
                    if let location = area.location, !location.isEmpty {
                        Text(location).font(.system(size: 11)).foregroundStyle(Theme.slate500)
                    }
                }
                Spacer()
                Text("\(Fmt.number(value, digits: 2)) \(metric == .water ? "m³" : "kWh")")
                    .font(.system(size: 14, weight: .bold)).foregroundStyle(Theme.slate900).monospacedDigit()
            }
            ProgressView(value: min(max((share ?? 0) / 100, 0), 1))
                .tint(metric == .water ? Theme.emerald500 : Theme.amber500)
            HStack {
                meterStatus
                Spacer()
                Text("\(Fmt.number(share)) %").font(.system(size: 11)).foregroundStyle(Theme.slate500)
            }
        }
    }

    @ViewBuilder
    private var meterStatus: some View {
        if area.monitored != true {
            Label(L10n.t("meter_none"), systemImage: "minus.circle").foregroundStyle(Theme.slate500)
                .font(.system(size: 11, weight: .medium))
        } else if (area.metersOnline ?? 0) > 0 {
            Label(L10n.t("meter_online"), systemImage: "dot.radiowaves.left.and.right").foregroundStyle(Theme.emerald700)
                .font(.system(size: 11, weight: .medium))
        } else {
            Label(L10n.t("meter_offline"), systemImage: "wifi.slash").foregroundStyle(Theme.red500)
                .font(.system(size: 11, weight: .medium))
        }
    }
}
