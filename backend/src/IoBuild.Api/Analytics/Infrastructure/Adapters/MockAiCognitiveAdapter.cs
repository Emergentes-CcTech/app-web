using System.Globalization;
using IoBuild.Api.Analytics.Application.Internal.QueryServices;
using IoBuild.Api.Analytics.Domain.Services;
using IoBuild.Api.Persistence;

namespace IoBuild.Api.Analytics.Infrastructure.Adapters;

/// <summary>
/// Explicación determinística del consumo del periodo: compara con el mes anterior, el recibo matriz, las alertas
/// del motor de reglas y la cuota del departamento (Ley N.° 27157). También es el respaldo del adaptador de Gemini,
/// por eso responde en el idioma de la interfaz (español o inglés).
/// </summary>
public sealed class MockAiCognitiveAdapter(IoBuildDbContext db) : IAiCognitiveService
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-PE");
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo Num = CultureInfo.InvariantCulture;

    public Task<ConsumptionFacts> BuildFactsAsync(int projectId, int? unitId, string? billingPeriod) =>
        new ConsumptionFactsBuilder(db).BuildAsync(projectId, unitId, billingPeriod);

    public Task<AssistantContext> BuildContextAsync(int projectId, int? unitId, string period) =>
        new AssistantContextBuilder(db).BuildAsync(projectId, unitId, period);

    public async Task<AiExplanationDto> ExplainConsumptionVariationAsync(int projectId, int? unitId, string billingPeriod, string question, string language = "es")
    {
        var facts = await BuildFactsAsync(projectId, unitId, billingPeriod);
        return Answer(question, facts, await BuildContextAsync(projectId, unitId, facts.Period), language);
    }

    /// <summary>
    /// Explicación del periodo con el resumen enfocado en la pregunta: vencimiento y deuda, historial de pagos,
    /// consumo por área o cómo se calcula la cuota. Si la pregunta no encaja en ninguno, explica la variación del mes.
    /// </summary>
    public static AiExplanationDto Answer(string question, ConsumptionFacts f, AssistantContext c, string language = "es")
    {
        var english = language == "en";
        var baseline = Render(f, language);
        var advice = ImprovementAdvice(question, f, c, english);
        if (advice is not null) return baseline with { Summary = advice.Value.Summary, Recommendations = advice.Value.Actions };
        var direct = DirectAnswer(question, f, c, english);
        if (direct is not null) return baseline with { Summary = direct };
        // Pregunta abierta que las reglas no entienden: se dice con honestidad en vez de responder otra cosa
        if (!IsVariationQuestion(question))
            return baseline with { Summary = (english
                ? "Without the AI assistant I can only answer questions about debts and due dates, payment history, consumption by area, how the fee is calculated and how to reduce costs. Here is the month's summary: "
                : "Sin el asistente de IA solo puedo responder sobre deudas y vencimientos, historial de pagos, consumo por área, cómo se calcula la cuota y cómo reducir gastos. Este es el resumen del mes: ") + baseline.Summary };
        return baseline;
    }

    // Las preguntas sugeridas piden explicar la variación del mes: esa es la explicación base
    private static bool IsVariationQuestion(string question)
    {
        var q = Normalize(question);
        return string.IsNullOrWhiteSpace(q) || new[] { "vari", "cambi", "subi", "bajo", "aument", "por que", "explica", "alerta", "fuga", "costos extra", "mantenimiento", "why", "change", "explain", "alert", "leak", "maintenance" }.Any(q.Contains);
    }

    /// <summary>
    /// Qué mejorar para bajar el gasto común (o responder a quejas de los propietarios): acciones concretas a partir
    /// del consumo por área, las alertas abiertas, la diferencia con el recibo matriz y la tendencia frente al mes anterior.
    /// </summary>
    private static (string Summary, List<string> Actions)? ImprovementAdvice(string question, ConsumptionFacts f, AssistantContext c, bool english)
    {
        string L(string es, string en) => english ? en : es;
        var q = Normalize(question);
        if (!new[] { "mejor", "reduc", "ahorr", "quej", "bajar", "optimiz", "recomend", "consejo", "que hacer", "que podemos", "que podriamos", "caro", "elevad",
                "improve", "reduce", "save", "complain", "lower", "cheaper", "advice", "what can we" }.Any(q.Contains))
            return null;

        var findings = new List<string>();
        var actions = new List<string>();

        var open = c.OpenAlerts;
        if (open.Count > 0)
        {
            var cost = open.Sum(a => a.EstimatedOvercostSoles);
            findings.Add(L($"hay {open.Count} alerta(s) abierta(s) con un sobrecosto estimado de {Pen(cost)}", $"there are {open.Count} open alert(s) with an estimated overcost of {Pen(cost)}"));
            actions.Add(L($"Atender las {open.Count} alertas abiertas: es el ahorro más directo ({Pen(cost)} estimados).", $"Resolve the {open.Count} open alerts: it is the most direct saving ({Pen(cost)} estimated)."));
        }

        var measured = c.Areas.Where(a => a.Meters > 0 && a.EnergyKwh > 0).OrderByDescending(a => a.EnergyKwh).ToList();
        var totalEnergy = measured.Sum(a => a.EnergyKwh);
        if (measured.Count > 0 && totalEnergy > 0)
        {
            var top = measured[0];
            var share = Math.Round(top.EnergyKwh / totalEnergy * 100, 0);
            findings.Add(L($"{top.Name} concentra el {share.ToString(Num)} % de la energía medida ({Kwh(top.EnergyKwh)})", $"{top.Name} accounts for {share.ToString(Num)}% of the measured energy ({Kwh(top.EnergyKwh)})"));
            actions.Add(top.Subsystem switch
            {
                "PressurePumpsRoom" => L("Revisar presostatos y alternancia de las bombas: suelen arrancar más de lo necesario.", "Check the pressure switches and pump rotation: they often start more than needed."),
                "ElevatorsBank" => L("Programar el modo de ahorro de los ascensores en horas de poco uso y revisar su mantenimiento.", "Schedule the elevators' energy-saving mode in off-peak hours and check their maintenance."),
                "GeneralElectricalPanel" => L("Pasar la iluminación común a LED con sensores de presencia o temporizadores.", "Switch common lighting to LED with motion sensors or timers."),
                _ => L($"Revisar el uso y mantenimiento de {top.Name}, que es lo que más energía consume.", $"Review the use and maintenance of {top.Name}, the biggest energy consumer.")
            });
        }

        if (f.PreviousConsumption.WaterM3 > 0 && f.Consumption.WaterM3 > f.PreviousConsumption.WaterM3 * 1.1 && !f.PeriodInProgress)
        {
            findings.Add(L($"el agua común subió frente a {MonthName(f.PreviousPeriod, false)}", $"common water rose compared with {MonthName(f.PreviousPeriod, true)}"));
            actions.Add(L("Revisar de madrugada si corre agua en la cisterna, boyas y montantes.", "Check overnight whether water runs in the cistern, floats and risers."));
        }

        if (f.Bill is not null && f.Bill.WaterM3 > 0 && f.Bill.WaterLossM3 / f.Bill.WaterM3 > 0.10m)
        {
            var pct = Math.Round(f.Bill.WaterLossM3 / f.Bill.WaterM3 * 100, 0);
            findings.Add(L($"el recibo de Sedapal factura {pct.ToString(Num)} % más agua de la que miden los sensores", $"the Sedapal bill charges {pct.ToString(Num)}% more water than the sensors measure"));
            actions.Add(L("Buscar fugas o consumos fuera de lo medido: la diferencia con el recibo supera el 10 %.", "Look for leaks or unmetered use: the gap with the bill exceeds 10%."));
        }

        // Ante quejas, la transparencia también baja el conflicto aunque el gasto sea correcto
        if (q.Contains("quej") || q.Contains("complain"))
            actions.Add(L("Compartir con los propietarios la explicación de su cuota y los recibos escaneados: en «Mi cuota actual» ven de dónde sale cada sol.",
                "Share each owner's fee explanation and the scanned bills with them: in «My current fee» they see where every sol goes."));
        if (actions.Count < 2)
            actions.Add(L("Mantener la revisión mensual de la cisterna y la alternancia de las bombas.", "Keep up the monthly cistern check and the pump rotation."));

        var summary = findings.Count == 0
            ? L($"En {MonthName(f.Period, false)} no veo un gasto fuera de lo normal: no hay alertas abiertas ni un área que se dispare. Estas medidas suelen bajar el gasto común:",
                $"In {MonthName(f.Period, true)} I see no unusual spending: no open alerts and no area spiking. These measures usually lower the common expense:")
            : L($"Para bajar el gasto común conviene empezar por lo que muestran los datos de {MonthName(f.Period, false)}: {string.Join("; ", findings)}.",
                $"To lower the common expense, start with what the {MonthName(f.Period, true)} data shows: {string.Join("; ", findings)}.");
        return (summary, actions.Take(5).ToList());
    }

    private static string? DirectAnswer(string question, ConsumptionFacts f, AssistantContext c, bool english)
    {
        string L(string es, string en) => english ? en : es;
        var q = Normalize(question);
        bool Has(params string[] words) => words.Any(w => q.Contains(w));

        if (Has("vence", "vencimiento", "debo", "deuda", "pendiente", "saldo", "atrasad", "moros", "due", "owe", "debt", "overdue", "balance"))
        {
            if (c.Unit is null) return L("Esa consulta aplica a un departamento: elige uno para ver su deuda.", "That question applies to a unit: pick one to see its balance.");
            var pending = c.FeeHistory.Where(h => h.Status != "Paid").OrderBy(h => h.Period).ToList();
            if (c.FeeHistory.Count == 0) return L($"El Departamento {c.Unit.Number} aún no tiene cuotas emitidas, así que no tiene deuda.", $"Unit {c.Unit.Number} has no fees issued yet, so it owes nothing.");
            if (pending.Count == 0)
            {
                var last = c.FeeHistory[0];
                return L($"No tiene cuotas pendientes. Su última cuota, de {MonthName(last.Period, false)}, fue {Pen(last.TotalSoles)} y está pagada.",
                    $"You have no pending fees. Your latest fee, for {MonthName(last.Period, true)}, was {Pen(last.TotalSoles)} and is paid.");
            }
            var oldest = pending[0];
            var verb = oldest.Status == "Overdue" ? L("venció", "was due") : L("vence", "is due");
            return L($"Debe {Pen(c.OutstandingSoles)} en {pending.Count} cuota(s). La más antigua, de {MonthName(oldest.Period, false)} ({Pen(oldest.BalanceSoles)}), {verb} el {oldest.DueDate}.",
                $"You owe {Pen(c.OutstandingSoles)} across {pending.Count} fee(s). The oldest, for {MonthName(oldest.Period, true)} ({Pen(oldest.BalanceSoles)}), {verb} on {oldest.DueDate}.");
        }

        if (Has("historial", "pagado", "pague", "he pagado", "total", "ultimos", "ultimas", "meses", "este ano", "history", "paid", "last months", "so far"))
        {
            if (c.Unit is null || c.FeeHistory.Count == 0) return L("Aún no hay cuotas emitidas para este departamento.", "No fees have been issued for this unit yet.");
            var items = c.FeeHistory.Take(6).ToList();
            var list = string.Join("; ", items.Select(h => $"{MonthName(h.Period, english)} {Pen(h.TotalSoles)} ({StatusName(h.Status, english)})"));
            var paidTotal = items.Where(h => h.Status == "Paid").Sum(h => h.TotalSoles);
            return L($"En los últimos {items.Count} mes(es) sus cuotas suman {Pen(items.Sum(h => h.TotalSoles))}, de los cuales pagó {Pen(paidTotal)}: {list}.",
                $"Over the last {items.Count} month(s) your fees add up to {Pen(items.Sum(h => h.TotalSoles))}, of which you paid {Pen(paidTotal)}: {list}.");
        }

        if (Has("area", "ascensor", "bomba", "cisterna", "tablero", "mas consume", "consume mas", "mayor consumo", "elevator", "pump", "cistern", "panel", "consumes the most"))
        {
            var measured = c.Areas.Where(a => a.Meters > 0).ToList();
            if (measured.Count == 0 || measured.All(a => a.WaterM3 <= 0 && a.EnergyKwh <= 0))
                return L($"Aún no hay lecturas por área común en {MonthName(f.Period, false)}.", $"There are no readings per common area for {MonthName(f.Period, true)} yet.");
            var parts = new List<string>();
            var topEnergy = measured.OrderByDescending(a => a.EnergyKwh).First();
            if (topEnergy.EnergyKwh > 0) parts.Add(L($"la que más energía consume es {topEnergy.Name} ({Kwh(topEnergy.EnergyKwh)})", $"the one using the most energy is {topEnergy.Name} ({Kwh(topEnergy.EnergyKwh)})"));
            var topWater = measured.OrderByDescending(a => a.WaterM3).First();
            if (topWater.WaterM3 > 0) parts.Add(L($"la que más agua registra es {topWater.Name} ({M3(topWater.WaterM3)})", $"the one recording the most water is {topWater.Name} ({M3(topWater.WaterM3)})"));
            var detail = string.Join("; ", measured.Select(a => $"{a.Name}: {M3(a.WaterM3)} · {Kwh(a.EnergyKwh)}"));
            var progress = f.PeriodInProgress ? L(" (mes en curso)", " (month in progress)") : string.Empty;
            return L($"En {MonthName(f.Period, false)}{progress}, entre las áreas comunes {string.Join(" y ", parts)}. Detalle: {detail}.",
                $"In {MonthName(f.Period, true)}{progress}, among the common areas {string.Join(" and ", parts)}. Detail: {detail}.");
        }

        if (Has("alicuota", "participacion", "como se calcula", "calcul", "prorrate", "formula", "porcentaje", "aliquot", "share", "calculated", "how is"))
        {
            var latest = c.BuildingExpenses.FirstOrDefault();
            if (c.Unit is null)
                return L("Cada cuota es el gasto común del mes multiplicado por la participación del departamento (Ley N.° 27157); las participaciones suman 100 %.",
                    "Each fee is the month's common expense times the unit's share (Law 27157); all shares add up to 100%.");
            var pct = c.Unit.ParticipationPct.ToString("F4", Num);
            var example = latest is null
                ? L($" Por ejemplo, si el gasto común fuera S/ 10,000.00, su cuota sería {Pen(10000m * c.Unit.ParticipationPct / 100m)}.", $" For example, if the common expense were S/ 10,000.00, your fee would be {Pen(10000m * c.Unit.ParticipationPct / 100m)}.")
                : L($" En {MonthName(latest.Period, false)} el gasto común fue {Pen(latest.TotalSoles)}, así que le correspondió {Pen(latest.TotalSoles * c.Unit.ParticipationPct / 100m)}.",
                    $" In {MonthName(latest.Period, true)} the common expense was {Pen(latest.TotalSoles)}, so your share was {Pen(latest.TotalSoles * c.Unit.ParticipationPct / 100m)}.");
            return L($"Su cuota es el gasto común del mes (recibos de agua y luz de las áreas comunes más otros gastos) multiplicado por su participación de {pct} %, definida en el Reglamento Interno.{example}",
                $"Your fee is the month's common expense (common-area water and electricity bills plus other expenses) times your {pct}% share, set in the building's bylaws.{example}");
        }

        return null;
    }

    private static string StatusName(string status, bool english) => status switch
    {
        "Paid" => english ? "paid" : "pagada",
        "Overdue" => english ? "overdue" : "vencida",
        _ => english ? "pending" : "pendiente"
    };

    // Minúsculas y sin tildes para reconocer la intención de la pregunta
    private static string Normalize(string text)
    {
        var formD = (text ?? string.Empty).ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        return new string(formD.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    public static AiExplanationDto Render(ConsumptionFacts f, string language = "es")
    {
        var english = language == "en";
        string L(string es, string en) => english ? en : es;

        var month = MonthName(f.Period, english);
        var previousMonth = MonthName(f.PreviousPeriod, english);
        var unitLabel = f.UnitNumber is null ? string.Empty : L($" para el Departamento {f.UnitNumber}", $" for Unit {f.UnitNumber}");
        var progress = f.PeriodInProgress
            ? L($" (mes en curso, {f.Consumption.DaysWithData} días con lecturas)", $" (month in progress, {f.Consumption.DaysWithData} days with readings)")
            : string.Empty;
        var activeAlerts = f.Alerts.Count(a => a.Status == "Active");
        var threshold = f.NightFlowThresholdM3H.ToString("F2", Num);

        // Resumen: primero la cuota (lo que pregunta el residente), luego qué la movió
        var summary = new List<string> { $"{f.ProjectName}{unitLabel}, {month}{progress}." };
        if (f.UnitNumber is not null)
        {
            if (f.Statement is null)
                summary.Add(L($"Aún no se emitió la cuota de {month} para el Departamento {f.UnitNumber}.", $"The {month} fee for Unit {f.UnitNumber} has not been issued yet."));
            else if (f.PreviousStatement is null)
                summary.Add(L($"Su cuota fue {Pen(f.Statement.TotalSoles)}; no hay cuota de {previousMonth} para comparar.", $"Your fee was {Pen(f.Statement.TotalSoles)}; there is no {previousMonth} fee to compare with."));
            else
            {
                var change = Change(f.Statement.TotalSoles, f.PreviousStatement.TotalSoles, english);
                var driver = MainDriver(f.Statement, f.PreviousStatement, english);
                summary.Add(L($"Su cuota fue {Pen(f.Statement.TotalSoles)} frente a {Pen(f.PreviousStatement.TotalSoles)} en {previousMonth} ({change}), principalmente por {driver}.",
                    $"Your fee was {Pen(f.Statement.TotalSoles)} compared with {Pen(f.PreviousStatement.TotalSoles)} in {previousMonth} ({change}), mainly because of {driver}."));
            }
        }
        summary.Add(f.HasActiveAnomalies
            ? L($"Hay {activeAlerts} alerta(s) activa(s) con un sobrecosto estimado de {Pen(f.ActiveOvercostSoles)}.", $"There are {activeAlerts} active alert(s) with an estimated overcost of {Pen(f.ActiveOvercostSoles)}.")
            : L("No hay alertas activas en el periodo.", "There are no active alerts in this period."));

        var waterVersus = Versus(f.Consumption.WaterM3, f.PreviousConsumption.WaterM3, previousMonth, M3, english);
        var water = new List<string> { L($"Agua de áreas comunes medida por IoBuild: {M3(f.Consumption.WaterM3)}{waterVersus}.", $"Common-area water measured by IoBuild: {M3(f.Consumption.WaterM3)}{waterVersus}.") };
        if (f.Bill is not null)
            water.Add(L($"Recibo matriz de Sedapal: {M3((double)f.Bill.WaterM3)} por {Pen(f.Bill.WaterSoles)}; diferencia entre lo facturado y lo medido: {M3((double)f.Bill.WaterLossM3)}.",
                $"Sedapal master bill: {M3((double)f.Bill.WaterM3)} for {Pen(f.Bill.WaterSoles)}; difference between billed and measured: {M3((double)f.Bill.WaterLossM3)}."));
        var leaks = f.Alerts.Where(a => a.Type == "NocturnalWaterLeak").ToList();
        var leakCost = Pen(leaks.Sum(a => a.EstimatedOvercostSoles));
        water.Add(leaks.Count > 0
            ? L($"Se detectó flujo nocturno sobre el umbral de {threshold} m3/h entre 01:00 y 04:00 ({leaks.Count} vez/veces), con un sobrecosto estimado de {leakCost}.",
                $"Night flow above the {threshold} m3/h threshold was detected between 1:00 and 4:00 AM ({leaks.Count} time(s)), with an estimated overcost of {leakCost}.")
            : L($"No se superó el umbral de fuga nocturna de {threshold} m3/h entre 01:00 y 04:00.", $"The {threshold} m3/h night-leak threshold was not exceeded between 1:00 and 4:00 AM."));
        // Detección estadística: noches fuera de la línea base propia del edificio
        var unusualNights = f.Alerts.Where(a => a.Type == "StatisticalNightFlow").ToList();
        if (unusualNights.Count > 0)
        {
            var nightsCost = Pen(unusualNights.Sum(a => a.EstimatedOvercostSoles));
            water.Add(L($"La detección estadística marcó {unusualNights.Count} noche(s) con un flujo mínimo por encima de lo normal para este edificio, con un sobrecosto estimado de {nightsCost} por día.",
                $"Statistical detection flagged {unusualNights.Count} night(s) with a minimum flow above this building's normal, with an estimated overcost of {nightsCost} per day."));
        }

        var energyVersus = Versus(f.Consumption.EnergyKwh, f.PreviousConsumption.EnergyKwh, previousMonth, Kwh, english);
        var energy = new List<string> { L($"Energía de áreas comunes medida por IoBuild: {Kwh(f.Consumption.EnergyKwh)}{energyVersus}.", $"Common-area energy measured by IoBuild: {Kwh(f.Consumption.EnergyKwh)}{energyVersus}.") };
        if (f.Bill is not null)
            energy.Add(L($"Recibo matriz de Luz del Sur: {Kwh((double)f.Bill.EnergyKwh)} por {Pen(f.Bill.EnergySoles)}.", $"Luz del Sur master bill: {Kwh((double)f.Bill.EnergyKwh)} for {Pen(f.Bill.EnergySoles)}."));
        var overloads = f.Alerts.Where(a => a.Type == "PumpElectricalOverload").ToList();
        var overloadCost = Pen(overloads.Sum(a => a.EstimatedOvercostSoles));
        energy.Add(overloads.Count > 0
            ? L($"Las bombas registraron {overloads.Count} sobrecarga(s) sobre su línea base, con un sobrecosto estimado de {overloadCost}.", $"The pumps recorded {overloads.Count} overload(s) above their baseline, with an estimated overcost of {overloadCost}.")
            : L("Las bombas y tableros no registraron sobrecargas sobre su línea base.", "Pumps and panels recorded no overloads above their baseline."));
        var pumpNights = f.Alerts.Where(a => a.Type == "PumpNightRuntime").ToList();
        if (pumpNights.Count > 0)
            energy.Add(L($"Las bombas trabajaron de madrugada más de lo normal en {pumpNights.Count} noche(s): señal de que la cisterna se vacía sin uso.",
                $"The pumps ran more than usual overnight on {pumpNights.Count} night(s): a sign that the cistern is emptying without use."));

        string aliquot;
        if (f.AliquotPercentage is { } pct && f.UnitNumber is not null)
        {
            var pctText = pct.ToString("F4", Num);
            var share = f.Bill is null ? string.Empty : L($" del gasto común de {Pen(f.Bill.TotalCommonExpenseSoles)}, es decir {Pen(f.Bill.TotalCommonExpenseSoles * pct / 100m)}",
                $" of the {Pen(f.Bill.TotalCommonExpenseSoles)} common expense, that is {Pen(f.Bill.TotalCommonExpenseSoles * pct / 100m)}");
            var detail = f.Statement is null ? string.Empty : L($" Su cuota se compone de agua {Pen(f.Statement.WaterSoles)}, luz {Pen(f.Statement.EnergySoles)} y otros gastos {Pen(f.Statement.OtherSoles)}.",
                $" Your fee is made up of water {Pen(f.Statement.WaterSoles)}, electricity {Pen(f.Statement.EnergySoles)} and other expenses {Pen(f.Statement.OtherSoles)}.");
            var overcost = f.ActiveOvercostSoles > 0 ? L($" De las alertas activas le corresponderían {Pen(f.ActiveOvercostSoles * pct / 100m)}.", $" Your share of the active alerts would be {Pen(f.ActiveOvercostSoles * pct / 100m)}.") : string.Empty;
            aliquot = L($"Con su alícuota legal de {pctText}%, al Departamento {f.UnitNumber} le corresponde ese porcentaje{share}.{detail}{overcost}",
                $"With its legal aliquot of {pctText}%, Unit {f.UnitNumber} pays that percentage{share}.{detail}{overcost}");
        }
        else
        {
            aliquot = f.Bill is null
                ? L("El gasto común se reparte entre los departamentos según su alícuota legal (Ley N.° 27157).", "The common expense is split among the units by their legal aliquot (Law 27157).")
                : L($"El gasto común de {Pen(f.Bill.TotalCommonExpenseSoles)} se reparte entre los {f.TotalUnits} departamentos según su alícuota legal (Ley N.° 27157).",
                    $"The {Pen(f.Bill.TotalCommonExpenseSoles)} common expense is split among the {f.TotalUnits} units by their legal aliquot (Law 27157).");
        }

        var recommendations = new List<string>();
        if (leaks.Count > 0)
        {
            recommendations.Add(L("Inspeccionar boyas y válvulas de llenado de la cisterna principal.", "Inspect the floats and fill valves of the main cistern."));
            recommendations.Add(L("Revisar filtraciones en las líneas de impulsión hacia las áreas comunes.", "Check for leaks in the supply lines to the common areas."));
        }
        if (overloads.Count > 0) recommendations.Add(L("Programar mantenimiento eléctrico y mecánico de las bombas.", "Schedule electrical and mechanical maintenance of the pumps."));
        if ((unusualNights.Count > 0 || pumpNights.Count > 0) && leaks.Count == 0)
            recommendations.Add(L("Revisar de madrugada si corre agua en la cisterna o en las montantes: el patrón nocturno se salió de lo normal del edificio.",
                "Check overnight whether water is running in the cistern or risers: the night pattern is outside the building's normal."));
        if (f.Bill is not null && f.Bill.WaterM3 > 0 && f.Bill.WaterLossM3 / f.Bill.WaterM3 > 0.10m)
            recommendations.Add(L("Revisar la diferencia entre el recibo de Sedapal y lo medido: supera el 10%.", "Review the difference between the Sedapal bill and the measurement: it exceeds 10%."));
        if (recommendations.Count == 0) recommendations.Add(L("Mantener la revisión mensual de la cisterna y la alternancia de las bombas.", "Keep up the monthly cistern check and the pump rotation."));

        return new AiExplanationDto(
            string.Join(" ", summary),
            string.Join(" ", water),
            string.Join(" ", energy),
            aliquot,
            recommendations,
            f.HasActiveAnomalies,
            Period: f.Period);
    }

    private static string MonthName(string period, bool english) =>
        !DateTime.TryParseExact(period + "-01", "yyyy-MM-dd", Num, DateTimeStyles.None, out var d) ? period
            : english ? d.ToString("MMMM yyyy", En) : d.ToString("MMMM 'de' yyyy", Es);

    private static string Pen(decimal amount) => $"S/ {amount.ToString("N2", Num)}";
    private static string M3(double value) => $"{value.ToString("N2", Num)} m³";
    private static string Kwh(double value) => $"{value.ToString("N2", Num)} kWh";

    private static string Change(decimal current, decimal previous, bool english) =>
        previous == 0 ? (english ? "no comparison base" : "sin base de comparación") : $"{(current >= previous ? "+" : "")}{((current - previous) / previous * 100m).ToString("F1", Num)}%";

    private static string Versus(double current, double previous, string previousMonth, Func<double, string> unit, bool english)
    {
        if (previous <= 0) return english ? $" (no {previousMonth} readings to compare)" : $" (sin lecturas de {previousMonth} para comparar)";
        var change = Change((decimal)current, (decimal)previous, english);
        return english ? $", compared with {unit(previous)} in {previousMonth} ({change})" : $", frente a {unit(previous)} en {previousMonth} ({change})";
    }

    // El componente de la cuota que más cambió explica la variación
    private static string MainDriver(StatementFact current, StatementFact previous, bool english)
    {
        var deltas = new[]
        {
            (english ? "water" : "el agua", current.WaterSoles - previous.WaterSoles),
            (english ? "electricity" : "la luz", current.EnergySoles - previous.EnergySoles),
            (english ? "other common expenses" : "otros gastos comunes", current.OtherSoles - previous.OtherSoles)
        };
        var (name, delta) = deltas.OrderByDescending(d => Math.Abs(d.Item2)).First();
        return $"{name} ({(delta >= 0 ? "+" : "-")}{Pen(Math.Abs(delta))})";
    }
}
