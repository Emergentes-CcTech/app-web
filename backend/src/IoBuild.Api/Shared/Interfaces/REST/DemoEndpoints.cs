using System.Globalization;
using System.Security.Claims;
using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Analytics.Domain.Services;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Shared.Interfaces.REST;

/// <summary>
/// Herramientas de demostración (solo con Demo:Enabled): generar alertas con las mismas reglas del motor de anomalías
/// y adelantar el tiempo de la facturación de un condominio para mostrar la morosidad sin esperar un mes.
/// </summary>
public static class DemoEndpoints
{
    public sealed record DemoAlertRequest(string? Type);

    private static int SelfId(ClaimsPrincipal user) =>
        int.TryParse(user.FindFirst(ClaimTypes.Sid)?.Value ?? user.FindFirst("sid")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value, out var id) ? id : 0;

    private static bool Enabled(IConfiguration configuration) => configuration.GetValue("Demo:Enabled", false);

    private static Task<bool> OwnsAsync(ClaimsPrincipal user, IoBuildDbContext db, int projectId, CancellationToken ct)
    {
        var self = SelfId(user);
        return db.Projects.AnyAsync(p => p.Id == projectId && p.BuilderId == self, ct);
    }

    public static void MapDemoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/demo").WithTags("Demo").RequireAuthorization();

        // La interfaz muestra el panel de demostración solo si el servidor lo tiene activo
        group.MapGet("/status", (IConfiguration configuration) => Results.Ok(new { enabled = Enabled(configuration) }));

