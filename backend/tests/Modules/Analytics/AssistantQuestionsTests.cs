using System.Net;
using System.Text.Json;
using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Analytics.Infrastructure.Adapters;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Persistence;
using IoBuild.Api.Proration.Domain.Model.Aggregates;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using IoBuild.Api.Publishing.Domain.Model.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Analytics;

/// <summary>
/// El asistente responde preguntas libres sobre los gastos con datos reales: deuda y vencimiento, historial de cuotas,
/// consumo por área común y cálculo de la cuota. Sin cuotas emitidas, "Mi cuota actual" muestra una vista previa.
/// </summary>
[Trait("Context", "Analytics")]
public sealed class AssistantQuestionsTests
{
    private const int Project = 950;

    private static async Task<(IoBuildDbContext Db, int UnitId)> SeedAsync(bool withFees = true)
    {
        var db = new IoBuildDbContext(new DbContextOptionsBuilder<IoBuildDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await SeedInto(db, withFees);
        return (db, db.Units.Single().Id);
    }

    private static async Task SeedInto(IoBuildDbContext db, bool withFees)
    {
        db.Projects.Add(new Project { Id = Project, Name = "Torre Sol", BuilderId = 60, TotalUnits = 20 });
        var unit = new Unit(Project, "402", 4, 85m, 5m);
        db.Units.Add(unit);
        db.CommonAreas.Add(new CommonArea(Project, "Cisterna", CommonAreaSubsystem.MainCistern, "Sótano"));
        db.CommonAreas.Add(new CommonArea(Project, "Ascensores", CommonAreaSubsystem.ElevatorsBank, "Azotea"));
        db.Devices.Add(new Device { Id = 901, Name = "AGUA-01", Type = "WaterFlowSensor", Subsystem = "MainCistern", ProjectId = Project, Status = "Online" });
        db.Devices.Add(new Device { Id = 902, Name = "ENERGIA-01", Type = "CurrentTransformerCT", Subsystem = "ElevatorsBank", ProjectId = Project, Status = "Online" });
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        db.ConsumptionRecords.AddRange(
            new ConsumptionRecord { ProjectId = Project, DeviceId = 901, MetricType = "WaterM3", Value = 100, TimestampUtc = monthStart.AddHours(1) },
            new ConsumptionRecord { ProjectId = Project, DeviceId = 901, MetricType = "WaterM3", Value = 112.5, TimestampUtc = monthStart.AddHours(5) },
            new ConsumptionRecord { ProjectId = Project, DeviceId = 902, MetricType = "EnergyKwh", Value = 200, TimestampUtc = monthStart.AddHours(1) },
            new ConsumptionRecord { ProjectId = Project, DeviceId = 902, MetricType = "EnergyKwh", Value = 260, TimestampUtc = monthStart.AddHours(5) });
        await db.SaveChangesAsync();
        if (!withFees) return;

        var september = new BillingPeriod { ProjectId = Project, PeriodCode = "2026-09", DueDate = new DateTime(2026, 10, 15), TotalCommonExpenseSoles = 4000m, MacroWaterAmountSoles = 2500m, MacroEnergyAmountSoles = 1500m, Status = "Settled" };
        var october = new BillingPeriod { ProjectId = Project, PeriodCode = "2026-10", DueDate = DateTime.UtcNow.Date.AddDays(-3), TotalCommonExpenseSoles = 5000m, MacroWaterAmountSoles = 3000m, MacroEnergyAmountSoles = 2000m, Status = "Settled" };
        db.BillingPeriods.AddRange(september, october);
        await db.SaveChangesAsync();
        db.StatementOfAccounts.AddRange(
            new StatementOfAccount { ProjectId = Project, UnitId = unit.Id, UnitNumber = "402", BillingPeriodId = september.Id, AliquotPercentage = 5m, IndividualAmountSoles = 200m, BalanceSoles = 0m, Status = "Paid", PaidAtUtc = new DateTime(2026, 10, 10) },
            new StatementOfAccount { ProjectId = Project, UnitId = unit.Id, UnitNumber = "402", BillingPeriodId = october.Id, AliquotPercentage = 5m, IndividualAmountSoles = 250m, BalanceSoles = 250m, Status = "Pending" });
        await db.SaveChangesAsync();
    }

    private static Task<IoBuild.Api.Analytics.Domain.Services.AiExplanationDto> AskAsync(IoBuildDbContext db, int unitId, string question, string period = "") =>
        new MockAiCognitiveAdapter(db).ExplainConsumptionVariationAsync(Project, unitId, period, question);

    [Fact]
    public async Task Due_date_and_debt_questions_use_the_unit_fee_history()
    {
        var (db, unitId) = await SeedAsync();
        await using var _ = db;

        var answer = await AskAsync(db, unitId, "¿Cuándo vence mi cuota y cuánto debo?");

        Assert.Contains("Debe S/ 250.00 en 1 cuota(s)", answer.Summary);
        Assert.Contains("venció", answer.Summary);
    }

    [Fact]
    public async Task History_questions_add_up_the_last_fees()
    {
        var (db, unitId) = await SeedAsync();
        await using var _ = db;

        var answer = await AskAsync(db, unitId, "¿Cuánto he pagado en los últimos meses?");

        Assert.Contains("suman S/ 450.00", answer.Summary);
        Assert.Contains("pagó S/ 200.00", answer.Summary);
    }

    [Fact]
    public async Task Common_area_questions_name_the_area_that_consumes_the_most()
    {
        var (db, unitId) = await SeedAsync();
        await using var _ = db;

        var answer = await AskAsync(db, unitId, "¿Qué área común consume más energía?", $"{DateTime.UtcNow:yyyy-MM}");

        Assert.Contains("Ascensores (60.00 kWh)", answer.Summary);
        Assert.Contains("Cisterna (12.50 m³)", answer.Summary);
    }

    [Fact]
    public async Task Calculation_questions_explain_the_share_with_the_latest_common_expense()
    {
        var (db, unitId) = await SeedAsync();
        await using var _ = db;

        var answer = await AskAsync(db, unitId, "¿Cómo se calcula mi cuota?");

        Assert.Contains("participación de 5.0000 %", answer.Summary);
        Assert.Contains("S/ 5,000.00", answer.Summary);
        Assert.Contains("S/ 250.00", answer.Summary);
    }

    [Fact]
    public async Task Other_questions_keep_the_monthly_variation_explanation()
    {
        var (db, unitId) = await SeedAsync();
        await using var _ = db;

        var answer = await AskAsync(db, unitId, "¿Por qué varió mi cuota?", "2026-10");

        Assert.Contains("frente a S/ 200.00", answer.Summary);
    }

    [Fact]
    public async Task Improvement_questions_get_concrete_actions_from_the_data()
    {
        var (db, unitId) = await SeedAsync();
        await using var _ = db;

        var answer = await AskAsync(db, unitId, "Los propietarios se quejan de los gastos, ¿qué podríamos mejorar?", $"{DateTime.UtcNow:yyyy-MM}");

        Assert.Contains("Ascensores concentra el 100 % de la energía medida", answer.Summary);
        Assert.Contains(answer.Recommendations, r => r.Contains("ascensores"));
        Assert.Contains(answer.Recommendations, r => r.Contains("Mi cuota actual"));
    }

    [Fact]
    public async Task Open_questions_the_rules_cannot_answer_say_so_instead_of_answering_something_else()
    {
        var (db, unitId) = await SeedAsync();
        await using var _ = db;

        var open = await AskAsync(db, unitId, "¿Cuántos residentes hay registrados en el edificio?");
        Assert.StartsWith("Sin el asistente de IA solo puedo responder", open.Summary);

        var variation = await AskAsync(db, unitId, "¿Por qué varió mi cuota?", "2026-10");
        Assert.DoesNotContain("Sin el asistente de IA", variation.Summary);
    }

    [Fact]
    public async Task Before_the_first_fee_the_owner_gets_a_preview_with_an_estimate()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        int unitId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
            await SeedInto(db, withFees: false);
            // El edificio ya tiene un gasto común registrado aunque este departamento aún no tenga cuota
            db.BillingPeriods.Add(new BillingPeriod { ProjectId = Project, PeriodCode = "2026-10", DueDate = new DateTime(2026, 11, 15), TotalCommonExpenseSoles = 8000m, Status = "Reconciled" });
            var unit = db.Units.Single(u => u.ProjectId == Project);
            unit.OwnerId = 61;
            await db.SaveChangesAsync();
            unitId = unit.Id;
        }

        var owner = Token(61, "owner61@example.test", "Resident");
        var response = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/settlement_lines/preview?unitId={unitId}", owner);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("402", body.GetProperty("unitNumber").GetString());
        Assert.Equal(5m, body.GetProperty("participationPct").GetDecimal());
        Assert.Equal(400.00m, body.GetProperty("estimatedFee").GetDecimal());          // 8,000 × 5 %
        Assert.Equal(12.5, body.GetProperty("currentMonth").GetProperty("waterM3").GetDouble());

        var stranger = Token(62, "x@example.test", "Resident");
        Assert.Equal(HttpStatusCode.NotFound, (await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/settlement_lines/preview?unitId={unitId}", stranger)).StatusCode);
    }
}
