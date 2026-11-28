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
/// El consumo medido no se escribe en el formulario: sale de los sensores del periodo y se compara con el facturado del recibo.
/// </summary>
[Trait("Context", "Proration")]
public sealed class MeasuredConsumptionTests
{
    private const int Project = 950;
    private static string Admin => Token(97, "admin97@example.test", "PropertyManager");
    private static readonly string ThisMonth = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    // Kit automático Urbano: AGUA-01 en la cisterna (ingreso), AGUA-02 en la impulsión y un tablero de energía
    private static async Task SeedAsync(CoreBusinessApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
        db.Projects.Add(new Project { Id = Project, Name = "Torre Medida", BuilderId = 97, TotalUnits = 60 });
        var intake = new Device { Name = "AGUA-01", Type = "WaterFlowSensor", Subsystem = "MainCistern", ProjectId = Project, OwnerId = 97 };
        var impulsion = new Device { Name = "AGUA-02", Type = "WaterFlowSensor", Subsystem = "PressurePumpsRoom", ProjectId = Project, OwnerId = 97 };
        var panel = new Device { Name = "ENERGIA-01", Type = "CurrentTransformerCT", Subsystem = "PressurePumpsRoom", ProjectId = Project, OwnerId = 97 };
        db.Devices.AddRange(intake, impulsion, panel);
        await db.SaveChangesAsync();

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var first = monthStart.AddHours(1);
        var last = DateTime.UtcNow.AddMinutes(-5) > first ? DateTime.UtcNow.AddMinutes(-5) : first.AddMinutes(1);
        void Reading(Device d, string metric, double value, DateTime at) =>
            db.ConsumptionRecords.Add(new ConsumptionRecord { ProjectId = Project, DeviceId = d.Id, Subsystem = d.Subsystem, MetricType = metric, Value = value, TimestampUtc = at });
        Reading(intake, "WaterM3", 100.0, first);
        Reading(intake, "WaterM3", 238.5, last);        // 138.5 m³ de ingreso
        Reading(impulsion, "WaterM3", 50.0, first);
        Reading(impulsion, "WaterM3", 180.0, last);     // la impulsión no se suma: duplicaría el agua
        Reading(panel, "EnergyKwh", 1000.0, first);
        Reading(panel, "EnergyKwh", 1420.25, last);    // 420.25 kWh
        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    [Fact]
    public async Task Measured_consumption_comes_from_the_intake_meter_and_panels_of_the_period()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        var preview = await JsonAsync(await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/utility_bills/measured?condominiumId={Project}&period={ThisMonth}", Admin));
        Assert.Equal(138.5m, preview.GetProperty("waterM3").GetDecimal());
        Assert.Equal(420.25m, preview.GetProperty("energyKwh").GetDecimal());

        var stranger = Token(98, "x@example.test", "PropertyManager");
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/utility_bills/measured?condominiumId={Project}&period={ThisMonth}", stranger)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/utility_bills/measured?condominiumId={Project}&period=noviembre", Admin)).StatusCode);
    }

    [Fact]
    public async Task Saving_a_bill_ignores_a_typed_measured_value_and_stores_the_loss_against_the_sensors()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        var bill = await JsonAsync(await SendAuthorizedAsync(client, HttpMethod.Post, "/api/v1/utility_bills", Admin,
            JsonSerializer.Serialize(new { condominiumId = Project, period = ThisMonth, service = "water", billedConsumption = 150.0m, billedAmount = 975.0m, measuredConsumption = 9999m, dueDate = DateTime.UtcNow.AddDays(15).ToString("yyyy-MM-dd") })));
        Assert.Equal(138.5m, bill.GetProperty("measuredConsumption").GetDecimal());

        using var scope = factory.Services.CreateScope();
        var period = await scope.ServiceProvider.GetRequiredService<IoBuildDbContext>().BillingPeriods.SingleAsync(p => p.ProjectId == Project);
        Assert.Equal(11.5m, period.WaterLossM3);                         // 150 facturados − 138.5 medidos
        Assert.Equal(1, period.StartDate.Day);                            // el periodo es su mes calendario
        Assert.Equal(period.StartDate.AddMonths(1).AddTicks(-1), period.EndDate);
    }
}
