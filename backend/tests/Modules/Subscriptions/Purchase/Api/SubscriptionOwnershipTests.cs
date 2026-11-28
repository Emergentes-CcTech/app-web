using System.Net;
using System.Text.Json;
using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using IoBuild.Api.Subscriptions.Domain.Model.Aggregates;
using Microsoft.Extensions.DependencyInjection;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Subscriptions.Purchase.Api;

/// <summary>
/// La suscripción pertenece al administrador del condominio. Que el id de un usuario coincida con el id de un condominio
/// no le da acceso: antes ese atajo mostraba suscripciones ajenas y ocultaba las propias.
/// </summary>
[Trait("Context", "Subscriptions")]
public sealed class SubscriptionOwnershipTests
{
    [Fact]
    public async Task A_user_whose_id_matches_another_condominium_cannot_see_or_touch_its_subscription()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        int subscriptionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
            db.Projects.Add(new Project { Id = 3, Name = "Condominio de la administradora 30", BuilderId = 30, TotalUnits = 60 });
            var plan = new Plan("Urbano", "", 420m);
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
            var subscription = Subscription.Activate(3, plan, DateTimeOffset.UtcNow);
            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync();
            subscriptionId = subscription.Id;
        }

        var owner = Token(30, "owner30@example.test", "PropertyManager");
        var ownList = JsonDocument.Parse(await (await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/subscriptions", owner)).Content.ReadAsStringAsync()).RootElement;
        var mine = Assert.Single(ownList.EnumerateArray());
        Assert.Equal(3, mine.GetProperty("condominiumId").GetInt32());

        // El usuario 3 no administra el condominio 3
        var other = Token(3, "user3@example.test", "PropertyManager");
        var otherList = JsonDocument.Parse(await (await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/subscriptions", other)).Content.ReadAsStringAsync()).RootElement;
        Assert.Empty(otherList.EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/subscriptions/{subscriptionId}", other)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/subscriptions/{subscriptionId}/cancel", other)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(client, HttpMethod.Post, "/api/v1/subscriptions/payments/sessions", other,
            "{\"builderId\":3,\"planId\":1,\"successUrl\":\"https://s.example\",\"cancelUrl\":\"https://c.example\"}")).StatusCode);
    }
}
