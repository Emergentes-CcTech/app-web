using System.Net;
using System.Text.Json;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Persistence;
using IoBuild.Api.Proration.Domain.Model.Aggregates;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.Extensions.DependencyInjection;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Proration;

/// <summary>
/// Antes de publicar: recibos, liquidaciones, auditoría y equipos solo los ve quien pertenece al condominio,
/// y el prorrateo nunca se genera con montos supuestos.
/// </summary>
[Trait("Context", "Proration")]
public sealed class ProductionAccessTests
{
    private const int Project = 910;

    private static async Task SeedAsync(CoreBusinessApiFactory factory, bool withBills)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
        db.Projects.Add(new Project { Id = Project, Name = "Condominio del administrador", BuilderId = 60 });
        db.Devices.Add(new Device { Id = 9101, ProjectId = Project, Name = "WFS-AJENO", Type = "WaterFlowSensor" });
        db.BillingPeriods.Add(new BillingPeriod
        {
            Id = 9102, ProjectId = Project, PeriodCode = "2026-10", Status = "Open",
            MacroWaterAmountSoles = withBills ? 1680 : 0, MacroEnergyAmountSoles = withBills ? 1160.50m : 0,
            ReconciledAtUtc = withBills ? new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc) : null
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Strangers_cannot_read_bills_settlements_audit_or_devices_of_another_condominium()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, withBills: true);
        var owner = Token(60, "owner@example.test", "PropertyManager");
        var stranger = Token(61, "stranger@example.test", "PropertyManager");

        foreach (var path in new[] { $"/api/v1/utility_bills?condominiumId={Project}", $"/api/v1/settlements?condominiumId={Project}", $"/api/v1/audit_events?condominiumId={Project}" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(client, HttpMethod.Get, path, stranger)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await SendAuthorizedAsync(client, HttpMethod.Get, path, owner)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/devices/9101", stranger)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/devices/9101/status", stranger)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/devices/9101", owner)).StatusCode);
        var list = JsonDocument.Parse(await (await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/devices?condominiumId={Project}", stranger)).Content.ReadAsStringAsync());
        Assert.Empty(list.RootElement.EnumerateArray());

        Assert.Equal(HttpStatusCode.NotFound, (await SendAuthorizedAsync(client, HttpMethod.Patch, "/api/v1/settlements/9102", stranger, "{\"status\":\"issued\"}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(client, HttpMethod.Post, "/api/v1/settlements", stranger, $"{{\"condominiumId\":{Project},\"period\":\"2026-10\"}}")).StatusCode);
    }

    [Fact]
    public async Task Audit_events_come_from_real_records()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, withBills: true);

        var response = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/audit_events?condominiumId={Project}&type=proration", Token(60, "owner@example.test", "PropertyManager"));
        var events = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.EnumerateArray().ToList();
        var only = Assert.Single(events);
        Assert.Equal("Periodo 2026-10", only.GetProperty("entity").GetString());
    }

    [Fact]
    public async Task Settlement_is_not_generated_without_the_utility_bills()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, withBills: false);

        var response = await SendAuthorizedAsync(client, HttpMethod.Post, "/api/v1/settlements", Token(60, "owner@example.test", "PropertyManager"), $"{{\"condominiumId\":{Project},\"period\":\"2026-10\"}}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAuthorizedAsync(client, HttpMethod.Post, "/api/v1/settlements", Token(60, "owner@example.test", "PropertyManager"), "{}")).StatusCode);
    }
}
