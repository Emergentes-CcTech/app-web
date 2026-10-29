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
            var subscriptions = await db.Subscriptions.Where(s => s.ProjectId == self || s.BuilderId == self || self == 0).ToListAsync(ct);
            var plansDict = await db.Plans.ToDictionaryAsync(p => p.Id, ct);
            var result = subscriptions.Select(s => new
            {
                s.Id,
                s.ProjectId,
                s.BuilderId,
                s.PlanId,
                s.Status,
                s.StartDate,
                s.EndDate,
                Plan = plansDict.TryGetValue(s.PlanId, out var plan) ? new
                {
                    plan.Id,
                    plan.Name,
                    plan.Price,
                    plan.Description,
                    plan.MaxUnits,
                    plan.MaxDevices,
                    Features = !string.IsNullOrWhiteSpace(plan.FeaturesJson)
                        ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(plan.FeaturesJson)
                        : new List<string>()
                } : null
            });
            return Results.Ok(result);
        }).RequireAuthorization();

        // Consultar una suscripción por identificador
        subs.MapGet("/subscriptions/{id:int}", async (int id, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var s = await db.Subscriptions.FindAsync([id], ct);
            if (s is null || !OwnsBuilder(user, s.ProjectId)) return Results.NotFound();
            var plan = await db.Plans.FindAsync([s.PlanId], ct);
            return Results.Ok(new
            {
                s.Id,
                s.ProjectId,
                s.BuilderId,
                s.PlanId,
                s.Status,
                s.StartDate,
                s.EndDate,
                Plan = plan is not null ? new
                {
                    plan.Id,
                    plan.Name,
                    plan.Price,
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
            if (item is null || !OwnsBuilder(user, item.ProjectId)) return Results.NotFound();

            // Validación de capacidad al escalar el plan
            var plan = await db.Plans.FindAsync([request.PlanId], ct);
            if (plan is null) return Results.NotFound(new { error = "El plan solicitado no existe." });

            var project = await db.Projects.FindAsync([item.ProjectId], ct);
            if (project is not null)
            {
                var deviceCount = await db.Devices.CountAsync(d => d.ProjectId == item.ProjectId, ct);
                if (!plan.ValidateCapacity(project.TotalUnits, deviceCount, out var capacityError))
                {
                    return Results.BadRequest(new { error = capacityError });
                }
            }

            item.PlanId = request.PlanId;
            item.EndDate = request.EndDate;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();

        // Cancelar o desvincular una suscripción activa
        subs.MapPost("/subscriptions/{id:int}/cancel", async (int id, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var item = await db.Subscriptions.FindAsync([id], ct);
            if (item is null || !OwnsBuilder(user, item.ProjectId)) return Results.NotFound();
            item.Cancel();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();

        // Iniciar sesión de pago en Stripe validando la compatibilidad de departamentos y sensores
        subs.MapPost("/subscriptions/payments/sessions", async (PaymentCheckoutRequest request, System.Security.Claims.ClaimsPrincipal user, IConfiguration configuration, IPaymentProvider provider, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!OwnsBuilder(user, request.BuilderId)) return Results.Forbid();
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
                var deviceCount = await db.Devices.CountAsync(d => d.ProjectId == request.BuilderId, ct);
                if (!plan.ValidateCapacity(project.TotalUnits, deviceCount, out var capacityError))
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
        subs.MapPatch("/subscriptions/payments/sessions/{sessionId}", async (string sessionId, IPaymentProvider provider, IoBuildDbContext db, CancellationToken ct) =>
        {
            var confirmation = await provider.ConfirmSessionAsync(sessionId, ct);
            if (confirmation is null) return Results.Problem(statusCode: 503);

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
                db.Subscriptions.Add(new Subscription
                {
                    ProjectId = confirmation.BuilderId,
                    PlanId = confirmation.PlanId,
                    Status = "active",
                    StartDate = DateTime.UtcNow
                });
            }
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException mysql && mysql.Number == 1062)
            {
                return Results.Conflict(new { error = "Subscription already active; refresh to see current state." });
            }

            return Results.Ok(confirmation);
        });

        // Consultar el historial de comprobantes de pago emitidos
        subs.MapGet("/subscriptions/payments/invoices", async (int builderId, System.Security.Claims.ClaimsPrincipal user, IPaymentProvider provider, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!OwnsBuilder(user, builderId)) return Results.Forbid();
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
                var amountInCents = plan is not null ? (long)(plan.Price * 100) : 50000L;
                return new
                {
                    Id = $"in_sub_{s.Id}",
                    Status = s.Status == "active" ? "paid" : s.Status,
                    AmountInCents = amountInCents,
                    Amount = plan?.Price ?? 500m,
                    Currency = "PEN",
                    Description = $"Suscripción Plan {plan?.Name ?? "IoBuild HaaS"}",
                    Date = s.StartDate,
                    ReceiptUrl = (string?)null
                };
            }).ToList();

            return Results.Ok(fallbackInvoices);
        }).RequireAuthorization();

        // Registrar una suscripción validando la capacidad del condominio
        subs.MapPost("/subscriptions", async (CreateSubscriptionRequest request, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!OwnsBuilder(user, request.BuilderId)) return Results.Forbid();

            // Valida que el plan exista y admita los departamentos del condominio
            var plan = await db.Plans.FindAsync([request.PlanId], ct);
            if (plan is null) return Results.NotFound(new { error = "El plan especificado no existe." });

            var project = await db.Projects.FindAsync([request.BuilderId], ct);
            if (project is not null)
            {
                var deviceCount = await db.Devices.CountAsync(d => d.ProjectId == request.BuilderId, ct);
                if (!plan.ValidateCapacity(project.TotalUnits, deviceCount, out var capacityError))
                {
                    return Results.BadRequest(new { error = capacityError });
                }
            }

            var subscription = new Subscription
            {
                ProjectId = request.BuilderId,
                PlanId = request.PlanId,
                StartDate = request.StartDate,
                EndDate = request.EndDate ?? request.StartDate.AddMonths(12)
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
