using System.Globalization;
using System.Net;
using System.Text.Json;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Persistence;
using IoBuild.Api.Proration.Domain.Model.Aggregates;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Shared.Demo;

/// <summary>
/// Herramientas de demostración: alertas generadas con las reglas del motor y adelanto de un mes para mostrar morosidad.
/// </summary>
[Trait("Context", "Demo")]
public sealed class DemoEndpointsTests
{
    private const int Project = 940;
    private static string Admin => Token(95, "admin95@example.test", "PropertyManager");
    private static readonly string ThisMonth = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    private static readonly string LastMonth = DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static async Task SeedAsync(CoreBusinessApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
        db.Projects.Add(new Project { Id = Project, Name = "Torre Demo", BuilderId = 95, TotalUnits = 2 });
        db.Devices.AddRange(
            new Device { Name = "AGUA-01", Type = "WaterFlowSensor", Subsystem = "MainCistern", ProjectId = Project, OwnerId = 95 },
            new Device { Name = "ENERGIA-01", Type = "CurrentTransformerCT", Subsystem = "PressurePumpsRoom", ProjectId = Project, OwnerId = 95 },
            new Device { Name = "GW-01", Type = "GatewayHub", Subsystem = "MainCistern", ProjectId = Project, OwnerId = 95 });
        // Cuota del mes actual que vence en 10 días: hoy nadie está moroso
        var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var period = new BillingPeriod { ProjectId = Project, PeriodCode = ThisMonth, StartDate = start, EndDate = start.AddMonths(1).AddDays(-1), DueDate = DateTime.UtcNow.Date.AddDays(10), Status = "Settled",
            WaterReadingFrom = start, WaterReadingTo = start.AddDays(20), EnergyReadingFrom = start.AddDays(-2), EnergyReadingTo = start.AddDays(18) };
        db.BillingPeriods.Add(period);
        await db.SaveChangesAsync();
        var paidAt = DateTime.UtcNow.AddDays(-1);
        db.StatementOfAccounts.AddRange(
            new StatementOfAccount { ProjectId = Project, UnitId = 1, UnitNumber = "101", BillingPeriodId = period.Id, AliquotPercentage = 50m, IndividualAmountSoles = 100m, BalanceSoles = 0m, Status = "Paid", PaidAtUtc = paidAt },
            new StatementOfAccount { ProjectId = Project, UnitId = 2, UnitNumber = "102", BillingPeriodId = period.Id, AliquotPercentage = 50m, IndividualAmountSoles = 100m, BalanceSoles = 100m, Status = "Pending" });
        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body[..Math.Min(body.Length, 600)]}");
        return JsonDocument.Parse(body).RootElement;
    }

    [Fact]
    public async Task Is_hidden_when_demo_mode_is_off()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        Assert.False((await JsonAsync(await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/demo/status", Admin))).GetProperty("enabled").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/demo/condominiums/{Project}/alerts", Admin, """{"type":"leak"}""")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/demo/condominiums/{Project}/advance-month", Admin)).StatusCode);
    }

    [Fact]
    public async Task Generates_alerts_with_the_engine_rules_and_resolves_them()
    {
        await using var factory = new CoreBusinessApiFactory(("Demo:Enabled", "true"));
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        var leak = await JsonAsync(await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/demo/condominiums/{Project}/alerts", Admin, """{"type":"leak"}"""));
        Assert.Equal("NocturnalWaterLeak", leak.GetProperty("alertType").GetString());
        Assert.True(leak.GetProperty("estimatedOvercostSoles").GetDecimal() > 0);
        // Registrada en la ventana nocturna de la regla (01:00–04:00 en Lima)
        var limaHour = DateTime.SpecifyKind(leak.GetProperty("detectedAtUtc").GetDateTime(), DateTimeKind.Utc).AddHours(-5).Hour;
        Assert.InRange(limaHour, 1, 3);

        Assert.Equal("PumpElectricalOverload", (await JsonAsync(await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/demo/condominiums/{Project}/alerts", Admin, """{"type":"overload"}"""))).GetProperty("alertType").GetString());
        Assert.Equal("GatewayOfflineInBasement", (await JsonAsync(await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/demo/condominiums/{Project}/alerts", Admin, """{"type":"gateway"}"""))).GetProperty("alertType").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/demo/condominiums/{Project}/alerts", Admin, """{"type":"otra"}""")).StatusCode);

        var resolved = await JsonAsync(await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/demo/condominiums/{Project}/alerts/resolve-all", Admin));
        Assert.Equal(3, resolved.GetProperty("resolved").GetInt32());

        var stranger = Token(96, "x@example.test", "PropertyManager");
        Assert.Equal(HttpStatusCode.NotFound, (await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/demo/condominiums/{Project}/alerts", stranger, """{"type":"leak"}""")).StatusCode);
    }

    [Fact]
    public async Task Advancing_a_month_turns_the_unpaid_fee_overdue_and_keeps_history_consistent()
    {
        await using var factory = new CoreBusinessApiFactory(("Demo:Enabled", "true"));
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        var before = await JsonAsync(await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/demo/condominiums/{Project}", Admin));
        Assert.Equal(0, before.GetProperty("periods")[0].GetProperty("overdue").GetInt32());

        var response = await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/demo/condominiums/{Project}/advance-month", Admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = (await JsonAsync(response)).GetProperty("periods")[0];
        Assert.Equal(LastMonth, after.GetProperty("period").GetString());
        Assert.Equal(1, after.GetProperty("paid").GetInt32());
        Assert.Equal(1, after.GetProperty("overdue").GetInt32());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
        var paid = await db.StatementOfAccounts.SingleAsync(s => s.ProjectId == Project && s.Status == "Paid");
        Assert.True(paid.PaidAtUtc < DateTime.UtcNow.AddDays(-25));
        Assert.False(await db.BillingPeriods.AnyAsync(p => p.ProjectId == Project && p.PeriodCode == ThisMonth));
        // El periodo de lectura de los recibos se movió con su mes
        var moved = await db.BillingPeriods.SingleAsync(p => p.ProjectId == Project);
        var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(start.AddMonths(-1), moved.WaterReadingFrom);
        Assert.Equal(start.AddDays(-2).AddMonths(-1), moved.EnergyReadingFrom);
    }
}
