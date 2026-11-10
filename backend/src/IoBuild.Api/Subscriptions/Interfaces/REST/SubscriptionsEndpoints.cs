using IoBuild.Api.CoreBusiness;
using IoBuild.Api.Persistence;
using IoBuild.Api.Subscriptions.Application.Internal.CommandServices;
using IoBuild.Api.Subscriptions.Domain.Model.Aggregates;
using IoBuild.Api.Subscriptions.Domain.Services;
using IoBuild.Api.Subscriptions.Domain.Services.Commands;
using IoBuild.Api.Subscriptions.Domain.Services.Queries;
using IoBuild.Api.Subscriptions.Infrastructure.Stripe;
using IoBuild.Api.Subscriptions.Interfaces.REST.Resources;
using IoBuild.Api.Subscriptions.Interfaces.REST.Transform;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Subscriptions.Interfaces.REST;

/// <summary>
/// Mapeo de rutas REST del contexto Subscriptions & Billing.
/// Permite listar, consultar, registrar, escalar y desvincular planes y suscripciones HaaS.
/// </summary>
public static class SubscriptionsEndpoints
{
    // Valida si el usuario autenticado tiene permisos sobre el identificador de condominio/administrador provisto
    private static bool OwnsBuilder(System.Security.Claims.ClaimsPrincipal user, int builderId) =>
        int.TryParse(user.FindFirst(System.Security.Claims.ClaimTypes.Sid)?.Value, out var id) && (id == builderId || builderId == 0);

    // El solicitante puede operar el condominio si es su id directo (compatibilidad) o si él lo registró
    private static Task<int> CommunalDevicesAsync(IoBuildDbContext db, int projectId, CancellationToken ct) =>
        db.Devices.CountAsync(d => d.ProjectId == projectId && d.UnitId == null, ct);

    private static async Task<bool> OwnsProjectAsync(System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, int projectId, CancellationToken ct)
    {
        if (OwnsBuilder(user, projectId)) return true;
        var self = SelfId(user);
        return self != 0 && await db.Projects.AnyAsync(p => p.Id == projectId && p.BuilderId == self, ct);
    }

    // Obtiene el identificador del usuario autenticado a partir de las credenciales del token
    private static int SelfId(System.Security.Claims.ClaimsPrincipal user) =>
        int.TryParse(user.FindFirst(System.Security.Claims.ClaimTypes.Sid)?.Value, out var id) ? id : 0;

