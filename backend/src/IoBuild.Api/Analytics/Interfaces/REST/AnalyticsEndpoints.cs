using IoBuild.Api.Analytics.Application.Internal.QueryServices;
using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Analytics.Domain.Model.Queries;
using IoBuild.Api.Analytics.Domain.Services;
using IoBuild.Api.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Analytics.Interfaces.REST;

/// <summary>
/// Solicitud de análisis cognitivo de consumos y variaciones para el asistente AI.
/// </summary>
public sealed record AiExplanationRequest(int ProjectId, int? UnitId, string BillingPeriod, string? Question = null);

/// <summary>
/// Endpoints REST para el contexto de Analytics (métricas comunales, alertas de anomalías y diagnóstico AI).
/// </summary>
public static class AnalyticsEndpoints
{
    // Obtiene el identificador del usuario autenticado a partir de sus claims
    private static int SelfId(System.Security.Claims.ClaimsPrincipal user) =>
        int.TryParse(user.FindFirst(System.Security.Claims.ClaimTypes.Sid)?.Value, out var id) ? id : 0;

    // Valida que el usuario tenga acceso al condominio (Administrador o Residente)
    private static async Task<bool> MaySeeProjectAsync(System.Security.Claims.ClaimsPrincipal user, IoBuild.Api.Persistence.IoBuildDbContext db, int projectId, CancellationToken ct)
    {
        var self = SelfId(user);
        if (await db.Projects.AnyAsync(p => p.Id == projectId && p.BuilderId == self, ct)) return true;
        return await db.Units.AnyAsync(u => u.ProjectId == projectId && u.OwnerId == self, ct)
            || await db.UnitOwnerProjections.AnyAsync(p => p.OwnerUserId == self && db.Units.Any(u => u.Id == p.UnitId && u.ProjectId == projectId), ct);
    }

    public static void MapAnalyticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/analytics").WithTags("Analytics");

