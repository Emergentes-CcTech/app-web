using System.Net;
using System.Text.Json;
using IoBuild.Api.Persistence;
using IoBuild.Api.Subscriptions.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Devices.Provisioning.Api;

/// <summary>
/// El condominio se crea pendiente; al confirmarse el pago de activación pasa a activo y recibe el kit del plan
/// que le corresponde por su número de departamentos, instalado en las áreas comunes que registró.
/// </summary>
[Trait("Context", "Devices")]
public sealed class KitProvisioningTests
{
    private static string Admin => Token(90, "admin90@example.test", "PropertyManager");

    private static string Condominium(int units, params (string Name, string Subsystem)[] areas) => JsonSerializer.Serialize(new
    {
        name = "Residencial Las Flores",
        address = "Av. Las Flores 123",
        district = "Surco",
        totalUnits = units,
        totalFloors = 8,
        commonAreas = areas.Select(a => new { name = a.Name, subsystem = a.Subsystem, location = "Sótano" })
    });

    private static async Task<int> CreateAsync(HttpClient client, string body)
    {
        var response = await SendAuthorizedAsync(client, HttpMethod.Post, "/api/v1/condominiums", Admin, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt32();
    }

    // Confirma un pago simulado de Stripe (sesión cs_sim_{condominio}_{plan}) del plan que le corresponde
    private static async Task PayActivationAsync(CoreBusinessApiFactory factory, HttpClient client, int condominiumId, string planName)
    {
        int planId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
            var plan = await db.Plans.FirstOrDefaultAsync(p => p.Name == planName);
            if (plan is null)
            {
                plan = new Plan(planName, "", 420m);
                db.Plans.Add(plan);
                await db.SaveChangesAsync();
            }
            planId = plan.Id;
        }
        var confirm = await client.PatchAsync($"/api/v1/subscriptions/payments/sessions/cs_sim_{condominiumId}_{planId}_test", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
    }

    private static async Task<string> StatusAsync(HttpClient client, int id) =>
        JsonDocument.Parse(await (await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/condominiums/{id}", Admin)).Content.ReadAsStringAsync())
            .RootElement.GetProperty("status").GetString()!;

    [Fact]
    public async Task A_new_condominium_waits_for_payment_and_paying_activates_it_with_the_kit_of_its_plan()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        // 65 departamentos → plan Urbano: 1 gateway, 2 puntos de agua y 2 tableros de energía
        var id = await CreateAsync(client, Condominium(65, ("Cisterna", "MainCistern"), ("Cuarto de bombas", "PressurePumpsRoom"), ("Ascensores", "ElevatorsBank")));
        Assert.Equal("pending", await StatusAsync(client, id));
        using (var before = factory.Services.CreateScope())
            Assert.False(await before.ServiceProvider.GetRequiredService<IoBuildDbContext>().Devices.AnyAsync(d => d.ProjectId == id));

        await PayActivationAsync(factory, client, id, "Urbano");
        Assert.Equal("active", await StatusAsync(client, id));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
        var devices = await db.Devices.Where(d => d.ProjectId == id).OrderBy(d => d.Id).ToListAsync();
        Assert.Equal(5, devices.Count);
        Assert.Single(devices, d => d.Type == "GatewayHub");
        Assert.Equal(["MainCistern", "PressurePumpsRoom"], devices.Where(d => d.Type == "WaterFlowSensor").Select(d => d.Subsystem));
        Assert.Equal(["PressurePumpsRoom", "ElevatorsBank"], devices.Where(d => d.Type == "CurrentTransformerCT").Select(d => d.Subsystem));
        Assert.All(devices, d => Assert.Equal(90, d.OwnerId));
        // Recién instalados: no cuentan como equipos que nunca dieron señal
        Assert.All(devices, d => Assert.NotNull(d.LastHeartbeatUtc));
        Assert.Equal(5, devices.Select(d => d.MacAddress).Distinct().Count());
        Assert.Equal(5, await db.DeviceProjections.CountAsync(p => p.ProjectId == id));
        Assert.Contains(devices, d => d.Location == "Cisterna · Punto de agua");
    }

    [Fact]
    public async Task Editing_an_active_condominium_adds_only_what_a_bigger_plan_is_missing_and_never_removes_devices()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        // 30 departamentos → Esencial: 3 equipos
        var id = await CreateAsync(client, Condominium(30, ("Cisterna", "MainCistern")));
        await PayActivationAsync(factory, client, id, "Esencial");

        async Task<int> CountAsync()
        {
            using var scope = factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IoBuildDbContext>().Devices.CountAsync(d => d.ProjectId == id);
        }
        Assert.Equal(3, await CountAsync());

        // Guardar sin cambiar el número de departamentos no duplica el kit
        Assert.Equal(HttpStatusCode.OK, (await SendAuthorizedAsync(client, HttpMethod.Put, $"/api/v1/condominiums/{id}", Admin, Condominium(30, ("Cisterna", "MainCistern")))).StatusCode);
        Assert.Equal(3, await CountAsync());

        // Pasa a 90 departamentos → Gran Torre: 7 equipos
        await SendAuthorizedAsync(client, HttpMethod.Put, $"/api/v1/condominiums/{id}", Admin, Condominium(90, ("Cisterna", "MainCistern")));
        Assert.Equal(7, await CountAsync());

        // Volver a un plan menor no retira equipos instalados
        await SendAuthorizedAsync(client, HttpMethod.Put, $"/api/v1/condominiums/{id}", Admin, Condominium(30, ("Cisterna", "MainCistern")));
        Assert.Equal(7, await CountAsync());
    }
}
