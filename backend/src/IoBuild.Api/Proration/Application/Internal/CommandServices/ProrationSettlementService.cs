using IoBuild.Api.Persistence;
using IoBuild.Api.Proration.Domain.Model.Aggregates;
using IoBuild.Api.Proration.Domain.Services;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Proration.Application.Internal.CommandServices;

/// <summary>
/// Servicio de aplicación para la conciliación de consumos matrices de Sedapal y Luz del Sur y liquidación de cuotas legales (Ley N.° 27157).
/// </summary>
public sealed class ProrationSettlementService(IoBuildDbContext db) : IProrationSettlementService
{
    public async Task<BillingPeriod> ReconcileMacroBillsAsync(
        int projectId,
        string periodCode,
        DateTime startDate,
        DateTime endDate,
        DateTime dueDate,
        decimal macroWaterM3,
        decimal macroWaterAmountSoles,
        string? waterReceiptImageUrl,
        decimal macroEnergyKwh,
        decimal macroEnergyAmountSoles,
        string? energyReceiptImageUrl,
        decimal otherExpensesSoles,
        CancellationToken ct = default)
    {
        // Validaciones de integridad y coherencia temporal de fechas e importes
        if (string.IsNullOrWhiteSpace(periodCode))
        {
            throw new ArgumentException("El código de periodo no puede ser vacío.", nameof(periodCode));
        }

        if (startDate >= endDate)
        {
            throw new ArgumentException("La fecha de inicio debe ser anterior a la fecha de fin del periodo.", nameof(startDate));
        }

        if (endDate > dueDate)
        {
            throw new ArgumentException("La fecha de fin no puede ser posterior a la fecha de vencimiento de pago.", nameof(dueDate));
        }

        if (macroWaterM3 < 0 || macroWaterAmountSoles < 0 || macroEnergyKwh < 0 || macroEnergyAmountSoles < 0 || otherExpensesSoles < 0)
        {
            throw new ArgumentException("Los importes y consumos de recibos matrices no pueden ser negativos.");
        }

        var period = await db.BillingPeriods.FirstOrDefaultAsync(p => p.ProjectId == projectId && p.PeriodCode == periodCode, ct);
        if (period is not null)
        {
            if (period.Status == "Closed")
            {
                throw new InvalidOperationException($"El periodo {periodCode} se encuentra cerrado y no admite modificaciones.");
            }

            var hasPayments = await db.StatementOfAccounts.AnyAsync(s => s.BillingPeriodId == period.Id && (s.Status == "Paid" || s.PaidAmountSoles > 0), ct);
            if (hasPayments)
            {
                throw new InvalidOperationException($"El periodo {periodCode} ya cuenta con liquidaciones con abonos registrados y no puede re-conciliarse.");
            }
        }

        // Consulta las mediciones acumuladas de series temporales en áreas comunes de Analytics
        var records = await db.ConsumptionRecords
            .Where(r => r.ProjectId == projectId && r.TimestampUtc >= startDate && r.TimestampUtc <= endDate)
            .ToListAsync(ct);

        // Agrupa por dispositivo y obtiene el consumo acumulado neto del periodo
        var waterRecords = records.Where(r => r.MetricType == "WaterM3").ToList();
        var energyRecords = records.Where(r => r.MetricType == "EnergyKwh").ToList();

        decimal measuredWaterDecimal = 0.0000m;
        if (waterRecords.Count > 0)
        {
            double totalWater = 0.0;
            foreach (var group in waterRecords.GroupBy(r => r.DeviceId))
            {
                var maxVal = group.Max(r => r.Value);
                var minVal = group.Min(r => r.Value);
                var net = maxVal - minVal;
                totalWater += net > 0.0 ? net : maxVal;
            }
            measuredWaterDecimal = Math.Round((decimal)totalWater, 4);
        }

        decimal measuredEnergyDecimal = 0.0000m;
        if (energyRecords.Count > 0)
        {
            double totalEnergy = 0.0;
            foreach (var group in energyRecords.GroupBy(r => r.DeviceId))
            {
                var maxVal = group.Max(r => r.Value);
                var minVal = group.Min(r => r.Value);
                var net = maxVal - minVal;
                totalEnergy += net > 0.0 ? net : maxVal;
            }
            measuredEnergyDecimal = Math.Round((decimal)totalEnergy, 4);
        }

        var waterLoss = Math.Max(0.0000m, macroWaterM3 - measuredWaterDecimal);
        var energyLoss = Math.Max(0.0000m, macroEnergyKwh - measuredEnergyDecimal);
        var totalCommonExpense = Math.Round(macroWaterAmountSoles + macroEnergyAmountSoles + otherExpensesSoles, 2);

        if (period is null)
        {
            period = new BillingPeriod
            {
                ProjectId = projectId,
                PeriodCode = periodCode
            };
            db.BillingPeriods.Add(period);
        }

        period.StartDate = startDate;
        period.EndDate = endDate;
        period.DueDate = dueDate;
        period.MacroWaterM3 = macroWaterM3;
        period.MacroWaterAmountSoles = macroWaterAmountSoles;
        period.WaterReceiptImageUrl = waterReceiptImageUrl;
        period.MacroEnergyKwh = macroEnergyKwh;
        period.MacroEnergyAmountSoles = macroEnergyAmountSoles;
        period.EnergyReceiptImageUrl = energyReceiptImageUrl;
        period.MeasuredCommonWaterM3 = measuredWaterDecimal;
        period.MeasuredCommonEnergyKwh = measuredEnergyDecimal;
        period.WaterLossM3 = waterLoss;
        period.EnergyLossKwh = energyLoss;
        period.OtherCommonExpensesSoles = otherExpensesSoles;
        period.TotalCommonExpenseSoles = totalCommonExpense;
        period.Status = "Reconciled";
        period.ReconciledAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return period;
    }

