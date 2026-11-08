using IoBuild.Api.Analytics.Application.Internal.QueryServices;
using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Analytics.Infrastructure.Adapters;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Persistence;
using IoBuild.Api.Proration.Domain.Model.Aggregates;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Modules.Tests.Analytics;

/// <summary>
/// Los hechos que recibe el explicador: consumo real del periodo frente al anterior, recibos y cuota del departamento.
/// </summary>
public sealed class ConsumptionFactsTests
{
    private static async Task<IoBuildDbContext> SeededAsync()
    {
        var db = new IoBuildDbContext(new DbContextOptionsBuilder<IoBuildDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Projects.Add(new Project { Id = 10, Name = "Residencial Los Robles", TotalUnits = 25 });
        db.Units.Add(new Unit(10, "402", null, 4, "402") { Id = 402, AliquotPercentage = 3.8500m });
        db.Devices.AddRange(
            new Device { Id = 1, ProjectId = 10, Name = "WFS-CIS-IN", Type = "WaterFlowSensor", NightFlowThresholdM3H = 0.1500m },
            new Device { Id = 2, ProjectId = 10, Name = "WFS-CIS-OUT", Type = "WaterFlowSensor", NightFlowThresholdM3H = 0.1500m },
            new Device { Id = 3, ProjectId = 10, Name = "CT-BOMBAS", Type = "CurrentTransformerCT" });

        // Lecturas acumuladas: septiembre 100 → 400 m³ (300), octubre 400 → 760 m³ (360); la impulsión no suma
        void Reading(int device, string metric, double value, DateTime at) => db.ConsumptionRecords.Add(new ConsumptionRecord
            { ProjectId = 10, DeviceId = device, MetricType = metric, Subsystem = "MainCistern", Value = value, TimestampUtc = at });
        Reading(1, "WaterM3", 100, new DateTime(2026, 9, 1, 6, 0, 0, DateTimeKind.Utc));
        Reading(1, "WaterM3", 400, new DateTime(2026, 9, 30, 6, 0, 0, DateTimeKind.Utc));
        Reading(1, "WaterM3", 400, new DateTime(2026, 10, 1, 6, 0, 0, DateTimeKind.Utc));
        Reading(1, "WaterM3", 760, new DateTime(2026, 10, 31, 6, 0, 0, DateTimeKind.Utc));
        Reading(2, "WaterM3", 0, new DateTime(2026, 10, 1, 6, 0, 0, DateTimeKind.Utc));
        Reading(2, "WaterM3", 900, new DateTime(2026, 10, 31, 6, 0, 0, DateTimeKind.Utc));
        Reading(3, "EnergyKwh", 1000, new DateTime(2026, 10, 1, 6, 0, 0, DateTimeKind.Utc));
        Reading(3, "EnergyKwh", 1800, new DateTime(2026, 10, 31, 6, 0, 0, DateTimeKind.Utc));

        db.BillingPeriods.AddRange(
            new BillingPeriod { Id = 1, ProjectId = 10, PeriodCode = "2026-09", MacroWaterM3 = 310, MacroWaterAmountSoles = 1450, MacroEnergyKwh = 750, MacroEnergyAmountSoles = 1100, TotalCommonExpenseSoles = 2550, Status = "Settled" },
            new BillingPeriod { Id = 2, ProjectId = 10, PeriodCode = "2026-10", MacroWaterM3 = 372, MacroWaterAmountSoles = 1680, MacroEnergyKwh = 820, MacroEnergyAmountSoles = 1160.50m, WaterLossM3 = 12, TotalCommonExpenseSoles = 2840.50m, Status = "Settled" });
        db.StatementOfAccounts.AddRange(
            new StatementOfAccount { Id = 1, BillingPeriodId = 1, ProjectId = 10, UnitId = 402, UnitNumber = "402", IndividualWaterSoles = 55.83m, IndividualEnergySoles = 42.35m, IndividualOtherExpensesSoles = 0, IndividualAmountSoles = 98.18m, Status = "Paid" },
            new StatementOfAccount { Id = 2, BillingPeriodId = 2, ProjectId = 10, UnitId = 402, UnitNumber = "402", IndividualWaterSoles = 64.68m, IndividualEnergySoles = 44.68m, IndividualOtherExpensesSoles = 0, IndividualAmountSoles = 109.36m, Status = "Pending" });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Without_a_period_the_latest_statement_is_explained_against_the_previous_month()
    {
        await using var db = await SeededAsync();

        var facts = await new ConsumptionFactsBuilder(db).BuildAsync(10, 402, null);

        Assert.Equal("2026-10", facts.Period);
        Assert.Equal("2026-09", facts.PreviousPeriod);
        Assert.False(facts.PeriodInProgress);
        Assert.Equal(360, facts.Consumption.WaterM3);          // solo el medidor de ingreso
        Assert.Equal(300, facts.PreviousConsumption.WaterM3);
        Assert.Equal(800, facts.Consumption.EnergyKwh);
        Assert.Equal(109.36m, facts.Statement!.TotalSoles);
        Assert.Equal(98.18m, facts.PreviousStatement!.TotalSoles);
        Assert.Equal(2840.50m, facts.Bill!.TotalCommonExpenseSoles);
    }

    [Fact]
    public async Task The_rules_explanation_follows_the_interface_language()
    {
        await using var db = await SeededAsync();

        var explanation = await new MockAiCognitiveAdapter(db).ExplainConsumptionVariationAsync(10, 402, "", "Why did my fee change?", "en");

        Assert.Contains("Your fee was S/ 109.36 compared with S/ 98.18 in September 2026 (+11.4%), mainly because of water (+S/ 8.85)", explanation.Summary);
        Assert.Contains("Common-area water measured by IoBuild", explanation.WaterAnalysis);
        Assert.Contains("With its legal aliquot of 3.8500%", explanation.AliquotImpact);
        Assert.DoesNotContain("Departamento", explanation.Summary + explanation.AliquotImpact);
    }

    [Fact]
    public async Task The_rules_explanation_answers_why_the_fee_changed_with_real_figures()
    {
        await using var db = await SeededAsync();

        var explanation = await new MockAiCognitiveAdapter(db).ExplainConsumptionVariationAsync(10, 402, "", "¿Por qué varió mi cuota?");

        Assert.Equal("2026-10", explanation.Period);
        Assert.Contains("S/ 109.36 frente a S/ 98.18", explanation.Summary);
        Assert.Contains("+11.4%", explanation.Summary);
        Assert.Contains("el agua (+S/ 8.85)", explanation.Summary);
        Assert.Contains("360.00 m³, frente a 300.00 m³", explanation.WaterAnalysis);
        Assert.Contains("+20.0%", explanation.WaterAnalysis);
        Assert.Contains("3.8500%", explanation.AliquotImpact);
        Assert.Contains("S/ 109.36", explanation.AliquotImpact); // 3.85% de S/ 2,840.50
        Assert.False(explanation.HasAnomalies);
    }
}
