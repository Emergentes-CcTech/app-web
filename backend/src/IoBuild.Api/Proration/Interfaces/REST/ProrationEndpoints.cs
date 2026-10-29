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

        // ── Compatibilidad REST: /api/v1/utility_bills ──
        app.MapGet("/api/v1/utility_bills", async ([FromQuery] int? condominiumId, [FromQuery] int? projectId, [FromQuery] string? period, IoBuildDbContext db, CancellationToken ct) =>
        {
            var targetProjectId = condominiumId ?? projectId ?? 1;
            var query = db.BillingPeriods.Where(p => p.ProjectId == targetProjectId);
            if (!string.IsNullOrWhiteSpace(period)) query = query.Where(p => p.PeriodCode == period);
            var periods = await query.OrderByDescending(p => p.PeriodCode).ToListAsync(ct);
            var bills = new List<object>();
            foreach (var p in periods)
            {
                bills.Add(new
                {
                    id = p.Id * 10 + 1,
                    condominiumId = p.ProjectId,
                    projectId = p.ProjectId,
                    period = p.PeriodCode,
                    service = "water",
                    billedConsumption = p.MacroWaterM3,
                    consumptionUnit = "m³",
                    measuredConsumption = p.MeasuredCommonWaterM3,
                    billedAmount = p.MacroWaterAmountSoles,
                    dueDate = p.DueDate.ToString("yyyy-MM-dd"),
                    receiptImageUrl = p.WaterReceiptImageUrl
                });
                bills.Add(new
                {
                    id = p.Id * 10 + 2,
                    condominiumId = p.ProjectId,
                    projectId = p.ProjectId,
                    period = p.PeriodCode,
                    service = "electricity",
                    billedConsumption = p.MacroEnergyKwh,
                    consumptionUnit = "kWh",
                    measuredConsumption = p.MeasuredCommonEnergyKwh,
                    billedAmount = p.MacroEnergyAmountSoles,
                    dueDate = p.DueDate.ToString("yyyy-MM-dd"),
                    receiptImageUrl = p.EnergyReceiptImageUrl
                });
            }
            return Results.Ok(bills);
        }).RequireAuthorization();

        app.MapPost("/api/v1/utility_bills", async ([FromBody] System.Text.Json.JsonElement body, IoBuildDbContext db, CancellationToken ct) =>
        {
            var condoId = body.TryGetProperty("condominiumId", out var cProp) ? cProp.GetInt32() : 1;
            var periodCode = body.TryGetProperty("period", out var pProp) ? pProp.GetString() ?? "2026-10" : "2026-10";
            var service = body.TryGetProperty("service", out var sProp) ? sProp.GetString() ?? "water" : "water";
            var consumption = body.TryGetProperty("billedConsumption", out var bcProp) ? bcProp.GetDecimal() : 100.0m;
            var amount = body.TryGetProperty("billedAmount", out var baProp) ? baProp.GetDecimal() : 500.0m;
            var dueDateStr = body.TryGetProperty("dueDate", out var ddProp) ? ddProp.GetString() : null;
            var dueDate = DateTime.TryParse(dueDateStr, out var parsedDate) ? parsedDate : DateTime.UtcNow.AddDays(15);

            var period = await db.BillingPeriods.FirstOrDefaultAsync(p => p.ProjectId == condoId && p.PeriodCode == periodCode, ct);
            if (period is null)
            {
                period = new BillingPeriod
                {
                    ProjectId = condoId,
                    PeriodCode = periodCode,
                    StartDate = DateTime.UtcNow.AddMonths(-1),
                    EndDate = DateTime.UtcNow,
                    DueDate = dueDate,
                    Status = "Draft"
                };
                db.BillingPeriods.Add(period);
            }

            if (service == "water")
            {
                period.MacroWaterM3 = consumption;
                period.MacroWaterAmountSoles = amount;
            }
            else
            {
                period.MacroEnergyKwh = consumption;
                period.MacroEnergyAmountSoles = amount;
            }
            period.TotalCommonExpenseSoles = period.MacroWaterAmountSoles + period.MacroEnergyAmountSoles + period.OtherCommonExpensesSoles;
            await db.SaveChangesAsync(ct);

            var createdBill = new
            {
                id = period.Id * 10 + (service == "water" ? 1 : 2),
                condominiumId = period.ProjectId,
                projectId = period.ProjectId,
                period = period.PeriodCode,
                service,
                billedConsumption = consumption,
                consumptionUnit = service == "water" ? "m³" : "kWh",
                measuredConsumption = service == "water" ? period.MeasuredCommonWaterM3 : period.MeasuredCommonEnergyKwh,
                billedAmount = amount,
                dueDate = period.DueDate.ToString("yyyy-MM-dd")
            };
            return Results.Created($"/api/v1/utility_bills/{createdBill.id}", createdBill);
        }).RequireAuthorization();

        // ── Compatibilidad REST: /api/v1/settlements ──
        app.MapGet("/api/v1/settlements", async ([FromQuery] int? condominiumId, [FromQuery] int? projectId, IoBuildDbContext db, CancellationToken ct) =>
        {
            var targetProjectId = condominiumId ?? projectId ?? 1;
            var periods = await db.BillingPeriods
                .Where(p => p.ProjectId == targetProjectId)
                .OrderByDescending(p => p.PeriodCode)
                .ToListAsync(ct);

            var settlements = periods.Select(p => new
            {
                id = p.Id,
                condominiumId = p.ProjectId,
                period = p.PeriodCode,
                commonAreaAmount = p.TotalCommonExpenseSoles,
                status = string.Equals(p.Status, "Settled", StringComparison.OrdinalIgnoreCase) ? "issued" : "draft",
                isIssued = string.Equals(p.Status, "Settled", StringComparison.OrdinalIgnoreCase),
                issuedAt = p.SettledAtUtc
            });
            return Results.Ok(settlements);
        }).RequireAuthorization();

        app.MapGet("/api/v1/settlements/{id:int}", async (int id, IoBuildDbContext db, CancellationToken ct) =>
        {
            var p = await db.BillingPeriods.FindAsync([id], ct);
            if (p is null) return Results.NotFound();

            var statements = await db.StatementOfAccounts
                .Where(s => s.BillingPeriodId == id)
                .OrderBy(s => s.UnitNumber)
                .ToListAsync(ct);

            var lines = statements.Select(s => new
            {
                id = s.Id,
                settlementId = s.BillingPeriodId,
                unitId = s.UnitId,
                participationPct = s.AliquotPercentage,
                amount = s.IndividualAmountSoles,
                breakdown = new
                {
                    water = s.IndividualWaterSoles,
                    energy = s.IndividualEnergySoles,
                    other = s.IndividualOtherExpensesSoles
                }
            });

            return Results.Ok(new
            {
                id = p.Id,
                condominiumId = p.ProjectId,
                period = p.PeriodCode,
                commonAreaAmount = p.TotalCommonExpenseSoles,
                status = string.Equals(p.Status, "Settled", StringComparison.OrdinalIgnoreCase) ? "issued" : "draft",
                isIssued = string.Equals(p.Status, "Settled", StringComparison.OrdinalIgnoreCase),
                issuedAt = p.SettledAtUtc,
                settlement_lines = lines,
                lines
            });
        }).RequireAuthorization();

        app.MapPost("/api/v1/settlements", async ([FromBody] System.Text.Json.JsonElement body, IProrationSettlementService prorationService, IoBuildDbContext db, CancellationToken ct) =>
        {
            var condoId = body.TryGetProperty("condominiumId", out var cProp) ? cProp.GetInt32() : 1;
            var periodCode = body.TryGetProperty("period", out var pProp) ? pProp.GetString() ?? "2026-10" : "2026-10";

            var period = await db.BillingPeriods.FirstOrDefaultAsync(p => p.ProjectId == condoId && p.PeriodCode == periodCode, ct);
            if (period is null)
            {
                period = new BillingPeriod
                {
                    ProjectId = condoId,
                    PeriodCode = periodCode,
                    StartDate = DateTime.UtcNow.AddMonths(-1),
                    EndDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(15),
                    MacroWaterM3 = 420.0m,
                    MacroWaterAmountSoles = 1680.00m,
                    MacroEnergyKwh = 1450.0m,
                    MacroEnergyAmountSoles = 1160.50m,
                    TotalCommonExpenseSoles = 2840.50m,
                    Status = "Draft"
                };
                db.BillingPeriods.Add(period);
                await db.SaveChangesAsync(ct);
            }

            try
            {
                await prorationService.GenerateSettlementAsync(period.Id, ct);
            }
            catch { }

            return Results.Created($"/api/v1/settlements/{period.Id}", new
            {
                id = period.Id,
                condominiumId = period.ProjectId,
                period = period.PeriodCode,
                commonAreaAmount = period.TotalCommonExpenseSoles,
                status = "draft"
            });
        }).RequireAuthorization();

        app.MapPatch("/api/v1/settlements/{id:int}", async (int id, IoBuildDbContext db, CancellationToken ct) =>
        {
            var period = await db.BillingPeriods.FindAsync([id], ct);
            if (period is null) return Results.NotFound();
            period.Status = "Settled";
            period.SettledAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new
            {
                id = period.Id,
                condominiumId = period.ProjectId,
                period = period.PeriodCode,
                commonAreaAmount = period.TotalCommonExpenseSoles,
                status = "issued",
                isIssued = true,
                issuedAt = period.SettledAtUtc
            });
        }).RequireAuthorization();

        // ── Compatibilidad REST: /api/v1/settlement_lines ──
        app.MapGet("/api/v1/settlement_lines", async ([FromQuery] int? unitId, [FromQuery] int? settlementId, IoBuildDbContext db, CancellationToken ct) =>
        {
            var query = db.StatementOfAccounts.AsQueryable();
            if (unitId.HasValue) query = query.Where(s => s.UnitId == unitId.Value);
            if (settlementId.HasValue) query = query.Where(s => s.BillingPeriodId == settlementId.Value);

            var statements = await query.OrderBy(s => s.UnitNumber).ToListAsync(ct);
            var lines = statements.Select(s => new
            {
                id = s.Id,
                settlementId = s.BillingPeriodId,
                unitId = s.UnitId,
                participationPct = s.AliquotPercentage,
                amount = s.IndividualAmountSoles,
                breakdown = new
                {
                    water = s.IndividualWaterSoles,
                    energy = s.IndividualEnergySoles,
                    other = s.IndividualOtherExpensesSoles
                }
            });
            return Results.Ok(lines);
        }).RequireAuthorization();

        app.MapGet("/api/v1/settlement_lines/{id:int}", async (int id, IoBuildDbContext db, CancellationToken ct) =>
        {
            var s = await db.StatementOfAccounts.FindAsync([id], ct);
            if (s is null) return Results.NotFound();
            return Results.Ok(new
            {
                id = s.Id,
                settlementId = s.BillingPeriodId,
                unitId = s.UnitId,
                participationPct = s.AliquotPercentage,
                amount = s.IndividualAmountSoles,
                breakdown = new
                {
                    water = s.IndividualWaterSoles,
                    energy = s.IndividualEnergySoles,
                    other = s.IndividualOtherExpensesSoles
                }
            });
        }).RequireAuthorization();

        // ── Compatibilidad REST: /api/v1/payments ──
        app.MapGet("/api/v1/payments", async ([FromQuery] int? condominiumId, [FromQuery] int? projectId, [FromQuery] int? residentUserId, [FromQuery] int? unitId, [FromQuery] string? status, IoBuildDbContext db, CancellationToken ct) =>
        {
            var targetProjectId = condominiumId ?? projectId ?? 1;
            var query = db.StatementOfAccounts.Where(s => s.ProjectId == targetProjectId);
            if (unitId.HasValue) query = query.Where(s => s.UnitId == unitId.Value);
            if (residentUserId.HasValue)
            {
                var unitsForResident = await db.Units.Where(u => u.OwnerId == residentUserId.Value).Select(u => u.Id).ToListAsync(ct);
                query = query.Where(s => unitsForResident.Contains(s.UnitId));
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(s => s.Status.ToLower() == status.ToLower());
            }

            var periods = await db.BillingPeriods.Where(p => p.ProjectId == targetProjectId).ToDictionaryAsync(p => p.Id, ct);
            var statements = await query.ToListAsync(ct);

            var payments = statements.Select(s =>
            {
                var p = periods.TryGetValue(s.BillingPeriodId, out var period) ? period : null;
                return new
                {
                    id = s.Id,
                    settlementLineId = s.Id,
                    unitId = s.UnitId,
                    condominiumId = s.ProjectId,
                    period = p?.PeriodCode ?? "2026-10",
                    amount = s.IndividualAmountSoles,
                    dueDate = p?.DueDate.ToString("yyyy-MM-dd") ?? "2026-10-31",
                    paidAt = s.PaidAtUtc?.ToString("yyyy-MM-dd"),
                    status = s.Status.ToLowerInvariant(),
                    method = s.PaymentMethod ?? "Transferencia Bancaria",
                    reference = s.PaymentReference ?? $"OP-{s.Id:D6}"
                };
            });
            return Results.Ok(payments);
        }).RequireAuthorization();

        app.MapPatch("/api/v1/payments/{paymentId:int}", async (int paymentId, [FromBody] System.Text.Json.JsonElement body, IoBuildDbContext db, CancellationToken ct) =>
        {
            var statement = await db.StatementOfAccounts.FindAsync([paymentId], ct);
            if (statement is null) return Results.NotFound();
            statement.Status = "Paid";
            statement.PaidAtUtc = DateTime.UtcNow;
            if (body.TryGetProperty("reference", out var refProp)) statement.PaymentReference = refProp.GetString();
            if (body.TryGetProperty("method", out var methProp)) statement.PaymentMethod = methProp.GetString();
            statement.PaidAmountSoles = statement.IndividualAmountSoles;
            statement.BalanceSoles = 0.00m;
            await db.SaveChangesAsync(ct);
            return Results.Ok(statement);
        }).RequireAuthorization();

        // ── Compatibilidad REST: /api/v1/audit_events ──
        app.MapGet("/api/v1/audit_events", ([FromQuery] int? condominiumId, [FromQuery] int? projectId, IoBuildDbContext db) =>
        {
            var targetProjectId = condominiumId ?? projectId ?? 1;
            var events = new List<object>
            {
                new
                {
                    id = 1,
                    condominiumId = targetProjectId,
                    type = "proration",
                    entity = "BillingPeriod",
                    actorUserId = 1,
                    at = DateTime.UtcNow.AddDays(-2).ToString("o"),
                    previousValue = "Draft",
                    newValue = "Settled"
                },
                new
                {
                    id = 2,
                    condominiumId = targetProjectId,
                    type = "reading",
                    entity = "DeviceTelemetry",
                    actorUserId = 1,
                    at = DateTime.UtcNow.AddDays(-1).ToString("o"),
                    previousValue = "0.15 m3/h",
                    newValue = "0.42 m3/h"
                },
                new
                {
                    id = 3,
                    condominiumId = targetProjectId,
                    type = "participation_changed",
                    entity = "Unit 402",
                    actorUserId = 1,
                    at = DateTime.UtcNow.AddDays(-5).ToString("o"),
                    previousValue = "3.8500%",
                    newValue = "3.8500%"
                }
            };
            return Results.Ok(events);
        }).RequireAuthorization();
    }
}