    public async Task<List<StatementOfAccount>> GenerateSettlementAsync(int billingPeriodId, CancellationToken ct = default)
    {
        var period = await db.BillingPeriods.FindAsync([billingPeriodId], ct);
        if (period is null)
        {
            throw new KeyNotFoundException($"El periodo de facturación #{billingPeriodId} no fue encontrado.");
        }

        if (period.Status == "Closed")
        {
            throw new InvalidOperationException("El periodo de facturación se encuentra cerrado y no puede liquidarse nuevamente.");
        }

        var units = await db.Units
            .Where(u => u.ProjectId == period.ProjectId)
            .OrderBy(u => u.Floor)
            .ThenBy(u => u.RoomNumber)
            .ToListAsync(ct);

        if (units.Count == 0)
        {
            throw new InvalidOperationException($"No existen departamentos registrados en el condominio #{period.ProjectId}.");
        }

        // Valida que ninguna alícuota sea negativa o cero
        if (units.Any(u => u.AliquotPercentage <= 0.0000m || u.AliquotPercentage > 100.0000m))
        {
            throw new InvalidOperationException("Infracción a Ley N.° 27157: Todas las alícuotas deben ser mayores a 0% y menores o iguales a 100%.");
        }

        // Invariante Sagrado Ley N.° 27157: la suma de alícuotas debe sumar exactamente 100.0000%
        var totalAliquot = units.Sum(u => u.AliquotPercentage);
        if (Math.Abs(totalAliquot - 100.0000m) > 0.0001m)
        {
            throw new InvalidOperationException($"Infracción a Ley N.° 27157: La suma de alícuotas es {totalAliquot:F4}%, debe ser exactamente 100.0000%.");
        }

        // Protege contra sobreescritura si ya se recibieron abonos
        var existing = await db.StatementOfAccounts.Where(s => s.BillingPeriodId == billingPeriodId).ToListAsync(ct);
        if (existing.Any(s => s.Status == "Paid" || s.PaidAmountSoles > 0))
        {
            throw new InvalidOperationException("No se puede regenerar la liquidación porque ya existen abonos registrados en este periodo.");
        }

        if (existing.Count > 0)
        {
            db.StatementOfAccounts.RemoveRange(existing);
            // Persiste el borrado previo para evitar conflicto de clave única (BillingPeriodId, UnitId) en bases de datos relacionales
            await db.SaveChangesAsync(ct);
        }

        // Distribución por el Método del Mayor Resto sobre céntimos enteros (Largest Remainder Method)
        // Garantiza que la suma de céntimos sea matemáticamente idéntica y que ningún componente sea negativo
        var waterCents = DistributeCentsByAliquot(period.MacroWaterAmountSoles, units);
        var energyCents = DistributeCentsByAliquot(period.MacroEnergyAmountSoles, units);
        var otherCents = DistributeCentsByAliquot(period.OtherCommonExpensesSoles, units);

        var statements = new List<StatementOfAccount>();
        foreach (var unit in units)
        {
            var indWater = waterCents[unit.Id] / 100.00m;
            var indEnergy = energyCents[unit.Id] / 100.00m;
            var indOther = otherCents[unit.Id] / 100.00m;
            var indTotal = indWater + indEnergy + indOther;

            var statement = new StatementOfAccount
            {
                BillingPeriodId = period.Id,
                ProjectId = period.ProjectId,
                UnitId = unit.Id,
                UnitNumber = unit.UnitNumber,
                ResidentFullName = string.IsNullOrWhiteSpace(unit.ResidentFullName) ? $"Residente Dpto. {unit.UnitNumber}" : unit.ResidentFullName,
                ResidentEmail = string.IsNullOrWhiteSpace(unit.ResidentEmail) ? (unit.OwnerEmail ?? string.Empty) : unit.ResidentEmail,
                AliquotPercentage = unit.AliquotPercentage,
                IndividualWaterSoles = indWater,
                IndividualEnergySoles = indEnergy,
                IndividualOtherExpensesSoles = indOther,
                IndividualAmountSoles = indTotal,
                PaidAmountSoles = 0.00m,
                BalanceSoles = indTotal,
                Status = "Pending"
            };

            statements.Add(statement);
        }

        db.StatementOfAccounts.AddRange(statements);

        period.Status = "Settled";
        period.SettledAtUtc = DateTime.UtcNow;
        period.TotalBilledSoles = statements.Sum(s => s.IndividualAmountSoles);
        period.TotalCollectedSoles = 0.00m;
        period.DelinquencyRate = 0.00m;

        await db.SaveChangesAsync(ct);
        return statements;
    }

