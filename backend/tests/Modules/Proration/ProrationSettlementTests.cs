using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Persistence;
using IoBuild.Api.Proration.Application.Internal.CommandServices;
using IoBuild.Api.Proration.Domain.Model.Aggregates;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IoBuild.Modules.Tests.Proration;

/// <summary>
/// Pruebas unitarias para el contexto de Prorrateo y Liquidación (Proration).
/// Valida la conciliación de recibos matrices con mediciones comunales IoT,
/// la exigencia de la alícuota legal al 100.0000% (Ley N.° 27157), el cuadre de céntimos
/// por el método del mayor resto, pagos parciales y el cálculo de la morosidad según vencimiento.
/// </summary>
public sealed class ProrationSettlementTests
{
    private static IoBuildDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<IoBuildDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IoBuildDbContext(options);
    }

    [Fact]
    public async Task ReconcileMacroBills_CalculatesLossesAndTotalExpensesCorrectly()
    {
        await using var db = CreateInMemoryDb();
        var projectId = 10;
        var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc);
        var due = new DateTime(2026, 11, 15, 23, 59, 59, DateTimeKind.Utc);

        // Registro de telemetría de caudalímetro en cisterna (420.50 m3 acumulados)
        db.ConsumptionRecords.Add(new ConsumptionRecord
        {
            ProjectId = projectId,
            DeviceId = 1,
            Subsystem = "MainCistern",
            MetricType = "WaterM3",
            Value = 420.50,
            TimestampUtc = start.AddDays(15)
        });

        // Registro de telemetría de transformador de corriente en tableros (1250.00 kWh acumulados)
        db.ConsumptionRecords.Add(new ConsumptionRecord
        {
            ProjectId = projectId,
            DeviceId = 2,
            Subsystem = "PressurePumpsRoom",
            MetricType = "EnergyKwh",
            Value = 1250.00,
            TimestampUtc = start.AddDays(15)
        });

        await db.SaveChangesAsync();

        var service = new ProrationSettlementService(db);

        // Recibo matriz Sedapal: 450.00 m3 por S/ 1,800.00
        // Recibo matriz Luz del Sur: 1300.00 kWh por S/ 1,200.00
        // Otros gastos comunes: S/ 500.00
        var period = await service.ReconcileMacroBillsAsync(
            projectId: projectId,
            periodCode: "2026-10",
            startDate: start,
            endDate: end,
            dueDate: due,
            macroWaterM3: 450.00m,
            macroWaterAmountSoles: 1800.00m,
            waterReceiptImageUrl: "https://storage.iobuild.pe/receipts/sedapal-202610.pdf",
            macroEnergyKwh: 1300.00m,
            macroEnergyAmountSoles: 1200.00m,
            energyReceiptImageUrl: "https://storage.iobuild.pe/receipts/luzdelsur-202610.pdf",
            otherExpensesSoles: 500.00m);

        Assert.Equal("Reconciled", period.Status);
        Assert.Equal(420.50m, period.MeasuredCommonWaterM3);
        Assert.Equal(1250.00m, period.MeasuredCommonEnergyKwh);
        Assert.Equal(29.50m, period.WaterLossM3);
        Assert.Equal(50.00m, period.EnergyLossKwh);
        Assert.Equal(3500.00m, period.TotalCommonExpenseSoles);
    }

    [Fact]
    public async Task ReconcileMacroBills_RejectsInvalidInputParameters_AndClosedPeriods()
    {
        await using var db = CreateInMemoryDb();
        var service = new ProrationSettlementService(db);
        var now = DateTime.UtcNow;

        // Fecha de fin anterior a fecha de inicio
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReconcileMacroBillsAsync(1, "2026-10", now.AddDays(5), now, now.AddDays(10), 10, 10, null, 10, 10, null, 0));

        // Periodo cerrado
        db.BillingPeriods.Add(new BillingPeriod { ProjectId = 1, PeriodCode = "2026-09", Status = "Closed" });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReconcileMacroBillsAsync(1, "2026-09", now, now.AddDays(10), now.AddDays(20), 10, 10, null, 10, 10, null, 0));
    }

    [Fact]
    public async Task GenerateSettlement_EnforcesExact100AliquotInvariant_ThrowsWhenNot100()
    {
        await using var db = CreateInMemoryDb();
        var projectId = 20;

        var period = new BillingPeriod
        {
            ProjectId = projectId,
            PeriodCode = "2026-10",
            TotalCommonExpenseSoles = 2000.00m,
            Status = "Reconciled"
        };
        db.BillingPeriods.Add(period);

        db.Units.AddRange(
            new Unit { ProjectId = projectId, UnitNumber = "101", AliquotPercentage = 50.0000m },
            new Unit { ProjectId = projectId, UnitNumber = "102", AliquotPercentage = 40.0000m }
        );
        await db.SaveChangesAsync();

        var service = new ProrationSettlementService(db);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateSettlementAsync(period.Id));

        Assert.Contains("Ley N.° 27157", exception.Message);
        Assert.Contains("100.0000%", exception.Message);
    }

    [Fact]
    public async Task GenerateSettlement_RejectsZeroOrNegativeAliquots()
    {
        await using var db = CreateInMemoryDb();
        var projectId = 25;

        var period = new BillingPeriod
        {
            ProjectId = projectId,
            PeriodCode = "2026-10",
            TotalCommonExpenseSoles = 2000.00m,
            Status = "Reconciled"
        };
        db.BillingPeriods.Add(period);

        db.Units.AddRange(
            new Unit { ProjectId = projectId, UnitNumber = "101", AliquotPercentage = 100.0000m },
            new Unit { ProjectId = projectId, UnitNumber = "102", AliquotPercentage = 0.0000m }
        );
        await db.SaveChangesAsync();

        var service = new ProrationSettlementService(db);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateSettlementAsync(period.Id));

        Assert.Contains("mayores a 0%", exception.Message);
    }

    [Fact]
    public async Task GenerateSettlement_DistributesExpensesAccordingToAliquot_AndAdjustsPennyDifference_WithLargestRemainder()
    {
        await using var db = CreateInMemoryDb();
        var projectId = 30;

        var period = new BillingPeriod
        {
            ProjectId = projectId,
            PeriodCode = "2026-10",
            MacroWaterAmountSoles = 500.00m,
            MacroEnergyAmountSoles = 300.00m,
            OtherCommonExpensesSoles = 200.01m,
            TotalCommonExpenseSoles = 1000.01m,
            Status = "Reconciled"
        };
        db.BillingPeriods.Add(period);

        // 3 unidades con alícuotas exactas que suman 100.0000%
        db.Units.AddRange(
            new Unit { ProjectId = projectId, UnitNumber = "101", ResidentFullName = "Ana Gómez", ResidentEmail = "ana@ejemplo.com", AliquotPercentage = 33.3333m },
            new Unit { ProjectId = projectId, UnitNumber = "102", ResidentFullName = "Beto Pérez", ResidentEmail = "beto@ejemplo.com", AliquotPercentage = 33.3333m },
            new Unit { ProjectId = projectId, UnitNumber = "103", ResidentFullName = "Carlos Mora", ResidentEmail = "carlos@ejemplo.com", AliquotPercentage = 33.3334m }
        );
        await db.SaveChangesAsync();

        var service = new ProrationSettlementService(db);
        var statements = await service.GenerateSettlementAsync(period.Id);

        Assert.Equal(3, statements.Count);

        // La suma de todos los estados de cuenta debe cuadrar exactamente con TotalCommonExpenseSoles
        var totalDistributed = statements.Sum(s => s.IndividualAmountSoles);
        Assert.Equal(period.TotalCommonExpenseSoles, totalDistributed);

        // Cada componente parcial debe cuadrar exactamente
        Assert.Equal(period.MacroWaterAmountSoles, statements.Sum(s => s.IndividualWaterSoles));
        Assert.Equal(period.MacroEnergyAmountSoles, statements.Sum(s => s.IndividualEnergySoles));
        Assert.Equal(period.OtherCommonExpensesSoles, statements.Sum(s => s.IndividualOtherExpensesSoles));

        // Todos los estados de cuenta deben tener saldo inicial igual a su importe y abono 0
        Assert.All(statements, s =>
        {
            Assert.Equal(s.IndividualAmountSoles, s.BalanceSoles);
            Assert.Equal(0.00m, s.PaidAmountSoles);
            Assert.Equal("Pending", s.Status);
        });

        var updatedPeriod = await db.BillingPeriods.FindAsync(period.Id);
        Assert.NotNull(updatedPeriod);
        Assert.Equal("Settled", updatedPeriod.Status);
        Assert.Equal(1000.01m, updatedPeriod.TotalBilledSoles);
    }

    [Fact]
    public async Task GenerateSettlement_RejectsRegeneration_WhenPaymentsAlreadyExist()
    {
        await using var db = CreateInMemoryDb();
        var projectId = 35;

        var period = new BillingPeriod
        {
            ProjectId = projectId,
            PeriodCode = "2026-10",
            TotalCommonExpenseSoles = 1000.00m,
            Status = "Settled"
        };
        db.BillingPeriods.Add(period);

        db.Units.Add(new Unit { ProjectId = projectId, UnitNumber = "101", AliquotPercentage = 100.0000m });
        db.StatementOfAccounts.Add(new StatementOfAccount
        {
            BillingPeriodId = period.Id,
            ProjectId = projectId,
            UnitId = 1,
            UnitNumber = "101",
            IndividualAmountSoles = 1000.00m,
            PaidAmountSoles = 500.00m,
            BalanceSoles = 500.00m,
            Status = "PartiallyPaid"
        });
        await db.SaveChangesAsync();

        var service = new ProrationSettlementService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateSettlementAsync(period.Id));

        Assert.Contains("ya existen abonos registrados", ex.Message);
    }

    [Fact]
    public async Task RegisterPayment_SupportsPartialPayment_AndUpdatesBalancesCorrectly()
    {
        await using var db = CreateInMemoryDb();
        var projectId = 40;

        var period = new BillingPeriod
        {
            ProjectId = projectId,
            PeriodCode = "2026-10",
            TotalCommonExpenseSoles = 1000.00m,
            MacroWaterAmountSoles = 600.00m,
            MacroEnergyAmountSoles = 400.00m,
            DueDate = DateTime.UtcNow.AddDays(10), // Aún no vencido
            Status = "Reconciled"
        };
        db.BillingPeriods.Add(period);

        db.Units.AddRange(
            new Unit { ProjectId = projectId, UnitNumber = "101", AliquotPercentage = 50.0000m },
            new Unit { ProjectId = projectId, UnitNumber = "102", AliquotPercentage = 50.0000m }
        );
        await db.SaveChangesAsync();

        var service = new ProrationSettlementService(db);
        var statements = await service.GenerateSettlementAsync(period.Id);

        var statement1 = statements.First(s => s.UnitNumber == "101");
        Assert.Equal(500.00m, statement1.IndividualAmountSoles);
        Assert.Equal(500.00m, statement1.BalanceSoles);

        // Abono parcial de S/ 200.00
        var part1 = await service.RegisterPaymentAsync(statement1.Id, 200.00m, "BCP Transferencia", "OP-001");
        Assert.Equal("PartiallyPaid", part1.Status);
        Assert.Equal(200.00m, part1.PaidAmountSoles);
        Assert.Equal(300.00m, part1.BalanceSoles);

        // Segundo abono de S/ 300.00 que cancela el saldo total
        var part2 = await service.RegisterPaymentAsync(statement1.Id, 300.00m, "BBVA Transferencia", "OP-002");
        Assert.Equal("Paid", part2.Status);
        Assert.Equal(500.00m, part2.PaidAmountSoles);
        Assert.Equal(0.00m, part2.BalanceSoles);
        Assert.NotNull(part2.PaidAtUtc);

        var updatedPeriod = await db.BillingPeriods.FindAsync(period.Id);
        Assert.NotNull(updatedPeriod);
        Assert.Equal(500.00m, updatedPeriod.TotalCollectedSoles);
    }

    [Fact]
    public async Task RegisterPayment_RejectsOverpayment_AndZeroAmounts()
    {
        await using var db = CreateInMemoryDb();
        var statement = new StatementOfAccount
        {
            BillingPeriodId = 1,
            ProjectId = 1,
            UnitId = 1,
            UnitNumber = "101",
            IndividualAmountSoles = 500.00m,
            BalanceSoles = 200.00m,
            PaidAmountSoles = 300.00m,
            Status = "PartiallyPaid"
        };
        db.StatementOfAccounts.Add(statement);
        await db.SaveChangesAsync();

        var service = new ProrationSettlementService(db);

        // Intento de pago con monto <= 0
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RegisterPaymentAsync(statement.Id, 0.00m, "POS", "REF-0"));

        // Intento de sobrepago (S/ 250.00 cuando el saldo es S/ 200.00)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegisterPaymentAsync(statement.Id, 250.00m, "POS", "REF-1"));

        Assert.Contains("excede el saldo pendiente", ex.Message);
    }

    [Fact]
    public async Task GetPeriodMetrics_CalculatesMetricsAndOverdueCorrectly_RespectingDueDate()
    {
        await using var db = CreateInMemoryDb();
        var projectId = 50;

        // Periodo vencido
        var period = new BillingPeriod
        {
            ProjectId = projectId,
            PeriodCode = "2026-10",
            TotalCommonExpenseSoles = 1500.00m,
            DueDate = DateTime.UtcNow.AddDays(-1), // Vencido
            Status = "Settled",
            TotalBilledSoles = 1500.00m,
            TotalCollectedSoles = 500.00m
        };
        db.BillingPeriods.Add(period);
        await db.SaveChangesAsync();

        db.StatementOfAccounts.AddRange(
            new StatementOfAccount { BillingPeriodId = period.Id, ProjectId = projectId, UnitId = 1, UnitNumber = "101", IndividualAmountSoles = 500.00m, PaidAmountSoles = 500.00m, BalanceSoles = 0.00m, Status = "Paid" },
            new StatementOfAccount { BillingPeriodId = period.Id, ProjectId = projectId, UnitId = 2, UnitNumber = "102", IndividualAmountSoles = 500.00m, PaidAmountSoles = 0.00m, BalanceSoles = 500.00m, Status = "Pending" },
            new StatementOfAccount { BillingPeriodId = period.Id, ProjectId = projectId, UnitId = 3, UnitNumber = "103", IndividualAmountSoles = 500.00m, PaidAmountSoles = 0.00m, BalanceSoles = 500.00m, Status = "Pending" }
        );
        await db.SaveChangesAsync();

        var service = new ProrationSettlementService(db);
        var metrics = await service.GetPeriodMetricsAsync(period.Id);

        Assert.Equal(period.Id, metrics.BillingPeriodId);
        Assert.Equal("2026-10", metrics.PeriodCode);
        Assert.Equal(1500.00m, metrics.TotalBilledSoles);
        Assert.Equal(500.00m, metrics.TotalCollectedSoles);
        Assert.Equal(1000.00m, metrics.TotalOverdueSoles);
        Assert.Equal(66.67m, metrics.DelinquencyRate);
        Assert.Equal(3, metrics.TotalStatements);
        Assert.Equal(1, metrics.PaidStatements);
        Assert.Equal(0, metrics.PendingStatements); // Como está vencido, los que tienen saldo van a Overdue
        Assert.Equal(2, metrics.OverdueStatements);
    }
}