        // Alertas tempranas de anomalías operativas (fugas nocturnas, sobrecargas eléctricas)
        group.MapGet("/projects/{projectId:int}/alerts", async (int projectId, [FromQuery] string? status, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!await MaySeeProjectAsync(user, db, projectId, ct)) return Results.Forbid();
            var query = db.AnomalyAlerts.Where(a => a.ProjectId == projectId);
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.Status.ToLower() == status.ToLower());
            }
            var alerts = await query.OrderByDescending(a => a.DetectedAtUtc).ToListAsync(ct);
            return Results.Ok(alerts);
        }).RequireAuthorization();

        // Resolución de una alerta de anomalía operativa
        group.MapPost("/projects/{projectId:int}/alerts/{alertId:int}/resolve", async (int projectId, int alertId, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!await MaySeeProjectAsync(user, db, projectId, ct)) return Results.Forbid();
            var alert = await db.AnomalyAlerts.FirstOrDefaultAsync(a => a.Id == alertId && a.ProjectId == projectId, ct);
            if (alert is null) return Results.NotFound(new { message = $"Alerta #{alertId} no encontrada en el condominio #{projectId}" });
            alert.Resolve();
            await db.SaveChangesAsync(ct);
            return Results.Ok(alert);
        }).RequireAuthorization();

        // Consulta de series temporales de consumo en áreas comunes
        group.MapGet("/projects/{projectId:int}/consumption", async (int projectId, [FromQuery] string? subsystem, [FromQuery] DateTime? from, [FromQuery] DateTime? to, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!await MaySeeProjectAsync(user, db, projectId, ct)) return Results.Forbid();
            var query = db.ConsumptionRecords.Where(c => c.ProjectId == projectId);
            if (!string.IsNullOrWhiteSpace(subsystem))
            {
                query = query.Where(c => c.Subsystem.ToLower() == subsystem.ToLower());
            }
            if (from.HasValue) query = query.Where(c => c.TimestampUtc >= from.Value);
            if (to.HasValue) query = query.Where(c => c.TimestampUtc <= to.Value);
            var records = await query.OrderBy(c => c.TimestampUtc).Take(1000).ToListAsync(ct);
            return Results.Ok(records);
        }).RequireAuthorization();

        // Consumo diario de áreas comunes: las lecturas son acumuladas, así que el consumo del día es (máximo - mínimo) por medidor
        group.MapGet("/projects/{projectId:int}/daily-consumption", async (int projectId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!await MaySeeProjectAsync(user, db, projectId, ct)) return Results.Forbid();
            var end = to ?? DateTime.UtcNow;
            var start = from ?? end.AddDays(-30);

            // La cisterna tiene medidor de ingreso (Sedapal) y de impulsión; sumar ambos duplicaría el agua.
            // Se toma el de ingreso (sufijo -IN del kit HaaS), que es el que se concilia con el recibo matriz.
            var waterSensors = await db.Devices
                .Where(d => d.ProjectId == projectId && d.Type == "WaterFlowSensor")
                .Select(d => new { d.Id, d.Name })
                .ToListAsync(ct);
            var intakeIds = waterSensors.Where(d => d.Name.EndsWith("-IN", StringComparison.OrdinalIgnoreCase)).Select(d => d.Id).ToList();
            var excludedWaterIds = intakeIds.Count > 0 ? waterSensors.Select(d => d.Id).Except(intakeIds).ToList() : new List<int>();

            var perDevice = await db.ConsumptionRecords
                .Where(c => c.ProjectId == projectId && c.TimestampUtc >= start && c.TimestampUtc <= end)
                .Where(c => !(c.MetricType == "WaterM3" && excludedWaterIds.Contains(c.DeviceId)))
                .GroupBy(c => new { c.DeviceId, c.MetricType, Day = c.TimestampUtc.Date })
                .Select(g => new { g.Key.MetricType, g.Key.Day, Delta = g.Max(c => c.Value) - g.Min(c => c.Value) })
                .ToListAsync(ct);

            var days = perDevice
                .GroupBy(r => r.Day)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    date = g.Key.ToString("yyyy-MM-dd"),
                    waterM3 = Math.Round(g.Where(r => r.MetricType == "WaterM3").Sum(r => r.Delta), 3),
                    energyKwh = Math.Round(g.Where(r => r.MetricType == "EnergyKwh").Sum(r => r.Delta), 3)
                });
            return Results.Ok(days);
        }).RequireAuthorization();

        // Asistente cognitivo determinístico / IA para explicación de variaciones de consumo
        group.MapPost("/ai/explain", async (AiExplanationRequest request, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, IAiCognitiveService cognitive, CancellationToken ct) =>
        {
            if (!await MaySeeProjectAsync(user, db, request.ProjectId, ct)) return Results.Forbid();
            if (request.UnitId.HasValue)
            {
                var self = SelfId(user);
                var isProjectAdmin = await db.Projects.AnyAsync(p => p.Id == request.ProjectId && p.BuilderId == self, ct);
                if (!isProjectAdmin)
                {
                    var ownsUnit = await db.Units.AnyAsync(u => u.Id == request.UnitId.Value && u.ProjectId == request.ProjectId && u.OwnerId == self, ct)
                        || await db.UnitOwnerProjections.AnyAsync(p => p.UnitId == request.UnitId.Value && p.OwnerUserId == self, ct);
                    if (!ownsUnit) return Results.Forbid();
                }
            }
            var result = await cognitive.ExplainConsumptionVariationAsync(request.ProjectId, request.UnitId, request.BillingPeriod, request.Question ?? "Explicar variación mensual");
            return Results.Ok(result);
        }).RequireAuthorization();

        // Endpoints de métricas existentes
        group.MapGet("/builders/{userId:int}/metrics", async (int userId, System.Security.Claims.ClaimsPrincipal user, IAnalyticsQueryService analytics, CancellationToken ct) =>
        {
            if (SelfId(user) != userId) return Results.Forbid();
            var result = await analytics.Handle(new GetBuilderDashboardQuery(userId), ct);
            return result is null ? Results.NotFound(new { message = "No builder metrics found for the specified user." }) : Results.Ok(result);
        }).RequireAuthorization();
        group.MapGet("/owners/{userId:int}/metrics", async (int userId, System.Security.Claims.ClaimsPrincipal user, IAnalyticsQueryService analytics, CancellationToken ct) =>
        {
            if (SelfId(user) != userId) return Results.Forbid();
            var result = await analytics.Handle(new GetOwnerDashboardQuery(userId), ct);
            return result is null ? Results.NotFound(new { message = "No owner metrics found for the specified user." }) : Results.Ok(result);
        }).RequireAuthorization();
        group.MapGet("/builders/{userId:int}/energy", async (int userId, System.Security.Claims.ClaimsPrincipal user, int? minutes, IAnalyticsQueryService analytics, CancellationToken ct) =>
        {
            if (SelfId(user) != userId) return Results.Forbid();
            var result = await analytics.Handle(new GetBuilderLiveEnergyQuery(userId, Math.Clamp(minutes ?? 10, 1, 60)), ct);
            return Results.Ok(result.Select(point => new { timestamp = point.Timestamp, totalEnergyKwh = point.TotalEnergyKwh }));
        }).RequireAuthorization();
        group.MapGet("/owners/{userId:int}/energy", async (int userId, System.Security.Claims.ClaimsPrincipal user, int? minutes, IAnalyticsQueryService analytics, CancellationToken ct) =>
        {
            if (SelfId(user) != userId) return Results.Forbid();
            var result = await analytics.Handle(new GetOwnerLiveEnergyQuery(userId, Math.Clamp(minutes ?? 10, 1, 60)), ct);
            return Results.Ok(result.Select(point => new { timestamp = point.Timestamp, totalEnergyKwh = point.TotalEnergyKwh }));
        }).RequireAuthorization();
        group.MapGet("/insights", async (int projectId, string? metric, DateTime? startDate, DateTime? endDate, System.Security.Claims.ClaimsPrincipal user, IoBuild.Api.Persistence.IoBuildDbContext db, IAnalyticsQueryService analytics, CancellationToken ct) =>
        {
            if (!await MaySeeProjectAsync(user, db, projectId, ct)) return Results.NotFound(new { message = "No insights found for the specified project." });
            var start = startDate ?? DateTime.UtcNow.AddDays(-30);
            var end = endDate ?? DateTime.UtcNow;
            var result = await analytics.Handle(new GetHistoricalDataQuery(projectId, metric ?? "temperature", start, end), ct);
            return Results.Ok(result);
        }).RequireAuthorization();

        // ── Compatibilidad REST: /api/v1/alerts ──
        app.MapGet("/api/v1/alerts", async ([FromQuery] int? condominiumId, [FromQuery] int? projectId, [FromQuery] string? status, [FromQuery] string? _sort, [FromQuery] string? _order, IoBuildDbContext db, CancellationToken ct) =>
        {
            var targetProjectId = condominiumId ?? projectId;
            var query = db.AnomalyAlerts.AsQueryable();
            if (targetProjectId.HasValue)
            {
                query = query.Where(a => a.ProjectId == targetProjectId.Value);
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                var normStatus = status.Trim().ToLowerInvariant();
                if (normStatus == "open" || normStatus == "active")
                {
                    query = query.Where(a => a.Status.ToLower() == "active" || a.Status.ToLower() == "open");
                }
                else if (normStatus == "resolved")
                {
                    query = query.Where(a => a.Status.ToLower() == "resolved");
                }
                else
                {
                    query = query.Where(a => a.Status.ToLower() == normStatus);
                }
            }

            var alerts = await query.OrderByDescending(a => a.DetectedAtUtc).ToListAsync(ct);
            var result = alerts.Select(a => new
            {
                a.Id,
                CondominiumId = a.ProjectId,
                a.ProjectId,
                a.DeviceId,
                a.Subsystem,
                Type = a.AlertType.ToLowerInvariant(),
                a.AlertType,
                Severity = a.Severity.ToLowerInvariant(),
                a.Title,
                a.Description,
                Message = !string.IsNullOrWhiteSpace(a.Title) ? $"{a.Title}: {a.Description}" : a.Description,
                a.EstimatedOvercostSoles,
                CreatedAt = a.DetectedAtUtc,
                DetectedAtUtc = a.DetectedAtUtc,
                ResolvedAtUtc = a.ResolvedAtUtc,
                Status = a.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) ? "open" : a.Status.ToLowerInvariant()
            });

            return Results.Ok(result);
        }).RequireAuthorization();

        app.MapPatch("/api/v1/alerts/{id:int}", async (int id, [FromBody] System.Text.Json.JsonElement body, IoBuildDbContext db, CancellationToken ct) =>
        {
            var alert = await db.AnomalyAlerts.FindAsync([id], ct);
            if (alert is null) return Results.NotFound();
            alert.Resolve();
            await db.SaveChangesAsync(ct);
            return Results.Ok(new
            {
                alert.Id,
                CondominiumId = alert.ProjectId,
                alert.ProjectId,
                alert.DeviceId,
                alert.Subsystem,
                Type = alert.AlertType.ToLowerInvariant(),
                Severity = alert.Severity.ToLowerInvariant(),
                alert.Title,
                alert.Description,
                Message = !string.IsNullOrWhiteSpace(alert.Title) ? $"{alert.Title}: {alert.Description}" : alert.Description,
                CreatedAt = alert.DetectedAtUtc,
                Status = "resolved"
            });
        }).RequireAuthorization();

        // ── Compatibilidad REST: /api/v1/readings ──
        app.MapGet("/api/v1/readings", async (
            [FromQuery] int? condominiumId,
            [FromQuery] int? projectId,
            [FromQuery] int? deviceId,
            [FromQuery(Name = "timestamp_gte")] DateTimeOffset? from,
            [FromQuery(Name = "timestamp_lte")] DateTimeOffset? to,
            [FromQuery] string? _order,
            [FromQuery] int? _limit,
            System.Security.Claims.ClaimsPrincipal user,
            IoBuildDbContext db,
            CancellationToken ct) =>
        {
            var targetProjectId = condominiumId ?? projectId ?? 1;
            if (!await MaySeeProjectAsync(user, db, targetProjectId, ct)) return Results.Forbid();

            var devices = await db.Devices.Where(d => d.ProjectId == targetProjectId).ToDictionaryAsync(d => d.Id, ct);
            var deviceIds = devices.Keys.ToList();
            var query = db.DeviceTelemetry.Where(t => deviceIds.Contains(t.DeviceId));
            if (deviceId.HasValue) query = query.Where(t => t.DeviceId == deviceId.Value);
            if (from.HasValue) query = query.Where(t => t.OccurredAt >= from.Value);
            if (to.HasValue) query = query.Where(t => t.OccurredAt <= to.Value);

            // Se toman las más recientes y luego se ordena según lo pedido, para que el límite no corte el tramo actual
            var limit = Math.Clamp(_limit ?? 500, 1, 2000);
            var readings = await query.OrderByDescending(t => t.OccurredAt).Take(limit).ToListAsync(ct);
            if (!string.Equals(_order, "desc", StringComparison.OrdinalIgnoreCase)) readings.Reverse();

            // El simulador reporta lectura acumulada (m³ o kWh) y una magnitud instantánea (caudal m³/h o potencia kW) en ReportedJson
            static double? Read(System.Text.Json.JsonElement root, params string[] keys)
            {
                foreach (var key in keys)
                {
                    if (root.TryGetProperty(key, out var prop) && prop.ValueKind == System.Text.Json.JsonValueKind.Number) return prop.GetDouble();
                }
                return null;
            }

            var result = readings
                .Where(t => devices.TryGetValue(t.DeviceId, out var d) && d.Type != "GatewayHub")
                .Select(t =>
                {
                    var isWater = devices[t.DeviceId].Type == "WaterFlowSensor";
                    double? cumulative = null, rate = null;
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(t.ReportedJson) ? "{}" : t.ReportedJson);
                        cumulative = Read(doc.RootElement, "cumulative_value", isWater ? "cumulative_m3" : "cumulative_kwh");
                        rate = Read(doc.RootElement, isWater ? "flow_m3h" : "power_kw", isWater ? "flow_rate_m3h" : "power");
                    }
                    catch (System.Text.Json.JsonException) { /* lectura sin carga útil válida: se omite su valor */ }

                    return new
                    {
                        t.Id,
                        t.DeviceId,
                        CondominiumId = targetProjectId,
                        Timestamp = t.OccurredAt.ToString("o"),
                        Value = cumulative ?? (isWater ? 0 : t.EnergyKwh),
                        Unit = isWater ? "m³" : "kWh",
                        Rate = rate,
                        RateUnit = isWater ? "m³/h" : "kW"
                    };
                });
            return Results.Ok(result);
        }).RequireAuthorization();
    }
}
