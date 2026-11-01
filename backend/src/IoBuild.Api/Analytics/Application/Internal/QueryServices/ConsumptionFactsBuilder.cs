using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Analytics.Application.Internal.QueryServices;

/// <summary>Consumo de áreas comunes de un periodo: agua del medidor de ingreso y energía de los tableros.</summary>
public sealed record PeriodConsumption(double WaterM3, double EnergyKwh, int DaysWithData);

/// <summary>Una alerta del periodo tal como la registró el motor de reglas.</summary>
public sealed record AlertFact(string Type, string Title, DateTime DetectedAtUtc, string Status, decimal EstimatedOvercostSoles);

/// <summary>Recibo matriz conciliado del periodo (Sedapal y Luz del Sur) y gasto común total repartido.</summary>
public sealed record BillFact(decimal WaterM3, decimal WaterSoles, decimal EnergyKwh, decimal EnergySoles, decimal MeasuredCommonWaterM3,
    decimal MeasuredCommonEnergyKwh, decimal WaterLossM3, decimal EnergyLossKwh, decimal OtherExpensesSoles, decimal TotalCommonExpenseSoles, string Status);

/// <summary>Cuota de un departamento en un periodo, desglosada como en su estado de cuenta.</summary>
public sealed record StatementFact(decimal WaterSoles, decimal EnergySoles, decimal OtherSoles, decimal TotalSoles, string Status);

/// <summary>
/// Hechos verificables del periodo y del anterior. Es lo único que el explicador (reglas o Gemini) puede usar.
/// </summary>
public sealed record ConsumptionFacts(
    string ProjectName,
    int TotalUnits,
    string Period,
    string PreviousPeriod,
    bool PeriodInProgress,
    PeriodConsumption Consumption,
    PeriodConsumption PreviousConsumption,
    decimal NightFlowThresholdM3H,
    IReadOnlyList<AlertFact> Alerts,
    BillFact? Bill,
    BillFact? PreviousBill,
    string? UnitNumber,
    decimal? AliquotPercentage,
    StatementFact? Statement,
    StatementFact? PreviousStatement)
{
    public decimal ActiveOvercostSoles => Alerts.Where(a => a.Status == "Active").Sum(a => a.EstimatedOvercostSoles);
    public bool HasActiveAnomalies => Alerts.Any(a => a.Status == "Active");
}

/// <summary>
/// Reúne los hechos de consumo, recibos, alertas y cuota de un periodo. Las lecturas son acumuladas:
/// el consumo del periodo es la lectura máxima menos la mínima de cada medidor.
/// </summary>
public sealed class ConsumptionFactsBuilder(IoBuildDbContext db)
{
    // La cisterna tiene medidor de ingreso (Sedapal) y de impulsión; sumar ambos duplicaría el agua.
    // Cuenta el de ingreso (sufijo -IN del kit HaaS), que es el que se concilia con el recibo matriz.
    public static async Task<List<int>> NonIntakeWaterSensorIdsAsync(IoBuildDbContext db, int projectId, CancellationToken ct = default)
    {
        var waterSensors = await db.Devices
            .Where(d => d.ProjectId == projectId && d.Type == "WaterFlowSensor")
            .Select(d => new { d.Id, d.Name })
            .ToListAsync(ct);
        var intakeIds = waterSensors.Where(d => d.Name.EndsWith("-IN", StringComparison.OrdinalIgnoreCase)).Select(d => d.Id).ToList();
        return intakeIds.Count > 0 ? waterSensors.Select(d => d.Id).Except(intakeIds).ToList() : new List<int>();
    }

