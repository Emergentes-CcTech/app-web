using System.Globalization;
using System.Net;
using System.Text.Json;
using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Proration.Api;

/// <summary>
/// El recibo se concilia con lo medido en SU periodo de lectura (las fechas impresas en el recibo), no en el mes calendario,
/// y la luz cuenta solo el tablero general cuando existe, porque ya incluye bombas y ascensores.
/// </summary>
[Trait("Context", "Proration")]
public sealed class ReadingPeriodTests
{
    private const int Project = 960;
    private static string Admin => Token(99, "admin99@example.test", "PropertyManager");
    // Mes pasado: todas las lecturas ya ocurrieron
    private static readonly DateTime Month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
    private static readonly string Period = Month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    private static string Day(int offset) => Month.AddDays(offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task SeedAsync(CoreBusinessApiFactory factory, bool withGeneralPanel)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
        db.Projects.Add(new Project { Id = Project, Name = "Torre Ciclo", BuilderId = 99, TotalUnits = 35 });
        var water = new Device { Name = "AGUA-01", Type = "WaterFlowSensor", Subsystem = "MainCistern", ProjectId = Project, OwnerId = 99 };
        var pumps = new Device { Name = "ENERGIA-02", Type = "CurrentTransformerCT", Subsystem = "PressurePumpsRoom", ProjectId = Project, OwnerId = 99 };
        db.Devices.AddRange(water, pumps);
        Device? general = null;
        if (withGeneralPanel)
        {
            general = new Device { Name = "ENERGIA-01", Type = "CurrentTransformerCT", Subsystem = "GeneralElectricalPanel", ProjectId = Project, OwnerId = 99 };
            db.Devices.Add(general);
        }
        await db.SaveChangesAsync();

        void Reading(Device d, string metric, double value, int day) =>
            db.ConsumptionRecords.Add(new ConsumptionRecord { ProjectId = Project, DeviceId = d.Id, Subsystem = d.Subsystem, MetricType = metric, Value = value, TimestampUtc = Month.AddDays(day).AddHours(12) });
        // Agua: 100 → 150 dentro del ciclo (días 2 a 10) y sigue hasta 230 el día 20
        Reading(water, "WaterM3", 100, 2);
        Reading(water, "WaterM3", 150, 10);
        Reading(water, "WaterM3", 230, 20);
        // Luz del ciclo: el tablero de bombas suma 150 kWh y el general (que ya las incluye) 400 kWh
        Reading(pumps, "EnergyKwh", 1000, 2);
        Reading(pumps, "EnergyKwh", 1150, 10);
        if (general is not null)
        {
            Reading(general, "EnergyKwh", 5000, 2);
            Reading(general, "EnergyKwh", 5400, 10);
        }
        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    [Fact]
    public async Task Measured_consumption_only_counts_the_bill_reading_period_and_the_general_panel()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, withGeneralPanel: true);

        var cycle = await JsonAsync(await SendAuthorizedAsync(client, HttpMethod.Get,
            $"/api/v1/utility_bills/measured?condominiumId={Project}&period={Period}&from={Day(1)}&to={Day(12)}", Admin));
        Assert.Equal(50m, cycle.GetProperty("waterM3").GetDecimal());          // la lectura del día 20 queda fuera del recibo
        Assert.Equal(400m, cycle.GetProperty("energyKwh").GetDecimal());       // solo el tablero general, sin sumar bombas otra vez

        var wholeMonth = await JsonAsync(await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/utility_bills/measured?condominiumId={Project}&period={Period}", Admin));
        Assert.Equal(130m, wholeMonth.GetProperty("waterM3").GetDecimal());    // sin fechas: el mes calendario completo
    }

    [Fact]
    public async Task Without_a_general_panel_the_dedicated_panels_are_added()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, withGeneralPanel: false);

        var cycle = await JsonAsync(await SendAuthorizedAsync(client, HttpMethod.Get,
            $"/api/v1/utility_bills/measured?condominiumId={Project}&period={Period}&from={Day(1)}&to={Day(12)}", Admin));
        Assert.Equal(150m, cycle.GetProperty("energyKwh").GetDecimal());
    }

    [Fact]
    public async Task Saving_a_bill_keeps_its_reading_period_and_rejects_impossible_dates()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, withGeneralPanel: true);

        string Bill(string service, decimal billed, string from, string to) => JsonSerializer.Serialize(new
        {
            condominiumId = Project, period = Period, service, billedConsumption = billed, billedAmount = 500m,
            dueDate = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-dd"), readingFrom = from, readingTo = to
        });

        var water = await JsonAsync(await SendAuthorizedAsync(client, HttpMethod.Post, "/api/v1/utility_bills", Admin, Bill("water", 55m, Day(1), Day(12))));
        Assert.Equal(50m, water.GetProperty("measuredConsumption").GetDecimal());
        Assert.Equal(Day(1), water.GetProperty("readingFrom").GetString());
        Assert.Equal(Day(12), water.GetProperty("readingTo").GetString());
        // La luz puede tener otro ciclo: se guarda aparte
        await JsonAsync(await SendAuthorizedAsync(client, HttpMethod.Post, "/api/v1/utility_bills", Admin, Bill("electricity", 420m, Day(0), Day(14))));

        var list = (await JsonAsync(await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/utility_bills?condominiumId={Project}", Admin))).EnumerateArray().ToList();
        var listedWater = list.Single(b => b.GetProperty("service").GetString() == "water");
        Assert.Equal(50m, listedWater.GetProperty("measuredConsumption").GetDecimal());
        var listedEnergy = list.Single(b => b.GetProperty("service").GetString() == "electricity");
        Assert.Equal(Day(0), listedEnergy.GetProperty("readingFrom").GetString());
        Assert.Equal(400m, listedEnergy.GetProperty("measuredConsumption").GetDecimal());

        using (var scope = factory.Services.CreateScope())
        {
            var period = await scope.ServiceProvider.GetRequiredService<IoBuildDbContext>().BillingPeriods.SingleAsync(p => p.ProjectId == Project);
            Assert.Equal(5m, period.WaterLossM3);       // 55 facturados − 50 medidos en el ciclo
            Assert.Equal(20m, period.EnergyLossKwh);    // 420 − 400
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await SendAuthorizedAsync(client, HttpMethod.Post, "/api/v1/utility_bills", Admin, Bill("water", 55m, Day(12), Day(1)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAuthorizedAsync(client, HttpMethod.Post, "/api/v1/utility_bills", Admin, Bill("water", 55m, Day(-80), Day(1)))).StatusCode);
    }
}
