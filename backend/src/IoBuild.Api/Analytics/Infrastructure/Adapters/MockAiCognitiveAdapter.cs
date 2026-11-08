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

    public async Task<AiExplanationDto> ExplainConsumptionVariationAsync(int projectId, int? unitId, string billingPeriod, string question, string language = "es") =>
        Render(await BuildFactsAsync(projectId, unitId, billingPeriod), language);

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
