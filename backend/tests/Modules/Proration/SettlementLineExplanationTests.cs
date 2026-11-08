using System.Net;
using System.Text.Json;
using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Persistence;
using IoBuild.Api.Proration.Domain.Model.Aggregates;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.Extensions.DependencyInjection;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Proration;

/// <summary>
/// La explicación de la cuota usa los datos reales del periodo y solo la ve el dueño del departamento o su administrador.
/// </summary>
[Trait("Context", "Proration")]
public sealed class SettlementLineExplanationTests
{
    private const int ProjectId = 910, UnitId = 9402, PeriodId = 9100, PreviousPeriodId = 9099, LineId = 9200, AdminId = 70, OwnerId = 71, NeighbourId = 72;

    private static async Task SeedAsync(CoreBusinessApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
        db.Projects.Add(new Project { Id = ProjectId, Name = "Residencial Los Robles", BuilderId = AdminId, TotalUnits = 25 });
        db.Units.Add(new Unit(ProjectId, "402", null, 4, "402") { Id = UnitId, AliquotPercentage = 3.8500m, OwnerId = OwnerId });
        db.BillingPeriods.AddRange(
            new BillingPeriod { Id = PreviousPeriodId, ProjectId = ProjectId, PeriodCode = "2026-09", TotalCommonExpenseSoles = 2550m, Status = "Settled" },
            new BillingPeriod
            {
                Id = PeriodId, ProjectId = ProjectId, PeriodCode = "2026-10", MacroWaterM3 = 420m, MacroWaterAmountSoles = 1680m,
                MacroEnergyKwh = 1450m, MacroEnergyAmountSoles = 1160.50m, TotalCommonExpenseSoles = 2840.50m, Status = "Settled"
            });
        db.StatementOfAccounts.AddRange(
            new StatementOfAccount { Id = LineId - 1, BillingPeriodId = PreviousPeriodId, ProjectId = ProjectId, UnitId = UnitId, UnitNumber = "402", AliquotPercentage = 3.85m, IndividualWaterSoles = 55.83m, IndividualEnergySoles = 42.35m, IndividualAmountSoles = 98.18m, Status = "Paid" },
            new StatementOfAccount { Id = LineId, BillingPeriodId = PeriodId, ProjectId = ProjectId, UnitId = UnitId, UnitNumber = "402", AliquotPercentage = 3.85m, IndividualWaterSoles = 64.68m, IndividualEnergySoles = 44.68m, IndividualAmountSoles = 109.36m, BalanceSoles = 109.36m, Status = "Pending" });
        db.AnomalyAlerts.AddRange(
            new AnomalyAlert { ProjectId = ProjectId, DeviceId = 1, AlertType = "NocturnalWaterLeak", Title = "Fuga", Description = "Fuga", EstimatedOvercostSoles = 150m, DetectedAtUtc = new DateTime(2026, 10, 15, 7, 0, 0, DateTimeKind.Utc), Status = "Resolved" },
            new AnomalyAlert { ProjectId = ProjectId, DeviceId = 2, AlertType = "GatewayOfflineInBasement", Title = "Gateway", Description = "Gateway", EstimatedOvercostSoles = 0m, DetectedAtUtc = new DateTime(2026, 10, 16, 7, 0, 0, DateTimeKind.Utc), Status = "Active" });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task The_owner_sees_the_real_figures_behind_the_fee()
    {
        await using var factory = new CoreBusinessApiFactory();
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        var owner = Token(OwnerId, "owner402@example.test", "Resident");

        var line = JsonDocument.Parse(await (await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/settlement_lines/{LineId}", owner)).Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("2026-10", line.GetProperty("period").GetString());
        Assert.Equal(2840.50m, line.GetProperty("breakdown").GetProperty("commonAreaAmount").GetDecimal());

        var response = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/settlement_lines/{LineId}/explanation", owner);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fee = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("2026-10", fee.GetProperty("period").GetString());
        Assert.Equal(2840.50m, fee.GetProperty("common").GetProperty("total").GetDecimal());
        Assert.Equal(109.36m, fee.GetProperty("mine").GetProperty("total").GetDecimal());
        Assert.Equal(98.18m, fee.GetProperty("previous").GetProperty("totalSoles").GetDecimal());

        // Solo las alertas con costo, con la parte que le toca según su alícuota
        var alert = Assert.Single(fee.GetProperty("alerts").EnumerateArray());
        Assert.Equal("NocturnalWaterLeak", alert.GetProperty("alertType").GetString());
        Assert.Equal(5.78m, alert.GetProperty("myShare").GetDecimal());
    }

    [Fact]
    public async Task Other_residents_and_foreign_administrators_cannot_read_it()
    {
        await using var factory = new CoreBusinessApiFactory();
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        var path = $"/api/v1/settlement_lines/{LineId}/explanation";

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(client, HttpMethod.Get, path, Token(NeighbourId, "neighbour@example.test", "Resident"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(client, HttpMethod.Get, path, Token(99, "foreign@example.test", "PropertyManager"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAuthorizedAsync(client, HttpMethod.Get, path, Token(AdminId, "admin@example.test", "PropertyManager"))).StatusCode);
    }
}
