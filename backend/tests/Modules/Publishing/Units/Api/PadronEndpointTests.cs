using System.Net;
using System.Text.Json;
using IoBuild.Api.Persistence;
using IoBuild.Api.Proration.Domain.Model.Aggregates;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Publishing.Units.Api;

/// <summary>
/// El padrón se guarda completo: área, tipo y participación tal como los ingresa el administrador,
/// siempre que sumen 100 % y sin borrar unidades con historial.
/// </summary>
[Trait("Context", "Publishing")]
public sealed class PadronEndpointTests
{
    private const int Project = 920;

    private static async Task SeedAsync(CoreBusinessApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
        // Un departamento declarado: los estacionamientos y depósitos no cuentan para ese límite
        db.Projects.Add(new Project { Id = Project, Name = "Edificio de la demo", BuilderId = 70, TotalUnits = 1 });
        await db.SaveChangesAsync();
    }

    private static string Admin => Token(70, "admin@example.test", "PropertyManager");

    [Fact]
    public async Task Saves_area_type_and_participation_as_entered()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        var body = """{"units":[{"number":"401","type":"apartment","areaM2":105,"participationPct":60},{"number":"E-01","type":"parking","areaM2":12.5,"participationPct":40}]}""";
        var response = await SendAuthorizedAsync(client, HttpMethod.Put, $"/api/v1/condominiums/{Project}/units", Admin, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var units = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.EnumerateArray().ToList();
        var dept = Assert.Single(units, u => u.GetProperty("number").GetString() == "401");
        Assert.Equal(105m, dept.GetProperty("areaM2").GetDecimal());
        Assert.Equal(60m, dept.GetProperty("participationPct").GetDecimal());
        Assert.Equal(4, dept.GetProperty("floor").GetInt32());
        Assert.Equal("parking", units.Single(u => u.GetProperty("number").GetString() == "E-01").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Rejects_more_apartments_than_the_condominium_declares()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        var response = await SendAuthorizedAsync(client, HttpMethod.Put, $"/api/v1/condominiums/{Project}/units", Admin,
            """{"units":[{"number":"101","type":"apartment","areaM2":80,"participationPct":50},{"number":"102","type":"apartment","areaM2":80,"participationPct":50}]}""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("too_many_apartments", body.GetProperty("code").GetString());
        Assert.Equal(2, body.GetProperty("apartments").GetInt32());
        Assert.Equal(1, body.GetProperty("declared").GetInt32());
    }

    [Fact]
    public async Task Rejects_a_registry_that_does_not_add_up_to_100()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        var response = await SendAuthorizedAsync(client, HttpMethod.Put, $"/api/v1/condominiums/{Project}/units", Admin,
            """{"units":[{"number":"101","areaM2":80,"participationPct":10}]}""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("100", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Keeps_units_with_settlement_history_and_hides_the_registry_from_strangers()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        int unitId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
            var unit = new Unit(Project, "101", 1, 80m, 100m);
            db.Units.Add(unit);
            await db.SaveChangesAsync();
            unitId = unit.Id;
            db.StatementOfAccounts.Add(new StatementOfAccount { ProjectId = Project, UnitId = unitId, UnitNumber = "101", BillingPeriodId = 1, AliquotPercentage = 100m, IndividualAmountSoles = 10m, Status = "Pending" });
            await db.SaveChangesAsync();
        }

        var replace = await SendAuthorizedAsync(client, HttpMethod.Put, $"/api/v1/condominiums/{Project}/units", Admin,
            """{"units":[{"number":"201","areaM2":80,"participationPct":100}]}""");
        Assert.Equal(HttpStatusCode.Conflict, replace.StatusCode);

        var stranger = await SendAuthorizedAsync(client, HttpMethod.Put, $"/api/v1/condominiums/{Project}/units", Token(71, "x@example.test", "PropertyManager"),
            $$"""{"units":[{"id":{{unitId}},"number":"101","areaM2":80,"participationPct":100}]}""");
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);

        using var check = factory.Services.CreateScope();
        Assert.True(await check.ServiceProvider.GetRequiredService<IoBuildDbContext>().Units.AnyAsync(u => u.Id == unitId));
    }
}
