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
    }
}