        // Estado del condominio para el panel: alertas abiertas y cuotas por periodo
        group.MapGet("/condominiums/{id:int}", async (int id, ClaimsPrincipal user, IConfiguration configuration, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!Enabled(configuration)) return Results.NotFound();
            if (!await OwnsAsync(user, db, id, ct)) return Results.NotFound();
            return Results.Ok(await SnapshotAsync(db, id, ct));
        });

        // Genera una alerta con la regla real del motor, como si el medidor la hubiera reportado
        group.MapPost("/condominiums/{id:int}/alerts", async (int id, DemoAlertRequest body, ClaimsPrincipal user, IConfiguration configuration,
            IoBuildDbContext db, IAnomalyDetectionEngine engine, CancellationToken ct) =>
        {
            if (!Enabled(configuration)) return Results.NotFound();
            if (!await OwnsAsync(user, db, id, ct)) return Results.NotFound();
            var devices = await db.Devices.Where(d => d.ProjectId == id && d.UnitId == null && d.Status != "Unlinked").ToListAsync(ct);
            var now = DateTimeOffset.UtcNow;

            AnomalyAlert? alert;
            switch ((body.Type ?? "").Trim().ToLowerInvariant())
            {
                case "leak":
                {
                    // La regla solo cuenta caudal entre 01:00 y 04:00 (hora de Lima): se registra en la última madrugada
                    var meter = devices.Where(d => d.Type == "WaterFlowSensor").OrderBy(d => d.Subsystem == "MainCistern" ? 0 : 1).ThenBy(d => d.Id).FirstOrDefault();
                    if (meter is null) return Results.BadRequest(new { error = "El condominio no tiene un punto de agua." });
                    alert = await engine.EvaluateWaterFlowAsync(meter, (double)meter.NightFlowThresholdM3H + 1.05, LastNightInLima(now), ct);
                    break;
                }
                case "overload":
                {
                    var panel = devices.Where(d => d.Type == "CurrentTransformerCT").OrderBy(d => d.Subsystem == "PressurePumpsRoom" ? 0 : 1).ThenBy(d => d.Id).FirstOrDefault();
                    if (panel is null) return Results.BadRequest(new { error = "El condominio no tiene un tablero de energía." });
                    alert = await engine.EvaluatePumpElectricalAsync(panel, 26.6, 18.2, now, ct);
                    break;
                }
                case "gateway":
                {
                    var gateway = devices.FirstOrDefault(d => d.Type == "GatewayHub");
                    if (gateway is null) return Results.BadRequest(new { error = "El condominio no tiene gateway." });
                    // Misma alerta que emite el motor cuando el concentrador deja de latir
                    alert = new AnomalyAlert
                    {
                        ProjectId = id,
                        DeviceId = gateway.Id,
                        Subsystem = gateway.Subsystem,
                        AlertType = "GatewayOfflineInBasement",
                        Severity = "Warning",
                        Title = $"Gateway Hub fuera de línea en sótano ({gateway.Name})",
                        Description = "El concentrador en sótano no ha emitido señal de latido durante 15 minutos.",
                        EstimatedOvercostSoles = 0m,
                        DetectedAtUtc = now.UtcDateTime,
                        Status = "Active"
                    };
                    db.AnomalyAlerts.Add(alert);
                    await db.SaveChangesAsync(ct);
                    break;
                }
                default:
                    return Results.BadRequest(new { error = "Tipo de alerta no válido: usa leak, overload o gateway." });
            }

            if (alert is null) return Results.Conflict(new { error = "La regla no generó la alerta con esos valores." });
            return Results.Ok(new
            {
                alert.Id,
                alert.AlertType,
                alert.Severity,
                alert.Title,
                alert.Description,
                alert.EstimatedOvercostSoles,
                alert.DetectedAtUtc
            });
        });

        // Resuelve todas las alertas abiertas del condominio (deja la demostración limpia)
        group.MapPost("/condominiums/{id:int}/alerts/resolve-all", async (int id, ClaimsPrincipal user, IConfiguration configuration, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!Enabled(configuration)) return Results.NotFound();
            if (!await OwnsAsync(user, db, id, ct)) return Results.NotFound();
            var open = await db.AnomalyAlerts.Where(a => a.ProjectId == id && a.Status == "Active").ToListAsync(ct);
            foreach (var alert in open) alert.Resolve();
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { resolved = open.Count });
        });

        // Adelanta el tiempo un mes para este condominio: sus periodos, cuotas, pagos y alertas pasan al mes anterior.
        // La cuota que vencía este mes queda vencida (morosidad) y el mes actual queda libre para un nuevo prorrateo
        group.MapPost("/condominiums/{id:int}/advance-month", async (int id, ClaimsPrincipal user, IConfiguration configuration, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!Enabled(configuration)) return Results.NotFound();
            if (!await OwnsAsync(user, db, id, ct)) return Results.NotFound();

            if (!await db.BillingPeriods.AnyAsync(p => p.ProjectId == id, ct))
                return Results.BadRequest(new { error = "El condominio aún no tiene periodos de facturación: registra los recibos y calcula un prorrateo primero." });

            // MySQL usa una estrategia de reintentos: la transacción completa va dentro de ella y cada intento recarga los datos
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
                var periods = await db.BillingPeriods.Where(p => p.ProjectId == id).ToListAsync(ct);

                // Dos pasos: el código de periodo es único por condominio y 2026-11 → 2026-10 chocaría con el actual 2026-10
                var finalCodes = periods.ToDictionary(p => p.Id, p => ShiftCode(p.PeriodCode));
                foreach (var p in periods) p.PeriodCode = $"tmp-{p.Id}";
                await db.SaveChangesAsync(ct);
                foreach (var p in periods)
                {
                    p.PeriodCode = finalCodes[p.Id];
                    p.StartDate = p.StartDate.AddMonths(-1);
                    p.EndDate = p.EndDate.AddMonths(-1);
                    p.DueDate = p.DueDate.AddMonths(-1);
                    if (p.ReconciledAtUtc.HasValue) p.ReconciledAtUtc = p.ReconciledAtUtc.Value.AddMonths(-1);
                    if (p.SettledAtUtc.HasValue) p.SettledAtUtc = p.SettledAtUtc.Value.AddMonths(-1);
                }

                var statements = await db.StatementOfAccounts.Where(s => s.ProjectId == id && s.PaidAtUtc != null).ToListAsync(ct);
                foreach (var s in statements) s.PaidAtUtc = s.PaidAtUtc!.Value.AddMonths(-1);

                // Las alertas siguen ligadas a la cuota de su mes
                var alerts = await db.AnomalyAlerts.Where(a => a.ProjectId == id).ToListAsync(ct);
                foreach (var a in alerts)
                {
                    a.DetectedAtUtc = a.DetectedAtUtc.AddMonths(-1);
                    if (a.ResolvedAtUtc.HasValue) a.ResolvedAtUtc = a.ResolvedAtUtc.Value.AddMonths(-1);
                }

                await db.SaveChangesAsync(ct);
                if (transaction is not null) await transaction.CommitAsync(ct);
            });

            return Results.Ok(await SnapshotAsync(db, id, ct));
        });
    }

    // Periodos con sus cuotas: cuántas están pagadas, pendientes o vencidas a la fecha
    private static async Task<object> SnapshotAsync(IoBuildDbContext db, int id, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var periods = await db.BillingPeriods.Where(p => p.ProjectId == id).OrderByDescending(p => p.PeriodCode).ToListAsync(ct);
        var periodIds = periods.Select(p => p.Id).ToList();
        var statements = await db.StatementOfAccounts.Where(s => periodIds.Contains(s.BillingPeriodId)).ToListAsync(ct);
        var openAlerts = await db.AnomalyAlerts.CountAsync(a => a.ProjectId == id && a.Status == "Active", ct);

        return new
        {
            openAlerts,
            periods = periods.Select(p =>
            {
                var lines = statements.Where(s => s.BillingPeriodId == p.Id).ToList();
                var paid = lines.Count(s => string.Equals(s.Status, "Paid", StringComparison.OrdinalIgnoreCase));
                var overdue = p.DueDate.Date < today ? lines.Count - paid : 0;
                return new
                {
                    period = p.PeriodCode,
                    dueDate = p.DueDate.ToString("yyyy-MM-dd"),
                    statements = lines.Count,
                    paid,
                    overdue,
                    pending = lines.Count - paid - overdue
                };
            })
        };
    }

    private static string ShiftCode(string code) =>
        DateTime.TryParseExact(code, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month)
            ? month.AddMonths(-1).ToString("yyyy-MM", CultureInfo.InvariantCulture)
            : code;

    // 02:30 de la última madrugada en Lima (UTC-5), dentro de la ventana nocturna de la regla de fugas
    private static DateTimeOffset LastNightInLima(DateTimeOffset now)
    {
        var lima = now.ToOffset(TimeSpan.FromHours(-5));
        var day = lima.Hour >= 4 ? lima.Date : lima.Date.AddDays(-1);
        return new DateTimeOffset(day.Year, day.Month, day.Day, 2, 30, 0, TimeSpan.FromHours(-5));
    }
}
