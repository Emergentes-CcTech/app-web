using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Analytics.Application.Internal.QueryServices;

/// <summary>Departamento de quien pregunta, con su participación legal en el gasto común.</summary>
public sealed record UnitContext(string Number, string Type, int Floor, decimal AreaM2, decimal ParticipationPct);

/// <summary>Una cuota del departamento, como figura en su estado de cuenta.</summary>
public sealed record FeeHistoryItem(string Period, decimal TotalSoles, decimal WaterSoles, decimal EnergySoles, decimal OtherSoles,
    decimal BalanceSoles, string Status, string DueDate, string? PaidAt);

/// <summary>Consumo medido por IoBuild en un área común durante el periodo y el anterior.</summary>
public sealed record AreaConsumption(string Name, string Subsystem, double WaterM3, double EnergyKwh, double PreviousWaterM3, double PreviousEnergyKwh, int Meters);

/// <summary>Gasto común repartido de un periodo (recibos matriz más otros gastos).</summary>
public sealed record BuildingExpense(string Period, decimal WaterSoles, decimal EnergySoles, decimal OtherSoles, decimal TotalSoles);

/// <summary>
/// Datos adicionales para responder preguntas libres sobre los gastos: historial de cuotas y deuda del departamento,
/// consumo por área común, gasto común de los últimos meses y alertas abiertas. Todo sale de la base de datos.
/// </summary>
public sealed record AssistantContext(
    string Today,
    UnitContext? Unit,
    IReadOnlyList<FeeHistoryItem> FeeHistory,
    decimal OutstandingSoles,
    IReadOnlyList<AreaConsumption> Areas,
    IReadOnlyList<BuildingExpense> BuildingExpenses,
    IReadOnlyList<AlertFact> OpenAlerts,
    int MeteredDevices,
    int OnlineDevices,
    string FeeFormula);

public sealed class AssistantContextBuilder(IoBuildDbContext db)
{
    private const int HistoryMonths = 12;

    public async Task<AssistantContext> BuildAsync(int projectId, int? unitId, string period, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var unit = unitId.HasValue ? await db.Units.FirstOrDefaultAsync(u => u.Id == unitId.Value && u.ProjectId == projectId, ct) : null;

        // Historial de cuotas del departamento con su fecha de vencimiento
        var history = new List<FeeHistoryItem>();
        if (unit is not null)
        {
            var rows = await db.StatementOfAccounts
                .Where(s => s.UnitId == unit.Id)
                .Join(db.BillingPeriods, s => s.BillingPeriodId, b => b.Id, (s, b) => new { s, b.PeriodCode, b.DueDate })
                .OrderByDescending(x => x.PeriodCode)
                .Take(HistoryMonths)
                .ToListAsync(ct);
            history = rows.Select(x =>
            {
                var paid = string.Equals(x.s.Status, "Paid", StringComparison.OrdinalIgnoreCase) || x.s.BalanceSoles <= 0;
                var status = paid ? "Paid" : x.DueDate.Date < today ? "Overdue" : "Pending";
                return new FeeHistoryItem(x.PeriodCode, x.s.IndividualAmountSoles, x.s.IndividualWaterSoles, x.s.IndividualEnergySoles,
                    x.s.IndividualOtherExpensesSoles, paid ? 0m : x.s.BalanceSoles, status, x.DueDate.ToString("yyyy-MM-dd"), x.s.PaidAtUtc?.ToString("yyyy-MM-dd"));
            }).ToList();
        }

        var expenses = await db.BillingPeriods
            .Where(b => b.ProjectId == projectId && b.TotalCommonExpenseSoles > 0)
            .OrderByDescending(b => b.PeriodCode)
            .Take(6)
            .Select(b => new BuildingExpense(b.PeriodCode, b.MacroWaterAmountSoles, b.MacroEnergyAmountSoles, b.OtherCommonExpensesSoles, b.TotalCommonExpenseSoles))
            .ToListAsync(ct);

        var openAlerts = await db.AnomalyAlerts
            .Where(a => a.ProjectId == projectId && a.Status == "Active")
            .OrderByDescending(a => a.DetectedAtUtc)
            .Take(10)
            .Select(a => new AlertFact(a.AlertType, a.Title, a.DetectedAtUtc, a.Status, a.EstimatedOvercostSoles))
            .ToListAsync(ct);

        var devices = await db.Devices.Where(d => d.ProjectId == projectId && d.UnitId == null && d.Status != "Unlinked").ToListAsync(ct);
        var areas = await AreasAsync(projectId, period, devices, ct);

        return new AssistantContext(
            today.ToString("yyyy-MM-dd"),
            unit is null ? null : new UnitContext(unit.UnitNumber, unit.UnitType, unit.Floor, unit.AreaM2, unit.AliquotPercentage),
            history,
            history.Sum(h => h.BalanceSoles),
            areas,
            expenses,
            openAlerts,
            devices.Count(d => d.Type != "GatewayHub"),
            devices.Count(d => d.Type != "GatewayHub" && string.Equals(d.Status, "Online", StringComparison.OrdinalIgnoreCase)),
            "Cuota del departamento = gasto común del mes (recibos de agua y luz de áreas comunes + otros gastos) × participación del departamento ÷ 100 (Ley N.° 27157).");
    }

    // Consumo por área común: cada medidor se atribuye al área de su mismo subsistema
    private async Task<List<AreaConsumption>> AreasAsync(int projectId, string period, List<IoBuild.Api.Devices.Domain.Model.Aggregates.Device> devices, CancellationToken ct)
    {
        var start = DateTime.SpecifyKind(DateTime.ParseExact(period + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);
        var end = start.AddMonths(1).AddTicks(-1);
        var previous = start.AddMonths(-1);
        var excluded = await ConsumptionFactsBuilder.NonIntakeWaterSensorIdsAsync(db, projectId, ct);

        async Task<Dictionary<(int, string), double>> DeltasAsync(DateTime from, DateTime to) =>
            (await db.ConsumptionRecords
                .Where(c => c.ProjectId == projectId && c.TimestampUtc >= from && c.TimestampUtc <= to)
                .GroupBy(c => new { c.DeviceId, c.MetricType })
                .Select(g => new { g.Key.DeviceId, g.Key.MetricType, Delta = g.Max(c => c.Value) - g.Min(c => c.Value) })
                .ToListAsync(ct))
            .ToDictionary(r => (r.DeviceId, r.MetricType), r => r.Delta);

        var current = await DeltasAsync(start, end);
        var before = await DeltasAsync(previous, start.AddTicks(-1));
        var commonAreas = await db.CommonAreas.Where(a => a.ProjectId == projectId).ToListAsync(ct);

        double Sum(Dictionary<(int, string), double> deltas, IEnumerable<int> ids, string metric) =>
            Math.Round(ids.Where(id => metric != "WaterM3" || !excluded.Contains(id)).Sum(id => deltas.GetValueOrDefault((id, metric))), 2);

        return commonAreas.Select(area =>
        {
            var subsystem = area.Subsystem.ToString();
            var meterIds = devices.Where(d => d.Type != "GatewayHub" && d.Subsystem == subsystem).Select(d => d.Id).ToList();
            return new AreaConsumption(area.Name, subsystem,
                Sum(current, meterIds, "WaterM3"), Sum(current, meterIds, "EnergyKwh"),
                Sum(before, meterIds, "WaterM3"), Sum(before, meterIds, "EnergyKwh"), meterIds.Count);
        }).ToList();
    }
}
