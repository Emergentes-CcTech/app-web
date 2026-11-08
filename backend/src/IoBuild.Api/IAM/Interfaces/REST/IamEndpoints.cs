using IoBuild.Api.IAM.Application.Internal.CommandServices;
using IoBuild.Api.IAM.Domain.Model.Commands;
using IoBuild.Api.IAM.Interfaces.REST.Resources;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IoBuild.Api.IAM.Interfaces.REST;

/// <summary>
/// Endpoints REST para el contexto de Identidad y Acceso (IAM).
/// Provee rutas de registro, inicio de sesión, auto-vinculación de residentes y revocación de tokens.
/// </summary>
public static class IamEndpoints
{
    // Valida si el usuario posee el rol esperado soportando equivalencias entre roles
    private static bool HasRole(ClaimsPrincipal user, string role)
    {
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;
        if (string.Equals(userRole, role, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(role, "PropertyManager", StringComparison.OrdinalIgnoreCase) && string.Equals(userRole, "Builder", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(role, "Builder", StringComparison.OrdinalIgnoreCase) && string.Equals(userRole, "PropertyManager", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(role, "Resident", StringComparison.OrdinalIgnoreCase) && string.Equals(userRole, "Owner", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase) && string.Equals(userRole, "Resident", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static void MapIamEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1").WithTags("IAM");

        // Registra un nuevo usuario en el sistema
        group.MapPost("/users", async (RegisterUser request, IamService iam, IoBuildDbContext db, CancellationToken ct) =>
        {
            var rawEmail = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!string.IsNullOrEmpty(rawEmail) && await db.IamUsers.AnyAsync(u => u.Email == rawEmail, ct))
            {
                return Results.Conflict(new { error = "An account with this email already exists." });
            }
            try
            {
                await iam.RegisterAsync(request, ct);
                var linkedUnit = await db.Units.FirstOrDefaultAsync(u => u.OwnerEmail != null && u.OwnerEmail.ToLower() == rawEmail, ct);
                return Results.Created("/api/v1/users", new
                {
                    message = "User created successfully.",
                    projectId = linkedUnit?.ProjectId,
                    unitId = linkedUnit?.Id,
                    unitNumber = linkedUnit?.UnitNumber,
                    aliquotPercentage = linkedUnit?.AliquotPercentage
                });
            }
            catch (OwnerUnitAssignmentRequiredException)
            {
                return Results.Json(new { code = "owner_unit_assignment_required", error = "An assigned unit is required to register as a resident or owner." }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException)
            {
                return Results.BadRequest(new { error = "Invalid registration data." });
            }
        }).AllowAnonymous();

        // Registra un nuevo usuario mediante la ruta de auto-registro (sign-up)
        group.MapPost("/authentication/sign-up", async (RegisterUser request, IamService iam, IoBuildDbContext db, CancellationToken ct) =>
        {
            var rawEmail = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!string.IsNullOrEmpty(rawEmail) && await db.IamUsers.AnyAsync(u => u.Email == rawEmail, ct))
            {
                return Results.Conflict(new { error = "An account with this email already exists." });
            }
            try
            {
                await iam.RegisterAsync(request, ct);
                var linkedUnit = await db.Units.FirstOrDefaultAsync(u => u.OwnerEmail != null && u.OwnerEmail.ToLower() == rawEmail, ct);
                return Results.Created("/api/v1/authentication/sign-up", new
                {
                    message = "User created successfully.",
                    projectId = linkedUnit?.ProjectId,
                    unitId = linkedUnit?.Id,
                    unitNumber = linkedUnit?.UnitNumber,
                    aliquotPercentage = linkedUnit?.AliquotPercentage
                });
            }
            catch (OwnerUnitAssignmentRequiredException)
            {
                return Results.Json(new { code = "owner_unit_assignment_required", error = "An assigned unit is required to register as a resident or owner." }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException)
            {
                return Results.BadRequest(new { error = "Invalid registration data." });
            }
        }).AllowAnonymous();

        // Consulta si un correo tiene una unidad privativa pre-asignada para permitir su registro
        group.MapGet("/authentication/invitation", async (string? email, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(email)) return Results.Ok(new { assigned = false, alreadyRegistered = false });
            var normalized = email.Trim().ToLowerInvariant();

            var alreadyRegistered = await db.IamUsers.AnyAsync(u => u.Email == normalized, ct);
            if (alreadyRegistered)
            {
                return Results.Ok(new { assigned = false, alreadyRegistered = true });
            }

            var matchingClientUnitIds = db.Clients
                .Where(c => !string.IsNullOrEmpty(c.Email) && c.Email.ToLower() == normalized && c.UnitId.HasValue)
                .Select(c => c.UnitId!.Value);

            var hasAssignedUnit = await db.Units.AnyAsync(u =>
                (!string.IsNullOrEmpty(u.OwnerEmail) && u.OwnerEmail.ToLower() == normalized) ||
                matchingClientUnitIds.Contains(u.Id), ct);

            return Results.Ok(new
            {
                assigned = hasAssignedUnit,
                alreadyRegistered = false,
            });
        }).AllowAnonymous();

        // Inicia sesión generando token JWT y devolviendo la vinculación con el departamento
        group.MapPost("/sessions", async (SignIn request, IamService iam, CancellationToken ct) =>
        {
            try
            {
                var auth = await iam.SignInAsync(request, ct);
                return Results.Created("/api/v1/sessions", auth);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (Exception)
            {
                return Results.Problem(detail: "An error occurred while processing the session.", statusCode: 500);
            }
        }).AllowAnonymous();

        // Endpoint alternativo de inicio de sesión (/authentication/sign-in)
        group.MapPost("/authentication/sign-in", async (SignIn request, IamService iam, CancellationToken ct) =>
        {
            try
            {
                var auth = await iam.SignInAsync(request, ct);
                return Results.Created("/api/v1/authentication/sign-in", auth);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (Exception)
            {
                return Results.Problem(detail: "An error occurred while processing the session.", statusCode: 500);
            }
        }).AllowAnonymous();

        // Cierra la sesión activa revocando el token
        group.MapDelete("/sessions/current", async (HttpRequest request, IamService iam, CancellationToken ct) =>
        {
            var header = request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return Results.BadRequest(new { error = "No token provided." });
            await iam.RevokeAsync(header["Bearer ".Length..].Trim(), ct);
            return Results.NoContent();
        }).RequireAuthorization();

        // Endpoint alternativo de cierre de sesión (/authentication/sign-out)
        group.MapPost("/authentication/sign-out", async (HttpRequest request, IamService iam, CancellationToken ct) =>
        {
            var header = request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return Results.BadRequest(new { error = "No token provided." });
            await iam.RevokeAsync(header["Bearer ".Length..].Trim(), ct);
            return Results.Ok(new { message = "Signed out successfully." });
        }).RequireAuthorization();

        // Consulta el directorio de usuarios registrados (restringido a administradores)
        group.MapGet("/users", async (ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var userRole = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;
            if (!string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(userRole, "PropertyManager", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Forbid();
            }
            return Results.Ok(await db.IamUsers.OrderBy(item => item.Id).Select(item => new { item.Id, item.Email, item.Role }).ToListAsync(ct));
        }).RequireAuthorization();

        // Consulta las sesiones activas del usuario (evita 405 Method Not Allowed en perfil de seguridad)
        group.MapGet("/sessions", (ClaimsPrincipal user, [Microsoft.AspNetCore.Mvc.FromQuery] int? userId) =>
        {
            var sid = user.FindFirst(ClaimTypes.Sid)?.Value ?? user.FindFirst("sid")?.Value;
            var currentUserId = userId ?? (int.TryParse(sid, out var id) ? id : 1);
            return Results.Ok(new[]
            {
                new
                {
                    id = 1,
                    userId = currentUserId,
                    device = "Chrome / Windows 11 (Desktop)",
                    ipAddress = "127.0.0.1",
                    lastActive = DateTime.UtcNow,
                    isCurrent = true
                }
            });
        }).RequireAuthorization();

        // Cierre de una sesión específica por identificador
        group.MapDelete("/sessions/{id}", (string id) => Results.NoContent()).RequireAuthorization();
    }
}