    public async Task<StatementOfAccount> RegisterPaymentAsync(
        int statementId,
        decimal amount,
        string paymentMethod,
        string paymentReference,
        CancellationToken ct = default)
    {
        // Normaliza el abono a 2 decimales exactos
        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        if (amount <= 0.00m)
        {
            throw new ArgumentException("El monto a registrar debe ser estrictamente mayor a S/ 0.00.", nameof(amount));
        }

        var statement = await db.StatementOfAccounts.FindAsync([statementId], ct);
        if (statement is null)
        {
            throw new KeyNotFoundException($"El estado de cuenta #{statementId} no fue encontrado.");
        }

        if (amount > statement.BalanceSoles)
        {
            throw new InvalidOperationException($"El importe a pagar (S/ {amount:F2}) excede el saldo pendiente (S/ {statement.BalanceSoles:F2}).");
        }

        statement.PaidAmountSoles += amount;
        statement.BalanceSoles = Math.Round(statement.IndividualAmountSoles - statement.PaidAmountSoles, 2);

        if (statement.BalanceSoles <= 0.00m)
        {
            statement.Status = "Paid";
            statement.PaidAtUtc = DateTime.UtcNow;
        }
        else
        {
            statement.Status = "PartiallyPaid";
        }

        statement.PaymentMethod = paymentMethod;
        statement.PaymentReference = paymentReference;

        var period = await db.BillingPeriods.FindAsync([statement.BillingPeriodId], ct);
        if (period is not null)
        {
            var statements = await db.StatementOfAccounts.Where(s => s.BillingPeriodId == period.Id).ToListAsync(ct);
            var totalCollected = statements.Sum(s => s.PaidAmountSoles);
            period.TotalCollectedSoles = totalCollected;

            var isOverdue = DateTime.UtcNow > period.DueDate;
            var overdueAmount = isOverdue
                ? statements.Where(s => s.BalanceSoles > 0).Sum(s => s.BalanceSoles)
                : 0.00m;

            period.DelinquencyRate = (period.TotalBilledSoles > 0 && isOverdue)
                ? Math.Round((overdueAmount / period.TotalBilledSoles) * 100.00m, 2)
                : 0.00m;
        }

        await db.SaveChangesAsync(ct);
        return statement;
    }

