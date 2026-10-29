using System.Security.Claims;
using IoBuild.Api.Persistence;
using IoBuild.Api.Proration.Domain.Model.Aggregates;
using IoBuild.Api.Proration.Domain.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Proration.Interfaces.REST;

/// <summary>
/// DTO para la conciliación de los recibos matrices de Sedapal y Luz del Sur.
/// </summary>
public sealed record ReconcileMacroBillsRequest(
    string PeriodCode,
    DateTime StartDate,
    DateTime EndDate,
    DateTime DueDate,
    decimal MacroWaterM3,
    decimal MacroWaterAmountSoles,
    string? WaterReceiptImageUrl,
    decimal MacroEnergyKwh,
    decimal MacroEnergyAmountSoles,
    string? EnergyReceiptImageUrl,
    decimal OtherExpensesSoles);

/// <summary>
/// DTO para el registro del comprobante de abono de una cuota de mantenimiento.
/// </summary>
public sealed record RegisterPaymentRequest(
    decimal Amount,
    string PaymentMethod,
    string PaymentReference);

/// <summary>
/// Endpoints REST para el contexto de Prorrateo y Liquidación (Proration).
/// Gestiona la conciliación de recibos matrices y el cálculo de cuotas de áreas comunes bajo la Ley N.° 27157.
/// </summary>
public static class ProrationEndpoints
{
    // Extrae el identificador del usuario autenticado a partir del token
    private static int SelfId(ClaimsPrincipal user) =>
        int.TryParse(user.FindFirst(ClaimTypes.Sid)?.Value, out var id) ? id : 0;

    // Valida que el usuario sea el administrador del condominio
    private static async Task<bool> MayManageProjectAsync(ClaimsPrincipal user, IoBuildDbContext db, int projectId, CancellationToken ct)
    {
        var self = SelfId(user);
        var role = user.FindFirst(ClaimTypes.Role)?.Value;
        if (string.Equals(role, "Builder", StringComparison.OrdinalIgnoreCase) || string.Equals(role, "PropertyManager", StringComparison.OrdinalIgnoreCase))
        {
            return await db.Projects.AnyAsync(p => p.Id == projectId && p.BuilderId == self, ct);
        }
        return false;
    }

    // Valida que el usuario tenga acceso de consulta al condominio (Administrador o Residente)
    private static async Task<bool> MaySeeProjectAsync(ClaimsPrincipal user, IoBuildDbContext db, int projectId, CancellationToken ct)
    {
        var self = SelfId(user);
        if (await db.Projects.AnyAsync(p => p.Id == projectId && p.BuilderId == self, ct)) return true;
        return await db.Units.AnyAsync(u => u.ProjectId == projectId && u.OwnerId == self, ct)
            || await db.UnitOwnerProjections.AnyAsync(p => p.OwnerUserId == self && db.Units.Any(u => u.Id == p.UnitId && u.ProjectId == projectId), ct);
    }

    // Valida que el usuario tenga permiso para abonar el estado de cuenta (Administrador o el Residente titular de la unidad)
    private static async Task<bool> MayPayStatementAsync(ClaimsPrincipal user, IoBuildDbContext db, StatementOfAccount statement, CancellationToken ct)
    {
        var self = SelfId(user);
        var role = user.FindFirst(ClaimTypes.Role)?.Value;
        if (string.Equals(role, "Builder", StringComparison.OrdinalIgnoreCase) || string.Equals(role, "PropertyManager", StringComparison.OrdinalIgnoreCase))
        {
            return await db.Projects.AnyAsync(p => p.Id == statement.ProjectId && p.BuilderId == self, ct);
        }
        return await db.Units.AnyAsync(u => u.Id == statement.UnitId && u.OwnerId == self, ct)
            || await db.UnitOwnerProjections.AnyAsync(p => p.UnitId == statement.UnitId && p.OwnerUserId == self, ct);
    }

    public static void MapProrationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/proration").WithTags("Proration");

