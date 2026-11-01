using System.Globalization;
using IoBuild.Api.Analytics.Application.Internal.QueryServices;
using IoBuild.Api.Analytics.Domain.Services;
using IoBuild.Api.Persistence;

namespace IoBuild.Api.Analytics.Infrastructure.Adapters;

/// <summary>
/// Explicación determinística del consumo del periodo: compara con el mes anterior, el recibo matriz, las alertas
/// del motor de reglas y la cuota del departamento (Ley N.° 27157). También es el respaldo del adaptador de Gemini.
/// </summary>
public sealed class MockAiCognitiveAdapter(IoBuildDbContext db) : IAiCognitiveService
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-PE");
    private static readonly CultureInfo Num = CultureInfo.InvariantCulture;

    public Task<ConsumptionFacts> BuildFactsAsync(int projectId, int? unitId, string? billingPeriod) =>
        new ConsumptionFactsBuilder(db).BuildAsync(projectId, unitId, billingPeriod);

    public async Task<AiExplanationDto> ExplainConsumptionVariationAsync(int projectId, int? unitId, string billingPeriod, string question, string language = "es") =>
        Render(await BuildFactsAsync(projectId, unitId, billingPeriod));

    public static AiExplanationDto Render(ConsumptionFacts f)
    {
        var month = MonthName(f.Period);
        var previousMonth = MonthName(f.PreviousPeriod);
        var unitLabel = f.UnitNumber is null ? string.Empty : $" para el Departamento {f.UnitNumber}";
        var progress = f.PeriodInProgress ? $" (mes en curso, {f.Consumption.DaysWithData} días con lecturas)" : string.Empty;

        // Resumen: primero la cuota (lo que pregunta el residente), luego qué la movió
        var summary = new List<string> { $"{f.ProjectName}{unitLabel}, {month}{progress}." };
        if (f.UnitNumber is not null)
        {
            if (f.Statement is null)
                summary.Add($"Aún no se emitió la cuota de {month} para el Departamento {f.UnitNumber}.");
            else if (f.PreviousStatement is null)
                summary.Add($"Su cuota fue {Pen(f.Statement.TotalSoles)}; no hay cuota de {previousMonth} para comparar.");
            else
                summary.Add($"Su cuota fue {Pen(f.Statement.TotalSoles)} frente a {Pen(f.PreviousStatement.TotalSoles)} en {previousMonth} ({Change(f.Statement.TotalSoles, f.PreviousStatement.TotalSoles)}), principalmente por {MainDriver(f.Statement, f.PreviousStatement)}.");
        }
        summary.Add(f.HasActiveAnomalies
            ? $"Hay {f.Alerts.Count(a => a.Status == "Active")} alerta(s) activa(s) con un sobrecosto estimado de {Pen(f.ActiveOvercostSoles)}."
            : "No hay alertas activas en el periodo.");

        var water = new List<string> { $"Agua de áreas comunes medida por IoBuild: {M3(f.Consumption.WaterM3)}{Versus(f.Consumption.WaterM3, f.PreviousConsumption.WaterM3, previousMonth, M3)}." };
        if (f.Bill is not null)
            water.Add($"Recibo matriz de Sedapal: {M3((double)f.Bill.WaterM3)} por {Pen(f.Bill.WaterSoles)}; diferencia entre lo facturado y lo medido: {M3((double)f.Bill.WaterLossM3)}.");
        var leaks = f.Alerts.Where(a => a.Type == "NocturnalWaterLeak").ToList();
        water.Add(leaks.Count > 0
            ? $"Se detectó flujo nocturno sobre el umbral de {f.NightFlowThresholdM3H.ToString("F2", Num)} m3/h entre 01:00 y 04:00 ({leaks.Count} vez/veces), con un sobrecosto estimado de {Pen(leaks.Sum(a => a.EstimatedOvercostSoles))}."
            : $"No se superó el umbral de fuga nocturna de {f.NightFlowThresholdM3H.ToString("F2", Num)} m3/h entre 01:00 y 04:00.");

        var energy = new List<string> { $"Energía de áreas comunes medida por IoBuild: {Kwh(f.Consumption.EnergyKwh)}{Versus(f.Consumption.EnergyKwh, f.PreviousConsumption.EnergyKwh, previousMonth, Kwh)}." };
        if (f.Bill is not null)
            energy.Add($"Recibo matriz de Luz del Sur: {Kwh((double)f.Bill.EnergyKwh)} por {Pen(f.Bill.EnergySoles)}.");
        var overloads = f.Alerts.Where(a => a.Type == "PumpElectricalOverload").ToList();
        energy.Add(overloads.Count > 0
            ? $"Las bombas registraron {overloads.Count} sobrecarga(s) sobre su línea base, con un sobrecosto estimado de {Pen(overloads.Sum(a => a.EstimatedOvercostSoles))}."
            : "Las bombas y tableros no registraron sobrecargas sobre su línea base.");

        string aliquot;
        if (f.AliquotPercentage is { } pct && f.UnitNumber is not null)
        {
            var share = f.Bill is null ? string.Empty : $" del gasto común de {Pen(f.Bill.TotalCommonExpenseSoles)}, es decir {Pen(f.Bill.TotalCommonExpenseSoles * pct / 100m)}";
            var detail = f.Statement is null ? string.Empty : $" Su cuota se compone de agua {Pen(f.Statement.WaterSoles)}, luz {Pen(f.Statement.EnergySoles)} y otros gastos {Pen(f.Statement.OtherSoles)}.";
            var overcost = f.ActiveOvercostSoles > 0 ? $" De las alertas activas le corresponderían {Pen(f.ActiveOvercostSoles * pct / 100m)}." : string.Empty;
            aliquot = $"Con su alícuota legal de {pct.ToString("F4", Num)}%, al Departamento {f.UnitNumber} le corresponde ese porcentaje{share}.{detail}{overcost}";
        }
        else
        {
            aliquot = f.Bill is null
                ? "El gasto común se reparte entre los departamentos según su alícuota legal (Ley N.° 27157)."
                : $"El gasto común de {Pen(f.Bill.TotalCommonExpenseSoles)} se reparte entre los {f.TotalUnits} departamentos según su alícuota legal (Ley N.° 27157).";
        }

        var recommendations = new List<string>();
        if (leaks.Count > 0)
        {
            recommendations.Add("Inspeccionar boyas y válvulas de llenado de la cisterna principal.");
            recommendations.Add("Revisar filtraciones en las líneas de impulsión hacia las áreas comunes.");
        }
        if (overloads.Count > 0) recommendations.Add("Programar mantenimiento eléctrico y mecánico de las bombas.");
        if (f.Bill is not null && f.Bill.WaterM3 > 0 && f.Bill.WaterLossM3 / f.Bill.WaterM3 > 0.10m)
            recommendations.Add("Revisar la diferencia entre el recibo de Sedapal y lo medido: supera el 10%.");
        if (recommendations.Count == 0) recommendations.Add("Mantener la revisión mensual de la cisterna y la alternancia de las bombas.");

        return new AiExplanationDto(
            string.Join(" ", summary),
            string.Join(" ", water),
            string.Join(" ", energy),
            aliquot,
            recommendations,
            f.HasActiveAnomalies,
            Period: f.Period);
    }

    private static string MonthName(string period) =>
        DateTime.TryParseExact(period + "-01", "yyyy-MM-dd", Num, DateTimeStyles.None, out var d) ? d.ToString("MMMM 'de' yyyy", Es) : period;

    private static string Pen(decimal amount) => $"S/ {amount.ToString("N2", Num)}";
    private static string M3(double value) => $"{value.ToString("N2", Num)} m³";
    private static string Kwh(double value) => $"{value.ToString("N2", Num)} kWh";

    private static string Change(decimal current, decimal previous) =>
        previous == 0 ? "sin base de comparación" : $"{(current >= previous ? "+" : "")}{((current - previous) / previous * 100m).ToString("F1", Num)}%";

    private static string Versus(double current, double previous, string previousMonth, Func<double, string> unit) =>
        previous <= 0 ? $" (sin lecturas de {previousMonth} para comparar)" : $", frente a {unit(previous)} en {previousMonth} ({Change((decimal)current, (decimal)previous)})";

    // El componente de la cuota que más cambió explica la variación
    private static string MainDriver(StatementFact current, StatementFact previous)
    {
        var deltas = new[]
        {
            ("el agua", current.WaterSoles - previous.WaterSoles),
            ("la luz", current.EnergySoles - previous.EnergySoles),
            ("otros gastos comunes", current.OtherSoles - previous.OtherSoles)
        };
        var (name, delta) = deltas.OrderByDescending(d => Math.Abs(d.Item2)).First();
        return $"{name} ({(delta >= 0 ? "+" : "-")}{Pen(Math.Abs(delta))})";
    }
}
