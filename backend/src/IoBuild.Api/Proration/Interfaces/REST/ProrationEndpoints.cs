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
        int.TryParse(user.FindFirst(ClaimTypes.Sid)?.Value ?? user.FindFirst("sid")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value, out var id) ? id : 0;

    // Valida si el usuario autenticado posee roles de administración
    private static bool IsAdministrator(ClaimsPrincipal user)
    {
        var roles = user.FindAll(ClaimTypes.Role)
            .Concat(user.FindAll("role"))
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return roles.Contains("Builder") || roles.Contains("PropertyManager") || roles.Contains("Admin");
    }

    // Obtiene las unidades residenciales pertenecientes o asociadas al usuario autenticado
    private static async Task<List<int>> GetResidentUnitIdsAsync(ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct)
    {
        var self = SelfId(user);
        var email = user.FindFirst(ClaimTypes.Email)?.Value?.ToLowerInvariant()
            ?? user.FindFirst("email")?.Value?.ToLowerInvariant();

        var unitIds = await db.Units
            .Where(u => u.OwnerId == self || (email != null && u.OwnerEmail != null && u.OwnerEmail.ToLower() == email))
            .Select(u => u.Id)
            .ToListAsync(ct);

        var projectedIds = await db.UnitOwnerProjections
            .Where(p => p.OwnerUserId == self)
            .Select(p => p.UnitId)
            .ToListAsync(ct);

        return unitIds.Union(projectedIds).Distinct().ToList();
    }

    // Resuelve el modo de pago y la clave de Stripe desde appsettings, .env o variables de entorno
    private static (bool IsSimulated, string? ApiKey) ResolveStripeSettings(IConfiguration configuration)
    {
        // appsettings trae las claves como "" de marcador: se toma el primer valor no vacío para que el .env prevalezca
        static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        var envSimulated = FirstNonEmpty(
            configuration["STRIPE_USE_SIMULATED_PAYMENTS"],
            Environment.GetEnvironmentVariable("STRIPE_USE_SIMULATED_PAYMENTS"),
            Environment.GetEnvironmentVariable("Stripe__UseSimulatedPayments"));
        var rawSimulated = envSimulated ?? configuration["Stripe:UseSimulatedPayments"];

        var isSimulated = bool.TryParse(rawSimulated, out var simParsed) && simParsed;

        var restrictedKey = FirstNonEmpty(
            configuration["Stripe:RestrictedApiKey"],
            configuration["STRIPE_RESTRICTED_API_KEY"],
            Environment.GetEnvironmentVariable("STRIPE_RESTRICTED_API_KEY"));

        var secretKey = FirstNonEmpty(
            configuration["Stripe:SecretKey"],
            configuration["STRIPE_SECRET_KEY"],
            Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY"));

        var effectiveKey = restrictedKey ?? secretKey;

        // Sin claves de Stripe se opera en modo simulado seguro aunque no se haya pedido explícitamente
        return (isSimulated || string.IsNullOrWhiteSpace(effectiveKey), effectiveKey);
    }

    // Valida que el usuario sea el administrador del condominio
    private static async Task<bool> MayManageProjectAsync(ClaimsPrincipal user, IoBuildDbContext db, int projectId, CancellationToken ct)
    {
        var self = SelfId(user);
        if (IsAdministrator(user))
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
        var email = user.FindFirst(ClaimTypes.Email)?.Value?.ToLowerInvariant() ?? user.FindFirst("email")?.Value?.ToLowerInvariant();
        return await db.Units.AnyAsync(u => u.ProjectId == projectId && (u.OwnerId == self || (!string.IsNullOrEmpty(email) && u.OwnerEmail != null && u.OwnerEmail.ToLower() == email)), ct)
            || await db.UnitOwnerProjections.AnyAsync(p => p.OwnerUserId == self && db.Units.Any(u => u.Id == p.UnitId && u.ProjectId == projectId), ct);
    }

    // Valida que el usuario tenga permiso para abonar el estado de cuenta (Administrador o el Residente titular de la unidad)
    private static async Task<bool> MayPayStatementAsync(ClaimsPrincipal user, IoBuildDbContext db, StatementOfAccount statement, CancellationToken ct)
    {
        var self = SelfId(user);
        if (IsAdministrator(user))
        {
            return await db.Projects.AnyAsync(p => p.Id == statement.ProjectId && p.BuilderId == self, ct);
        }
        var email = user.FindFirst(ClaimTypes.Email)?.Value?.ToLowerInvariant() ?? user.FindFirst("email")?.Value?.ToLowerInvariant();
        return await db.Units.AnyAsync(u => u.Id == statement.UnitId && (u.OwnerId == self || (!string.IsNullOrEmpty(email) && u.OwnerEmail != null && u.OwnerEmail.ToLower() == email)), ct)
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

            var query = db.StatementOfAccounts.Where(s => s.BillingPeriodId == billingPeriodId);
            if (!IsAdministrator(user))
            {
                var myUnitIds = await GetResidentUnitIdsAsync(user, db, ct);
                query = query.Where(s => myUnitIds.Contains(s.UnitId));
            }

            var statements = await query
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

            if (!IsAdministrator(user))
            {
                var myUnitIds = await GetResidentUnitIdsAsync(user, db, ct);
                if (!myUnitIds.Contains(statement.UnitId))
                {
                    return Results.Forbid();
                }
            }

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

        app.MapPost("/api/v1/utility_bills", async ([FromBody] System.Text.Json.JsonElement body, ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var condoId = body.TryGetProperty("condominiumId", out var cProp) ? cProp.GetInt32() : 1;
            // Solo la administradora del condominio registra recibos matrices
            if (!await MayManageProjectAsync(user, db, condoId, ct)) return Results.Forbid();
            // Imagen del recibo alojada en Cloudinary (subida directa desde el navegador)
            var receiptImageUrl = body.TryGetProperty("receiptImageUrl", out var riProp) && riProp.ValueKind == System.Text.Json.JsonValueKind.String
                ? riProp.GetString()
                : null;
            // Solo se aceptan enlaces https, se muestran luego como enlace a residentes y administración
            if (!string.IsNullOrWhiteSpace(receiptImageUrl)
                && !(Uri.TryCreate(receiptImageUrl, UriKind.Absolute, out var receiptUri) && receiptUri.Scheme == Uri.UriSchemeHttps))
            {
                return Results.BadRequest(new { message = "La imagen del recibo debe ser un enlace https." });
            }
            if (string.IsNullOrWhiteSpace(receiptImageUrl)) receiptImageUrl = null;
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
                if (receiptImageUrl is not null) period.WaterReceiptImageUrl = receiptImageUrl;
            }
            else
            {
                period.MacroEnergyKwh = consumption;
                period.MacroEnergyAmountSoles = amount;
                if (receiptImageUrl is not null) period.EnergyReceiptImageUrl = receiptImageUrl;
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
                dueDate = period.DueDate.ToString("yyyy-MM-dd"),
                receiptImageUrl = service == "water" ? period.WaterReceiptImageUrl : period.EnergyReceiptImageUrl
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

        app.MapGet("/api/v1/settlements/{id:int}", async (int id, ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var p = await db.BillingPeriods.FindAsync([id], ct);
            if (p is null) return Results.NotFound();

            var query = db.StatementOfAccounts.Where(s => s.BillingPeriodId == id);
            if (!IsAdministrator(user))
            {
                var myUnitIds = await GetResidentUnitIdsAsync(user, db, ct);
                query = query.Where(s => myUnitIds.Contains(s.UnitId));
            }

            var statements = await query
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
        app.MapGet("/api/v1/settlement_lines", async (
            [FromQuery] int? unitId,
            [FromQuery] int? settlementId,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IoBuildDbContext db,
            CancellationToken ct) =>
        {
            var query = db.StatementOfAccounts.AsQueryable();

            int? effectiveUnitId = unitId;
            if (!effectiveUnitId.HasValue && httpContext.Request.Query.TryGetValue("unitId[]", out var qVal) && int.TryParse(qVal.FirstOrDefault(), out var parsedId))
            {
                effectiveUnitId = parsedId;
            }

            if (!IsAdministrator(user))
            {
                var myUnitIds = await GetResidentUnitIdsAsync(user, db, ct);
                if (effectiveUnitId.HasValue && !myUnitIds.Contains(effectiveUnitId.Value))
                {
                    return Results.Forbid();
                }
                if (effectiveUnitId.HasValue)
                {
                    query = query.Where(s => s.UnitId == effectiveUnitId.Value);
                }
                else
                {
                    query = query.Where(s => myUnitIds.Contains(s.UnitId));
                }
            }
            else
            {
                if (effectiveUnitId.HasValue) query = query.Where(s => s.UnitId == effectiveUnitId.Value);
            }

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

        app.MapGet("/api/v1/settlement_lines/{id:int}", async (int id, ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var s = await db.StatementOfAccounts.FindAsync([id], ct);
            if (s is null) return Results.NotFound();

            if (!IsAdministrator(user))
            {
                var myUnitIds = await GetResidentUnitIdsAsync(user, db, ct);
                if (!myUnitIds.Contains(s.UnitId))
                {
                    return Results.Forbid();
                }
            }

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
        app.MapGet("/api/v1/payments", async (
            [FromQuery] int? condominiumId,
            [FromQuery] int? projectId,
            [FromQuery] int? residentUserId,
            [FromQuery] int? unitId,
            [FromQuery] string? status,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IoBuildDbContext db,
            CancellationToken ct) =>
        {
            var targetProjectId = condominiumId ?? projectId ?? 1;
            var query = db.StatementOfAccounts.Where(s => s.ProjectId == targetProjectId);

            int? effectiveUnitId = unitId;
            if (!effectiveUnitId.HasValue && httpContext.Request.Query.TryGetValue("unitId[]", out var qVal) && int.TryParse(qVal.FirstOrDefault(), out var parsedId))
            {
                effectiveUnitId = parsedId;
            }

            if (!IsAdministrator(user))
            {
                var myUnitIds = await GetResidentUnitIdsAsync(user, db, ct);
                if (effectiveUnitId.HasValue && !myUnitIds.Contains(effectiveUnitId.Value))
                {
                    return Results.Forbid();
                }
                if (residentUserId.HasValue && residentUserId.Value != SelfId(user))
                {
                    return Results.Forbid();
                }
                if (effectiveUnitId.HasValue)
                {
                    query = query.Where(s => s.UnitId == effectiveUnitId.Value);
                }
                else
                {
                    query = query.Where(s => myUnitIds.Contains(s.UnitId));
                }
            }
            else
            {
                if (effectiveUnitId.HasValue) query = query.Where(s => s.UnitId == effectiveUnitId.Value);
                if (residentUserId.HasValue)
                {
                    var unitsForResident = await db.Units.Where(u => u.OwnerId == residentUserId.Value).Select(u => u.Id).ToListAsync(ct);
                    query = query.Where(s => unitsForResident.Contains(s.UnitId));
                }
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

        // ── Pasarela de pagos Stripe para cuotas de mantenimiento ──
        app.MapPost("/api/v1/payments/{paymentId:int}/checkout-session", async (
            int paymentId,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IConfiguration configuration,
            IoBuildDbContext db,
            CancellationToken ct) =>
        {
            var statement = await db.StatementOfAccounts.FindAsync([paymentId], ct);
            if (statement is null) return Results.NotFound(new { message = $"Estado de cuenta #{paymentId} no encontrado." });

            // Validación estricta de autorización: propietario de la unidad o administrador del condominio
            if (!await MayPayStatementAsync(user, db, statement, ct))
            {
                return Results.Forbid();
            }

            // Validación de máquina de estados: evitar doble pago o sobreescritura de comprobante previo
            if (string.Equals(statement.Status, "Paid", StringComparison.OrdinalIgnoreCase) || statement.BalanceSoles <= 0)
            {
                return Results.BadRequest(new { message = $"El estado de cuenta #{paymentId} ya se encuentra pagado." });
            }

            var origin = "";
            if (httpContext.Request.Headers.TryGetValue("Origin", out var originVal) && !string.IsNullOrWhiteSpace(originVal))
            {
                origin = originVal.ToString().TrimEnd('/');
            }
            else if (httpContext.Request.Headers.TryGetValue("Referer", out var refVal) && !string.IsNullOrWhiteSpace(refVal))
            {
                if (Uri.TryCreate(refVal.ToString(), UriKind.Absolute, out var uri))
                {
                    origin = $"{uri.Scheme}://{uri.Authority}".TrimEnd('/');
                }
            }

            if (string.IsNullOrWhiteSpace(origin))
            {
                origin = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}".TrimEnd('/');
            }
            if (string.IsNullOrWhiteSpace(origin) || origin.StartsWith("://"))
            {
                origin = "http://localhost";
            }

            var (isSimulated, effectiveKey) = ResolveStripeSettings(configuration);

            if (isSimulated)
            {
                // Flujo simulado verificado: solo muta el estado tras comprobar autenticación y pertenencia
                var sessionId = $"cs_sim_{paymentId}";
                statement.Status = "Paid";
                statement.PaidAtUtc = DateTime.UtcNow;
                statement.PaymentMethod = "Stripe";
                statement.PaymentReference = sessionId;
                statement.PaidAmountSoles = statement.IndividualAmountSoles;
                statement.BalanceSoles = 0.00m;
                await db.SaveChangesAsync(ct);

                var sessionUrl = $"{origin}/payments/account-statement?paid=1&payment_id={statement.Id}&session_id={sessionId}";
                return Results.Ok(new
                {
                    url = sessionUrl,
                    sessionId = sessionId,
                    paymentId = statement.Id,
                    amount = statement.IndividualAmountSoles,
                    status = statement.Status
                });
            }
            else
            {
                // Flujo en vivo con Stripe Checkout REST API
                try
                {
                    using var httpClient = new HttpClient();
                    var successUrl = $"{origin}/payments/account-statement?paid=1&payment_id={statement.Id}&session_id={{CHECKOUT_SESSION_ID}}";
                    var cancelUrl = $"{origin}/payments/account-statement?canceled=1";
                    var amountInCents = (long)Math.Round(statement.IndividualAmountSoles * 100m, MidpointRounding.AwayFromZero);

                    var formFields = new Dictionary<string, string>
                    {
                        ["mode"] = "payment",
                        ["success_url"] = successUrl,
                        ["cancel_url"] = cancelUrl,
                        ["line_items[0][quantity]"] = "1",
                        // Las cuotas de mantenimiento se liquidan en soles; Stripe:Currency corresponde a las suscripciones HaaS
                        ["line_items[0][price_data][currency]"] = "pen",
                        ["line_items[0][price_data][unit_amount]"] = amountInCents.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["line_items[0][price_data][product_data][name]"] = $"Cuota de mantenimiento - Dpto. {statement.UnitNumber}",
                        ["line_items[0][price_data][product_data][description]"] = $"Mantenimiento condominio #{statement.ProjectId}",
                        ["metadata[payment_id]"] = statement.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["metadata[unit_id]"] = statement.UnitId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["client_reference_id"] = statement.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    };

                    using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/checkout/sessions");
                    req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", effectiveKey);
                    req.Content = new FormUrlEncodedContent(formFields);

                    var resp = await httpClient.SendAsync(req, ct);
                    if (!resp.IsSuccessStatusCode)
                    {
                        var err = await resp.Content.ReadAsStringAsync(ct);
                        return Results.Problem(detail: $"Stripe gateway error: {err}", statusCode: (int)resp.StatusCode);
                    }

                    using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                    var realSessionId = doc.RootElement.GetProperty("id").GetString()!;
                    var realUrl = doc.RootElement.GetProperty("url").GetString()!;

                    statement.PaymentReference = realSessionId;
                    statement.PaymentMethod = "Stripe";
                    await db.SaveChangesAsync(ct);

                    return Results.Ok(new
                    {
                        url = realUrl,
                        sessionId = realSessionId,
                        paymentId = statement.Id,
                        amount = statement.IndividualAmountSoles,
                        status = statement.Status
                    });
                }
                catch (Exception ex)
                {
                    return Results.Problem(detail: $"Failed to connect to Stripe: {ex.Message}", statusCode: 503);
                }
            }
        }).RequireAuthorization();

        // Confirma un Checkout real consultando la sesión en Stripe: el estado solo pasa a pagado si Stripe lo reporta cobrado
        app.MapPost("/api/v1/payments/{paymentId:int}/checkout-session/confirm", async (
            int paymentId,
            [FromBody] System.Text.Json.JsonElement body,
            ClaimsPrincipal user,
            IConfiguration configuration,
            IoBuildDbContext db,
            CancellationToken ct) =>
        {
            var statement = await db.StatementOfAccounts.FindAsync([paymentId], ct);
            if (statement is null) return Results.NotFound(new { message = $"Estado de cuenta #{paymentId} no encontrado." });
            if (!await MayPayStatementAsync(user, db, statement, ct)) return Results.Forbid();

            var sessionId = body.TryGetProperty("sessionId", out var sidProp) ? sidProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(sessionId)) return Results.BadRequest(new { message = "Se requiere el identificador de la sesión de Stripe." });

            // Reintento idempotente: la misma sesión ya conciliada devuelve el estado actual sin volver a escribir
            if (string.Equals(statement.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                return statement.PaymentReference == sessionId
                    ? Results.Ok(new { paymentId = statement.Id, status = statement.Status })
                    : Results.BadRequest(new { message = $"El estado de cuenta #{paymentId} ya se encuentra pagado." });
            }

            var (isSimulated, effectiveKey) = ResolveStripeSettings(configuration);

            // En modo simulado el pago se concilia al crear la sesión; aquí no hay nada que consultar
            if (isSimulated) return Results.Ok(new { paymentId = statement.Id, status = statement.Status });

            // La pertenencia se valida con el client_reference_id que reporta Stripe, no con la última sesión guardada:
            // si el residente abrió Checkout dos veces y pagó en la primera, el cobro igual debe conciliarse
            try
            {
                using var httpClient = new HttpClient();
                using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.stripe.com/v1/checkout/sessions/{Uri.EscapeDataString(sessionId)}");
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", effectiveKey);

                var resp = await httpClient.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync(ct);
                    return Results.Problem(detail: $"Stripe gateway error: {err}", statusCode: (int)resp.StatusCode);
                }

                using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                var root = doc.RootElement;
                var paymentStatus = root.TryGetProperty("payment_status", out var psProp) ? psProp.GetString() : null;
                var clientReference = root.TryGetProperty("client_reference_id", out var crProp) ? crProp.GetString() : null;

                if (clientReference != statement.Id.ToString(System.Globalization.CultureInfo.InvariantCulture))
                {
                    return Results.BadRequest(new { message = "La sesión de Stripe no corresponde a esta cuota." });
                }
                if (!string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Ok(new { paymentId = statement.Id, status = statement.Status, stripePaymentStatus = paymentStatus });
                }

                // Stripe reporta en céntimos el monto realmente cobrado
                var amountTotal = root.TryGetProperty("amount_total", out var atProp) && atProp.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? atProp.GetInt64() / 100m
                    : statement.IndividualAmountSoles;

                statement.Status = "Paid";
                statement.PaidAtUtc = DateTime.UtcNow;
                statement.PaymentMethod = "Stripe";
                statement.PaymentReference = sessionId;
                statement.PaidAmountSoles = amountTotal;
                statement.BalanceSoles = Math.Max(0m, statement.IndividualAmountSoles - amountTotal);
                await db.SaveChangesAsync(ct);

                return Results.Ok(new { paymentId = statement.Id, status = statement.Status });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: $"Failed to connect to Stripe: {ex.Message}", statusCode: 503);
            }
        }).RequireAuthorization();

        // Registro manual de abonos (RF-016): reservado a la administración, el residente paga solo vía Checkout
        app.MapPatch("/api/v1/payments/{paymentId:int}", async (int paymentId, [FromBody] System.Text.Json.JsonElement body, ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var statement = await db.StatementOfAccounts.FindAsync([paymentId], ct);
            if (statement is null) return Results.NotFound();

            if (!IsAdministrator(user) || !await MayPayStatementAsync(user, db, statement, ct)) return Results.Forbid();

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