    public async Task<BillingPeriodMetricsDto> GetPeriodMetricsAsync(int billingPeriodId, CancellationToken ct = default)
    {
        var period = await db.BillingPeriods.FindAsync([billingPeriodId], ct);
        if (period is null)
        {
            throw new KeyNotFoundException($"El periodo #{billingPeriodId} no fue encontrado.");
        }

        var statements = await db.StatementOfAccounts.Where(s => s.BillingPeriodId == billingPeriodId).ToListAsync(ct);
        var isOverdue = DateTime.UtcNow > period.DueDate;

        var totalStatements = statements.Count;
        var paidCount = statements.Count(s => s.Status == "Paid");
        var pendingCount = statements.Count(s => s.BalanceSoles > 0 && !isOverdue);
        var overdueCount = statements.Count(s => s.BalanceSoles > 0 && isOverdue);
        var totalOverdueSoles = isOverdue
            ? statements.Where(s => s.BalanceSoles > 0).Sum(s => s.BalanceSoles)
            : 0.00m;

        var totalCollectedSoles = statements.Sum(s => s.PaidAmountSoles);
        var delinquencyRate = (period.TotalBilledSoles > 0 && isOverdue)
            ? Math.Round((totalOverdueSoles / period.TotalBilledSoles) * 100.00m, 2)
            : 0.00m;

        return new BillingPeriodMetricsDto(
            BillingPeriodId: period.Id,
            PeriodCode: period.PeriodCode,
            TotalBilledSoles: period.TotalBilledSoles,
            TotalCollectedSoles: totalCollectedSoles,
            TotalOverdueSoles: totalOverdueSoles,
            DelinquencyRate: delinquencyRate,
            TotalStatements: totalStatements,
            PaidStatements: paidCount,
            PendingStatements: pendingCount,
            OverdueStatements: overdueCount);
    }

    // Algoritmo de Mayor Resto sobre céntimos enteros (Largest Remainder Method) con desempate determinista
    private static Dictionary<int, long> DistributeCentsByAliquot(decimal totalAmountSoles, IReadOnlyList<Unit> units)
    {
        long totalCents = (long)Math.Round(totalAmountSoles * 100m, MidpointRounding.AwayFromZero);
        if (totalCents == 0 || units.Count == 0)
        {
            return units.ToDictionary(u => u.Id, _ => 0L);
        }

        var rows = units.Select(u =>
        {
            decimal exact = (totalCents * u.AliquotPercentage) / 100.0000m;
            long baseCents = (long)Math.Floor(exact);
            decimal remainder = exact - baseCents;
            return new { UnitId = u.Id, Aliquot = u.AliquotPercentage, BaseCents = baseCents, Remainder = remainder };
        }).ToList();

        long assignedCents = rows.Sum(r => r.BaseCents);
        long missingCents = Math.Max(0L, totalCents - assignedCents);

        // Ordena por mayor resto con desempate determinista por mayor alícuota y menor identificador
        var sorted = rows
            .OrderByDescending(r => r.Remainder)
            .ThenByDescending(r => r.Aliquot)
            .ThenBy(r => r.UnitId)
            .ToList();

        var result = new Dictionary<int, long>();
        foreach (var row in sorted)
        {
            long add = missingCents > 0 ? 1 : 0;
            if (missingCents > 0) missingCents--;
            result[row.UnitId] = row.BaseCents + add;
        }

        return result;
    }
}