        // Concilia los recibos matrices de Sedapal y Luz del Sur contra las mediciones comunales
        group.MapPost("/projects/{projectId:int}/periods/reconcile", async (
            int projectId,
            [FromBody] ReconcileMacroBillsRequest request,
            ClaimsPrincipal user,
            IoBuildDbContext db,
            IProrationSettlementService prorationService,
            CancellationToken ct) =>
        {
            if (!await MayManageProjectAsync(user, db, projectId, ct)) return Results.Forbid();

            try
            {
                var period = await prorationService.ReconcileMacroBillsAsync(
                    projectId,
                    request.PeriodCode,
                    request.StartDate,
                    request.EndDate,
                    request.DueDate,
                    request.MacroWaterM3,
                    request.MacroWaterAmountSoles,
                    request.WaterReceiptImageUrl,
                    request.MacroEnergyKwh,
                    request.MacroEnergyAmountSoles,
                    request.EnergyReceiptImageUrl,
                    request.OtherExpensesSoles,
                    ct);

                return Results.Ok(period);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization();

        // Genera la liquidación y estados de cuenta individuales aplicando las alícuotas legales (Ley N.° 27157)
        group.MapPost("/periods/{billingPeriodId:int}/settle", async (
            int billingPeriodId,
            ClaimsPrincipal user,
            IoBuildDbContext db,
            IProrationSettlementService prorationService,
            CancellationToken ct) =>
        {
            var period = await db.BillingPeriods.FindAsync([billingPeriodId], ct);
            if (period is null) return Results.NotFound(new { message = $"Periodo #{billingPeriodId} no encontrado." });

            if (!await MayManageProjectAsync(user, db, period.ProjectId, ct)) return Results.Forbid();

            try
            {
                var statements = await prorationService.GenerateSettlementAsync(billingPeriodId, ct);
                return Results.Ok(statements);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization();

        // Consulta el histórico de periodos de facturación de un condominio
        group.MapGet("/projects/{projectId:int}/periods", async (
            int projectId,
            ClaimsPrincipal user,
            IoBuildDbContext db,
            CancellationToken ct) =>
        {
            if (!await MaySeeProjectAsync(user, db, projectId, ct)) return Results.Forbid();

            var periods = await db.BillingPeriods
                .Where(p => p.ProjectId == projectId)
                .OrderByDescending(p => p.PeriodCode)
                .ToListAsync(ct);

            return Results.Ok(periods);
        }).RequireAuthorization();

        // Consulta el detalle de un periodo de facturación específico
        group.MapGet("/periods/{billingPeriodId:int}", async (
            int billingPeriodId,
            ClaimsPrincipal user,
            IoBuildDbContext db,
            CancellationToken ct) =>
        {
            var period = await db.BillingPeriods.FindAsync([billingPeriodId], ct);
            if (period is null) return Results.NotFound(new { message = $"Periodo #{billingPeriodId} no encontrado." });

            if (!await MaySeeProjectAsync(user, db, period.ProjectId, ct)) return Results.Forbid();

            return Results.Ok(period);
        }).RequireAuthorization();

        // Consulta todos los recibos y estados de cuenta de un periodo de facturación
        group.MapGet("/periods/{billingPeriodId:int}/statements", async (
            int billingPeriodId,
            ClaimsPrincipal user,
            IoBuildDbContext db,
            CancellationToken ct) =>
        {
            var period = await db.BillingPeriods.FindAsync([billingPeriodId], ct);
            if (period is null) return Results.NotFound(new { message = $"Periodo #{billingPeriodId} no encontrado." });

            if (!await MaySeeProjectAsync(user, db, period.ProjectId, ct)) return Results.Forbid();

            var statements = await db.StatementOfAccounts
                .Where(s => s.BillingPeriodId == billingPeriodId)
                .OrderBy(s => s.UnitNumber)
                .ToListAsync(ct);

            return Results.Ok(statements);
        }).RequireAuthorization();

        // Consulta un estado de cuenta individual
        group.MapGet("/statements/{statementId:int}", async (
            int statementId,
            ClaimsPrincipal user,
            IoBuildDbContext db,
            CancellationToken ct) =>
        {
            var statement = await db.StatementOfAccounts.FindAsync([statementId], ct);
            if (statement is null) return Results.NotFound(new { message = $"Estado de cuenta #{statementId} no encontrado." });

            if (!await MaySeeProjectAsync(user, db, statement.ProjectId, ct)) return Results.Forbid();

            return Results.Ok(statement);
        }).RequireAuthorization();

        // Registra el abono de una cuota de mantenimiento individual (administrador o residente propietario)
        group.MapPost("/statements/{statementId:int}/payments", async (
            int statementId,
            [FromBody] RegisterPaymentRequest request,
            ClaimsPrincipal user,
            IoBuildDbContext db,
            IProrationSettlementService prorationService,
            CancellationToken ct) =>
        {
            var statement = await db.StatementOfAccounts.FindAsync([statementId], ct);
            if (statement is null) return Results.NotFound(new { message = $"Estado de cuenta #{statementId} no encontrado." });

            if (!await MayPayStatementAsync(user, db, statement, ct)) return Results.Forbid();

            if (request.Amount <= 0.00m)
            {
                return Results.BadRequest(new { message = "El importe a abonar debe ser mayor a S/ 0.00." });
            }

            try
            {
                var updated = await prorationService.RegisterPaymentAsync(statementId, request.Amount, request.PaymentMethod, request.PaymentReference, ct);
                return Results.Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization();

        // Consulta los indicadores financieros y métricas de cobranza del periodo
        group.MapGet("/periods/{billingPeriodId:int}/metrics", async (
            int billingPeriodId,
            ClaimsPrincipal user,
            IoBuildDbContext db,
            IProrationSettlementService prorationService,
            CancellationToken ct) =>
        {
            var period = await db.BillingPeriods.FindAsync([billingPeriodId], ct);
            if (period is null) return Results.NotFound(new { message = $"Periodo #{billingPeriodId} no encontrado." });

            if (!await MaySeeProjectAsync(user, db, period.ProjectId, ct)) return Results.Forbid();

            var metrics = await prorationService.GetPeriodMetricsAsync(billingPeriodId, ct);
            return Results.Ok(metrics);
        }).RequireAuthorization();

        // Consulta los indicadores y tasa de morosidad más reciente del condominio
        group.MapGet("/projects/{projectId:int}/delinquency", async (
            int projectId,
            [FromQuery] string? periodCode,
            ClaimsPrincipal user,
            IoBuildDbContext db,
            IProrationSettlementService prorationService,
            CancellationToken ct) =>
        {
            if (!await MaySeeProjectAsync(user, db, projectId, ct)) return Results.Forbid();

            var query = db.BillingPeriods.Where(p => p.ProjectId == projectId);
            if (!string.IsNullOrWhiteSpace(periodCode))
            {
                query = query.Where(p => p.PeriodCode == periodCode);
            }

            var period = await query.OrderByDescending(p => p.PeriodCode).FirstOrDefaultAsync(ct);
            if (period is null)
            {
                return Results.Ok(new
                {
                    projectId,
                    periodCode = periodCode ?? "N/A",
                    delinquencyRate = 0.00m,
                    totalBilledSoles = 0.00m,
                    totalCollectedSoles = 0.00m,
                    totalOverdueSoles = 0.00m,
                    message = "No existen periodos registrados aún para este condominio."
                });
            }

            var metrics = await prorationService.GetPeriodMetricsAsync(period.Id, ct);
            return Results.Ok(metrics);
        }).RequireAuthorization();
    }
}
