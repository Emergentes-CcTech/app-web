using System.Net;
using System.Text.Json;
using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.Extensions.DependencyInjection;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Analytics;

/// <summary>
/// La campana lista las alertas abiertas del usuario: cada administrador ve y cierra solo las de sus condominios.
/// </summary>
[Trait("Context", "Analytics")]
public sealed class AlertsAccessTests
{
    [Fact]
    public async Task Open_alerts_are_limited_to_the_condominiums_of_the_administrator()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        int alertId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
            db.Projects.Add(new Project { Id = 900, Name = "Condominio ajeno", BuilderId = 50 });
            var alert = new AnomalyAlert { ProjectId = 900, DeviceId = 1, AlertType = "StatisticalNightFlow", Title = "Flujo", Description = "Flujo nocturno", Status = "Active" };
            db.AnomalyAlerts.Add(alert);
            await db.SaveChangesAsync();
            alertId = alert.Id;
        }
        var owner = Token(50, "owner@example.test", "PropertyManager");
        var stranger = Token(51, "stranger@example.test", "PropertyManager");

        var mine = await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/alerts?status=open", owner);
        var theirs = await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/alerts?status=open", stranger);
        Assert.Contains(JsonDocument.Parse(await mine.Content.ReadAsStringAsync()).RootElement.EnumerateArray(), a => a.GetProperty("id").GetInt32() == alertId);
        Assert.DoesNotContain(JsonDocument.Parse(await theirs.Content.ReadAsStringAsync()).RootElement.EnumerateArray(), a => a.GetProperty("id").GetInt32() == alertId);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/alerts?condominiumId=900", stranger)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAuthorizedAsync(client, HttpMethod.Patch, $"/api/v1/alerts/{alertId}", stranger, "{\"status\":\"acknowledged\"}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAuthorizedAsync(client, HttpMethod.Patch, $"/api/v1/alerts/{alertId}", owner, "{\"status\":\"acknowledged\"}")).StatusCode);
    }
}