    // Deserializa de forma segura la carga útil recibida desde el webhook de la pasarela
    private static System.Text.Json.JsonDocument? ParseWebhookPayload(string payload)
    {
        try
        {
            return System.Text.Json.JsonDocument.Parse(payload);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public static void MapSubscriptionsEndpoints(this WebApplication app)
    {
        // ── Rutas para consultar y administrar el catálogo de Planes HaaS ──
        var plans = app.MapGroup("/api/v1/plans").WithTags("Plans");

        // Listar el catálogo completo de planes HaaS vigentes
        plans.MapGet("", async (IPlanQueryService queryService, CancellationToken ct) =>
        {
            var planList = await queryService.Handle(new GetAllPlansQuery(), ct);
            return Results.Ok(planList.Select(PlanResourceFromEntityAssembler.ToResourceFromEntity));
        }).AllowAnonymous();

        // Consultar el detalle de un plan específico por su identificador
        plans.MapGet("/{id:int}", async (int id, IPlanQueryService queryService, CancellationToken ct) =>
        {
            var plan = await queryService.Handle(new GetPlanByIdQuery(id), ct);
            return plan is null ? Results.NotFound() : Results.Ok(PlanResourceFromEntityAssembler.ToResourceFromEntity(plan));
        }).AllowAnonymous();

        // Registrar un nuevo plan comercial en el catálogo
        plans.MapPost("", async (CreatePlanResource resource, IPlanCommandService commandService, IPlanQueryService queryService, CancellationToken ct) =>
        {
            var command = new CreatePlanCommand(resource.Name, resource.Description, resource.Price, resource.Interval, resource.FeaturesJson);
            var planId = await commandService.Handle(command, ct);
            var created = await queryService.Handle(new GetPlanByIdQuery(planId), ct);
            return created is null ? Results.Problem(statusCode: 500) : Results.Created($"/api/v1/plans/{planId}", PlanResourceFromEntityAssembler.ToResourceFromEntity(created));
        }).RequireAuthorization();

        // Actualizar especificaciones de un plan existente
        plans.MapPut("/{id:int}", async (int id, UpdatePlanResource resource, IPlanCommandService commandService, CancellationToken ct) =>
        {
            try
            {
                var command = new UpdatePlanCommand(id, resource.Name, resource.Description, resource.Price, resource.Interval, resource.FeaturesJson);
                await commandService.Handle(command, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        }).RequireAuthorization();

        // ── Rutas para gestionar contratos y suscripciones de condominios ──
        var subs = app.MapGroup("/api/v1").WithTags("Subscriptions");

        // Listar las suscripciones activas del solicitante
        subs.MapGet("/subscriptions", async (System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var self = SelfId(user);
            var adminProjectIds = await db.Projects
                .Where(p => p.BuilderId == self)
                .Select(p => p.Id)
                .ToListAsync(ct);
            var subscriptions = await db.Subscriptions
                .Where(s => s.ProjectId == self || s.BuilderId == self || adminProjectIds.Contains(s.ProjectId) || self == 0)
                .ToListAsync(ct);
            var plansDict = await db.Plans.ToDictionaryAsync(p => p.Id, ct);
            var projectsDict = await db.Projects.ToDictionaryAsync(p => p.Id, ct);
            var result = subscriptions.Select(s =>
            {
                var project = projectsDict.TryGetValue(s.ProjectId, out var p) ? p : null;
                var adminId = project != null ? project.AdministratorId : s.BuilderId;
                return new
                {
                    s.Id,
                    s.ProjectId,
                    CondominiumId = s.ProjectId,
                    s.BuilderId,
                    AdministratorId = adminId,
                    s.PlanId,
                    s.Status,
                    s.StartDate,
                    s.EndDate,
                    ContractEndDate = s.EndDate,
                    s.PrepaidMonthSoles,
                    s.GuaranteeSoles,
                    s.ActivationFeeSoles,
                    s.EarlyTerminationFeeSoles,
                    s.GuaranteeRefundSoles,
                    Plan = plansDict.TryGetValue(s.PlanId, out var plan) ? new
                    {
                        plan.Id,
                        plan.Name,
                        plan.Price,
                        plan.PriceWithIgv,
                        plan.ContractMonths,
                        plan.Description,
                        plan.MaxUnits,
                        plan.MaxDevices,
                        Features = !string.IsNullOrWhiteSpace(plan.FeaturesJson)
                            ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(plan.FeaturesJson)
                            : new List<string>()
                    } : null
                };
            });
            return Results.Ok(result);
        }).RequireAuthorization();

        // Consultar una suscripción por identificador
        subs.MapGet("/subscriptions/{id:int}", async (int id, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var s = await db.Subscriptions.FindAsync([id], ct);
            if (s is null) return Results.NotFound();
            var project = await db.Projects.FindAsync([s.ProjectId], ct);
            var adminId = project != null ? project.AdministratorId : s.BuilderId;
            var self = SelfId(user);
            if (self != 0 && s.ProjectId != self && s.BuilderId != self && adminId != self) return Results.NotFound();
            var plan = await db.Plans.FindAsync([s.PlanId], ct);
            return Results.Ok(new
            {
                s.Id,
                s.ProjectId,
                CondominiumId = s.ProjectId,
                s.BuilderId,
                AdministratorId = adminId,
                s.PlanId,
                s.Status,
                s.StartDate,
                s.EndDate,
                ContractEndDate = s.EndDate,
                s.PrepaidMonthSoles,
                s.GuaranteeSoles,
                s.ActivationFeeSoles,
                s.EarlyTerminationFeeSoles,
                s.GuaranteeRefundSoles,
                Plan = plan is not null ? new
                {
                    plan.Id,
                    plan.Name,
                    plan.Price,
                    plan.PriceWithIgv,
                    plan.ContractMonths,
                    plan.Description,
                    plan.MaxUnits,
                    plan.MaxDevices,
                    Features = !string.IsNullOrWhiteSpace(plan.FeaturesJson)
                        ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(plan.FeaturesJson)
                        : new List<string>()
                } : null
            });
        }).RequireAuthorization();

        // Actualizar o escalar el plan de una suscripción validando la capacidad del edificio
        subs.MapPut("/subscriptions/{id:int}", async (int id, CreateSubscriptionRequest request, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var item = await db.Subscriptions.FindAsync([id], ct);
            if (item is null || !await OwnsProjectAsync(user, db, item.ProjectId, ct)) return Results.NotFound();

            // Validación de capacidad al escalar el plan
            var plan = await db.Plans.FindAsync([request.PlanId], ct);
            if (plan is null) return Results.NotFound(new { error = "El plan solicitado no existe." });

            var project = await db.Projects.FindAsync([item.ProjectId], ct);
            if (project is not null)
            {
                if (!plan.ValidateCapacity(project.TotalUnits, out var capacityError))
                {
                    return Results.BadRequest(new { error = capacityError });
                }
            }

            item.PlanId = request.PlanId;
            item.EndDate = request.EndDate;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();

        // Pago de activación del condominio: plan asignado por departamentos, primera cuota, activación y garantía
        subs.MapGet("/subscriptions/activation-quote", async (int condominiumId, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var project = await db.Projects.FindAsync([condominiumId], ct);
            if (project is null || !await OwnsProjectAsync(user, db, condominiumId, ct)) return Results.NotFound();
            var planName = Plan.NameForUnits(project.TotalUnits);
            var plan = await db.Plans.FirstOrDefaultAsync(p => p.Name == planName, ct);
            if (plan is null) return Results.NotFound(new { error = "El plan del condominio no está en el catálogo." });
            var communal = await CommunalDevicesAsync(db, project.Id, ct);
            var extras = plan.ExtraDevices(communal);
            return Results.Ok(new
            {
                CondominiumId = project.Id,
                project.TotalUnits,
                PlanId = plan.Id,
                PlanName = plan.Name,
                MonthlyPrice = plan.Price,
                MonthlyPriceWithIgv = plan.PriceWithIgv,
                PricePerUnitWithIgv = project.TotalUnits > 0 ? Math.Round(plan.PriceWithIgv / project.TotalUnits, 2) : 0m,
                plan.PrepaidLastMonthWithIgv,
                ExtraDevices = extras,
                ExtraDevicesMonthlyWithIgv = extras * Plan.ExtraDeviceMonthlyPriceWithIgv,
                Total = plan.ActivationTotalWith(communal),
                plan.ContractMonths
            });
        }).RequireAuthorization();

        // Equipos comunes del condominio frente al kit de su plan, y lo que los adicionales suman a la cuota mensual
        subs.MapGet("/subscriptions/device-allowance", async (int condominiumId, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var project = await db.Projects.FindAsync([condominiumId], ct);
            if (project is null || !await OwnsProjectAsync(user, db, condominiumId, ct)) return Results.NotFound();
            // Con suscripción activa manda su plan; si aún no tiene, el que le corresponde por departamentos
            var activePlanId = await db.Subscriptions.Where(s => s.ProjectId == project.Id && s.Status == "active").OrderByDescending(s => s.Id).Select(s => (int?)s.PlanId).FirstOrDefaultAsync(ct);
            var plan = (activePlanId is int planId ? await db.Plans.FindAsync([planId], ct) : null)
                ?? await db.Plans.FirstOrDefaultAsync(p => p.Name == Plan.NameForUnits(project.TotalUnits), ct)
                ?? new Plan { Name = Plan.NameForUnits(project.TotalUnits) };
            var communal = await CommunalDevicesAsync(db, project.Id, ct);
            var extras = plan.ExtraDevices(communal);
            return Results.Ok(new
            {
                CondominiumId = project.Id,
                PlanName = plan.Name,
                IncludedDevices = plan.MaxDevices,
                CommunalDevices = communal,
                ExtraDevices = extras,
                ExtraDevicePrice = Plan.ExtraDeviceMonthlyPrice,
                ExtraDevicePriceWithIgv = Plan.ExtraDeviceMonthlyPriceWithIgv,
                ExtrasMonthly = extras * Plan.ExtraDeviceMonthlyPrice,
                ExtrasMonthlyWithIgv = extras * Plan.ExtraDeviceMonthlyPriceWithIgv,
                MonthlyTotal = plan.MonthlyPriceWith(communal),
                MonthlyTotalWithIgv = plan.MonthlyPriceWithIgvWith(communal)
            });
        }).RequireAuthorization();

        // Liquidación de una cancelación: saldo de equipos pendiente según los meses que faltan del contrato
        subs.MapGet("/subscriptions/{id:int}/cancellation-quote", async (int id, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var item = await db.Subscriptions.FindAsync([id], ct);
            if (item is null || !await OwnsProjectAsync(user, db, item.ProjectId, ct)) return Results.NotFound();
            var plan = await db.Plans.FindAsync([item.PlanId], ct);
            if (plan is null) return Results.NotFound();
            var quote = plan.QuoteEarlyTermination(item.EndDate ?? item.StartDate.AddMonths(plan.ContractMonths), item.PrepaidMonthSoles, item.GuaranteeSoles, DateTimeOffset.UtcNow);
            return Results.Ok(new
            {
                SubscriptionId = item.Id,
                ContractEndDate = item.EndDate,
                quote.MonthsRemaining,
                quote.EquipmentBalance,
                quote.CreditApplied,
                quote.AmountDue,
                quote.Refund
            });
        }).RequireAuthorization();

        // Cancelar la suscripción registrando el saldo de equipos y lo que se devuelve del mes adelantado
        subs.MapPost("/subscriptions/{id:int}/cancel", async (int id, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var item = await db.Subscriptions.FindAsync([id], ct);
            if (item is null || !await OwnsProjectAsync(user, db, item.ProjectId, ct)) return Results.NotFound();
            var plan = await db.Plans.FindAsync([item.PlanId], ct);
            if (plan is null)
            {
                item.Cancel();
            }
            else
            {
                var contractEnd = item.EndDate ?? item.StartDate.AddMonths(plan.ContractMonths);
                item.Cancel(plan.QuoteEarlyTermination(contractEnd, item.PrepaidMonthSoles, item.GuaranteeSoles, DateTimeOffset.UtcNow));
            }
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();

        // Iniciar sesión de pago en Stripe validando la compatibilidad de departamentos y sensores
        subs.MapPost("/subscriptions/payments/sessions", async (PaymentCheckoutRequest request, System.Security.Claims.ClaimsPrincipal user, IConfiguration configuration, IPaymentProvider provider, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!await OwnsProjectAsync(user, db, request.BuilderId, ct)) return Results.Forbid();
            var restrictedKey = StripeRestrictedKeyResolver.Resolve(configuration);
            if (restrictedKey is null)
            {
                return Results.Problem(detail: "Stripe payment service is not configured.", statusCode: 503);
            }

            var plan = await db.Plans.FindAsync([request.PlanId], ct);
            if (plan is null) return Results.NotFound(new { error = "Plan no encontrado." });

            // Validación del límite de unidades y dispositivos antes de generar el cobro
            var project = await db.Projects.FindAsync([request.BuilderId], ct);
            if (project is not null)
            {
                // El plan no se elige: corresponde al número de departamentos del condominio
                if (plan.Name != Plan.NameForUnits(project.TotalUnits))
                {
                    return Results.BadRequest(new { error = $"Al condominio le corresponde el plan {Plan.NameForUnits(project.TotalUnits)} por sus {project.TotalUnits} departamentos." });
                }
                if (!plan.ValidateCapacity(project.TotalUnits, out var capacityError))
                {
                    return Results.BadRequest(new { error = capacityError });
                }
            }

            var options = StripeIntegrationOptions.Create(restrictedKey);
            var session = await provider.CreateCheckoutSessionAsync(request, options, ct);
            if (session is null)
            {
                return Results.Problem(detail: "Payment session could not be created with Stripe.", statusCode: 503);
            }
            return Results.Created($"/api/v1/subscriptions/payments/sessions/{session.Id}", new
            {
                session.Id,
                session.Url,
                sessionId = session.Id,
                checkoutUrl = session.Url,
                session.AmountInCents,
                options.UsesDynamicPaymentMethods
            });
        }).RequireAuthorization();

        // Confirmar sesión de pago y activar la suscripción del condominio
        subs.MapPatch("/subscriptions/payments/sessions/{sessionId}", async (string sessionId, IPaymentProvider provider, IoBuildDbContext db, IoBuild.Api.Devices.Application.Internal.CommandServices.KitProvisioningService kit, CancellationToken ct) =>
        {
            var confirmation = await provider.ConfirmSessionAsync(sessionId, ct);
            if (confirmation is null) return Results.Problem(statusCode: 503);
            // Una sesión abierta o expirada no activa el servicio
            if (!string.Equals(confirmation.Status, "paid", StringComparison.OrdinalIgnoreCase))
                return Results.Conflict(new { error = "El pago de activación todavía no se completó." });
            var paidPlan = await db.Plans.FindAsync([confirmation.PlanId], ct);
            if (paidPlan is null) return Results.NotFound(new { error = "Plan no encontrado." });

            var existing = await db.Subscriptions.FirstOrDefaultAsync(s => s.ProjectId == confirmation.BuilderId && s.PlanId == confirmation.PlanId && s.Status == "active", ct);
            
            // Caducar otras suscripciones activas del mismo condominio para evitar duplicidad
            var otherActiveSubs = await db.Subscriptions
                .Where(s => s.ProjectId == confirmation.BuilderId && s.Status == "active" && (existing == null || s.Id != existing.Id))
                .ToListAsync(ct);
            foreach (var sub in otherActiveSubs)
            {
                sub.Status = "expired";
                sub.EndDate = DateTime.UtcNow;
            }

            if (existing is null)
            {
                db.Subscriptions.Add(Subscription.Activate(confirmation.BuilderId, paidPlan, DateTimeOffset.UtcNow));
            }
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException mysql && mysql.Number == 1062)
            {
                return Results.Conflict(new { error = "Subscription already active; refresh to see current state." });
            }

            // Pago confirmado: el condominio pasa a activo y recibe el kit de equipos de su plan (no duplica si ya lo tiene)
            var project = await db.Projects.FindAsync([confirmation.BuilderId], ct);
            if (project is not null)
            {
                if (string.Equals(project.Status, "Pendiente", StringComparison.OrdinalIgnoreCase)) project.Status = "Activo";
                await db.SaveChangesAsync(ct);
                await kit.ProvisionAsync(project.Id, ct);
            }

            return Results.Ok(confirmation);
        });

        // Consultar el historial de comprobantes de pago emitidos
        subs.MapGet("/subscriptions/payments/invoices", async (int builderId, System.Security.Claims.ClaimsPrincipal user, IPaymentProvider provider, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!await OwnsProjectAsync(user, db, builderId, ct)) return Results.Forbid();
            var invoices = await provider.GetInvoicesAsync(builderId, ct);
            if (invoices is not null)
            {
                return Results.Ok(invoices);
            }

            // Comprobantes sintetizados a partir de la suscripción del condominio
            var builderSubs = await db.Subscriptions.Where(s => s.ProjectId == builderId).ToListAsync(ct);
            var plansDict = await db.Plans.ToDictionaryAsync(p => p.Id, ct);

            var fallbackInvoices = builderSubs.Select(s =>
            {
                var plan = plansDict.TryGetValue(s.PlanId, out var p) ? p : null;
                var amountInCents = plan is not null ? (long)(plan.ActivationTotal * 100) : 0L;
                return new
                {
                    Id = $"in_sub_{s.Id}",
                    Status = s.Status == "active" ? "paid" : s.Status,
                    AmountInCents = amountInCents,
                    Amount = plan?.ActivationTotal ?? 0m,
                    Currency = "PEN",
                    Description = $"Activación Plan {plan?.Name ?? "IoBuild HaaS"}: primer mes y último mes adelantado",
                    Date = s.StartDate,
                    ReceiptUrl = (string?)null
                };
            }).ToList();

            return Results.Ok(fallbackInvoices);
        }).RequireAuthorization();

        // Registrar una suscripción validando la capacidad del condominio
        subs.MapPost("/subscriptions", async (CreateSubscriptionRequest request, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!await OwnsProjectAsync(user, db, request.BuilderId, ct)) return Results.Forbid();

            // Valida que el plan exista y admita los departamentos del condominio
            var plan = await db.Plans.FindAsync([request.PlanId], ct);
            if (plan is null) return Results.NotFound(new { error = "El plan especificado no existe." });

            var project = await db.Projects.FindAsync([request.BuilderId], ct);
            if (project is not null)
            {
                if (!plan.ValidateCapacity(project.TotalUnits, out var capacityError))
                {
                    return Results.BadRequest(new { error = capacityError });
                }
            }

            var subscription = new Subscription
            {
                ProjectId = request.BuilderId,
                PlanId = request.PlanId,
                StartDate = request.StartDate,
                EndDate = request.EndDate ?? request.StartDate.AddMonths(plan.ContractMonths)
            };
            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/subscriptions/{subscription.Id}", subscription);
        }).RequireAuthorization();

        // Procesar notificaciones webhook de eventos de pago de Stripe
        subs.MapPost("/webhooks/stripe", async (HttpRequest request, StripeWebhookProcessor processor, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var payload = await reader.ReadToEndAsync(ct);
            using var document = ParseWebhookPayload(payload);
            if (document is null) return Results.BadRequest();
            var eventId = document.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
            var eventType = document.RootElement.TryGetProperty("type", out var type) ? type.GetString() : null;
            if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(eventType)) return Results.BadRequest();
            var signature = request.Headers["Stripe-Signature"].ToString();
            return await processor.ProcessAsync(new StripeWebhookRequest(eventId, eventType, payload, signature), ct)
                ? Results.Ok(new { received = true, eventId })
                : Results.Unauthorized();
        }).AllowAnonymous();
    }
}