    public async Task<ConsumptionFacts> BuildAsync(int projectId, int? unitId, string? requestedPeriod, CancellationToken ct = default)
    {
        var project = await db.Projects.FindAsync([projectId], ct);
        var unit = unitId.HasValue ? await db.Units.FirstOrDefaultAsync(u => u.Id == unitId.Value && u.ProjectId == projectId, ct) : null;

        var period = await ResolvePeriodAsync(projectId, unit?.Id, requestedPeriod, ct);
        var (start, end) = Range(period);
        var previous = start.AddMonths(-1);
        var previousPeriod = $"{previous:yyyy-MM}";
        var now = DateTime.UtcNow;

        var excludedWaterIds = await NonIntakeWaterSensorIdsAsync(db, projectId, ct);
        var consumption = await ConsumptionAsync(projectId, excludedWaterIds, start, end, ct);
        var previousConsumption = await ConsumptionAsync(projectId, excludedWaterIds, previous, start.AddTicks(-1), ct);

        var threshold = await db.Devices
            .Where(d => d.ProjectId == projectId && d.Type == "WaterFlowSensor")
            .Select(d => (decimal?)d.NightFlowThresholdM3H)
            .FirstOrDefaultAsync(ct) ?? 0.1500m;

        var alerts = await db.AnomalyAlerts
            .Where(a => a.ProjectId == projectId && a.DetectedAtUtc >= start && a.DetectedAtUtc <= end)
            .OrderByDescending(a => a.DetectedAtUtc)
            .Select(a => new AlertFact(a.AlertType, a.Title, a.DetectedAtUtc, a.Status, a.EstimatedOvercostSoles))
            .Take(20)
            .ToListAsync(ct);

        var bills = await db.BillingPeriods
            .Where(b => b.ProjectId == projectId && (b.PeriodCode == period || b.PeriodCode == previousPeriod))
            .ToListAsync(ct);
        BillFact? ToBill(string code) => bills.Where(b => b.PeriodCode == code).Select(b => new BillFact(
            b.MacroWaterM3, b.MacroWaterAmountSoles, b.MacroEnergyKwh, b.MacroEnergyAmountSoles, b.MeasuredCommonWaterM3,
            b.MeasuredCommonEnergyKwh, b.WaterLossM3, b.EnergyLossKwh, b.OtherCommonExpensesSoles, b.TotalCommonExpenseSoles, b.Status)).FirstOrDefault();

        StatementFact? statement = null, previousStatement = null;
        if (unit is not null)
        {
            var periodIds = bills.ToDictionary(b => b.Id, b => b.PeriodCode);
            var statements = await db.StatementOfAccounts
                .Where(s => s.UnitId == unit.Id && periodIds.Keys.Contains(s.BillingPeriodId))
                .ToListAsync(ct);
            StatementFact? ToStatement(string code) => statements
                .Where(s => periodIds[s.BillingPeriodId] == code)
                .Select(s => new StatementFact(s.IndividualWaterSoles, s.IndividualEnergySoles, s.IndividualOtherExpensesSoles, s.IndividualAmountSoles, s.Status))
                .FirstOrDefault();
            statement = ToStatement(period);
            previousStatement = ToStatement(previousPeriod);
        }

        return new ConsumptionFacts(
            project?.Name ?? $"Condominio #{projectId}",
            project?.TotalUnits ?? 0,
            period,
            previousPeriod,
            PeriodInProgress: end > now,
            consumption,
            previousConsumption,
            threshold,
            alerts,
            ToBill(period),
            ToBill(previousPeriod),
            unit?.UnitNumber,
            unit?.AliquotPercentage,
            statement,
            previousStatement);
    }

    // Sin periodo pedido: el de la última cuota del departamento o el último recibo del condominio; si no hay, el mes en curso
    private async Task<string> ResolvePeriodAsync(int projectId, int? unitId, string? requested, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(requested) && DateTime.TryParseExact(requested + "-01", "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
            return requested;

        string? latest = null;
        if (unitId.HasValue)
        {
            latest = await db.StatementOfAccounts
                .Where(s => s.UnitId == unitId.Value)
                .Join(db.BillingPeriods, s => s.BillingPeriodId, b => b.Id, (s, b) => b.PeriodCode)
                .OrderByDescending(code => code)
                .FirstOrDefaultAsync(ct);
        }
        latest ??= await db.BillingPeriods
            .Where(b => b.ProjectId == projectId)
            .OrderByDescending(b => b.PeriodCode)
            .Select(b => b.PeriodCode)
            .FirstOrDefaultAsync(ct);
        return latest ?? $"{DateTime.UtcNow:yyyy-MM}";
    }

    private static (DateTime Start, DateTime End) Range(string period)
    {
        var start = DateTime.SpecifyKind(DateTime.ParseExact(period + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);
        return (start, start.AddMonths(1).AddTicks(-1));
    }

    private async Task<PeriodConsumption> ConsumptionAsync(int projectId, List<int> excludedWaterIds, DateTime start, DateTime end, CancellationToken ct)
    {
        var records = db.ConsumptionRecords.Where(c => c.ProjectId == projectId && c.TimestampUtc >= start && c.TimestampUtc <= end);
        var perDevice = await records
            .GroupBy(c => new { c.DeviceId, c.MetricType })
            .Select(g => new { g.Key.DeviceId, g.Key.MetricType, Delta = g.Max(c => c.Value) - g.Min(c => c.Value) })
            .ToListAsync(ct);
        var days = await records.Select(c => c.TimestampUtc.Date).Distinct().CountAsync(ct);
        var water = perDevice.Where(r => r.MetricType == "WaterM3" && !excludedWaterIds.Contains(r.DeviceId)).Sum(r => r.Delta);
        var energy = perDevice.Where(r => r.MetricType == "EnergyKwh").Sum(r => r.Delta);
        return new PeriodConsumption(Math.Round(water, 2), Math.Round(energy, 2), days);
    }
}
