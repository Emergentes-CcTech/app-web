using System.Net;
using IoBuild.Api.Persistence;
using IoBuild.Api.Proration.Domain.Model.Aggregates;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Proration.Api;

/// <summary>
/// El pago de una cuota con tarjeta solo se da por hecho cuando Stripe lo cobra: sin claves de Stripe la pasarela
/// responde que no está configurada en vez de marcar la cuota como pagada al instante.
/// </summary>
[Trait("Context", "Proration")]
public sealed class FeeCheckoutTests
{
    private const int Project = 970;
    private static string Admin => Token(97, "admin97@example.test", "PropertyManager");

    private static async Task<int> SeedAsync(CoreBusinessApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
        db.Projects.Add(new Project { Id = Project, Name = "Torre Cobro", BuilderId = 97, TotalUnits = 1 });
        var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var period = new BillingPeriod { ProjectId = Project, PeriodCode = start.ToString("yyyy-MM"), StartDate = start, EndDate = start.AddMonths(1).AddDays(-1), DueDate = DateTime.UtcNow.Date.AddDays(10), Status = "Settled" };
        db.BillingPeriods.Add(period);
        await db.SaveChangesAsync();
        var statement = new StatementOfAccount { ProjectId = Project, UnitId = 1, UnitNumber = "101", BillingPeriodId = period.Id, AliquotPercentage = 100m, IndividualAmountSoles = 150m, BalanceSoles = 150m, Status = "Pending" };
        db.StatementOfAccounts.Add(statement);
        await db.SaveChangesAsync();
        return statement.Id;
    }

    private static async Task<StatementOfAccount> ReloadAsync(CoreBusinessApiFactory factory, int id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IoBuildDbContext>().StatementOfAccounts.AsNoTracking().SingleAsync(s => s.Id == id);
    }

    [Fact]
    public async Task Without_stripe_keys_the_fee_is_not_paid_silently()
    {
        await using var factory = new CoreBusinessApiFactory(("Stripe:RestrictedApiKey", ""), ("Stripe:SecretKey", ""), ("Stripe:UseSimulatedPayments", "false"));
        using var client = factory.CreateClient();
        var id = await SeedAsync(factory);

        var response = await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/payments/{id}/checkout-session", Admin);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var statement = await ReloadAsync(factory, id);
        Assert.Equal("Pending", statement.Status);
        Assert.Equal(150m, statement.BalanceSoles);
    }

    [Fact]
    public async Task An_owner_outside_condominium_1_sees_their_pending_fee()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        var id = await SeedAsync(factory);
        int unitId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
            var unit = new Unit(Project, "101", 1, 80m, 100m) { OwnerId = 98 };
            db.Units.Add(unit);
            await db.SaveChangesAsync();
            var statement = await db.StatementOfAccounts.SingleAsync(s => s.Id == id);
            statement.UnitId = unit.Id;
            await db.SaveChangesAsync();
            unitId = unit.Id;
        }
        var owner = Token(98, "owner98@example.test", "Resident");

        // El estado de cuenta pide por unidad, sin condominio: antes se buscaba solo en el condominio 1 y salía vacío
        var response = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/payments?unitId={unitId}", owner);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var fees = System.Text.Json.JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();
        var fee = Assert.Single(fees);
        Assert.Equal("pending", fee.GetProperty("status").GetString());
        Assert.Equal(150m, fee.GetProperty("amount").GetDecimal());

        // Otro administrador no ve las cuotas de este condominio
        var stranger = Token(99, "otro@example.test", "PropertyManager");
        var foreign = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/payments?condominiumId={Project}", stranger);
        Assert.Equal("[]", (await foreign.Content.ReadAsStringAsync()).Trim());
    }

    [Fact]
    public async Task Simulated_mode_only_pays_when_explicitly_enabled()
    {
        await using var factory = new CoreBusinessApiFactory(("Stripe:RestrictedApiKey", ""), ("Stripe:UseSimulatedPayments", "true"));
        using var client = factory.CreateClient();
        var id = await SeedAsync(factory);

        var response = await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/payments/{id}/checkout-session", Admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Paid", (await ReloadAsync(factory, id)).Status);
    }
}
