using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IoBuild.Api.IAM.Domain.Model.Aggregates;
using IoBuild.Api.IAM.Domain.Model.Commands;
using IoBuild.Api.IAM.Domain.Model.Entities;
using IoBuild.Api.IAM.Domain.Services;
using IoBuild.Api.IAM.Infrastructure.Hashing;
using IoBuild.Api.IAM.Infrastructure.Tokens;
using IoBuild.Api.Persistence;
using IoBuild.Api.Workflows;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.IAM.Application.Internal.CommandServices;

/// <summary>
/// Servicio de aplicación para el contexto IAM: autenticación, revocación y auto-vinculación.
/// Realiza la resolución de proyectos, departamentos y porcentajes de alícuotas para el residente.
/// </summary>
public sealed class IamService(
    IoBuildDbContext dbContext,
    PasswordHasher passwordHasher,
    JwtTokenIssuer tokenIssuer,
    IWorkflow<RegisterUser, int> registrationWorkflow) : IIamService
{
    // Registra un nuevo usuario en la plataforma
    public async Task RegisterAsync(RegisterUser request, CancellationToken cancellationToken = default) =>
        await registrationWorkflow.ExecuteAsync(request, cancellationToken);

    // Inicia sesión validando credenciales y vinculando el contexto de condominio
    public async Task<AuthenticatedUser> SignInAsync(SignIn request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email)) throw new UnauthorizedAccessException("Invalid email or password.");
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.IamUsers.SingleOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash)) throw new UnauthorizedAccessException("Invalid email or password.");

        int? projectId = null;
        int? unitId = null;
        string? unitNumber = null;
        decimal? aliquotPercentage = null;

        // Si es residente o propietario, resuelve la auto-vinculación con su unidad y alícuota en Cadastre
        var isResident = string.Equals(user.Role, "Resident", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(user.Role, "Owner", StringComparison.OrdinalIgnoreCase);

        if (isResident)
        {
            var unit = await dbContext.Units.FirstOrDefaultAsync(u =>
                (u.OwnerEmail != null && u.OwnerEmail.ToLower() == normalizedEmail) ||
                u.OwnerId == user.Id, cancellationToken);

            if (unit is not null)
            {
                if (unit.OwnerId != user.Id)
                {
                    unit.OwnerId = user.Id;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                projectId = unit.ProjectId;
                unitId = unit.Id;
                unitNumber = unit.UnitNumber;
                aliquotPercentage = unit.AliquotPercentage;
            }
        }
        else
        {
            // Para PropertyManager/Builder, obtiene el condominio asignado si existe
            var project = await dbContext.Projects.FirstOrDefaultAsync(p =>
                p.BuilderId == user.Id, cancellationToken);
            if (project is not null)
            {
                projectId = project.Id;
            }
        }

        // Construye los reclamos (claims) contextuales para el token JWT
        var extraClaims = new List<Claim>();
        if (projectId.HasValue) extraClaims.Add(new Claim("projectId", projectId.Value.ToString()));
        if (unitId.HasValue) extraClaims.Add(new Claim("unitId", unitId.Value.ToString()));
        if (!string.IsNullOrEmpty(unitNumber)) extraClaims.Add(new Claim("unitNumber", unitNumber));
        if (aliquotPercentage.HasValue)
        {
            extraClaims.Add(new Claim("aliquotPercentage", aliquotPercentage.Value.ToString("F4", CultureInfo.InvariantCulture)));
        }

        var token = tokenIssuer.Issue(user, extraClaims);
        return new AuthenticatedUser(user.Id, user.Email, user.Role, token, projectId, unitId, unitNumber, aliquotPercentage);
    }

    // Revoca un token activo para cerrar sesión
    public async Task RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        var hash = HashToken(token);
        if (await dbContext.RevokedTokens.AnyAsync(item => item.TokenHash == hash, cancellationToken)) return;
        dbContext.RevokedTokens.Add(new RevokedToken { TokenHash = hash, ExpiresAt = DateTimeOffset.UtcNow.AddDays(7), RevokedAt = DateTimeOffset.UtcNow });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Consulta si un token ha sido revocado
    public Task<bool> IsRevokedAsync(string token, CancellationToken cancellationToken = default) =>
        dbContext.RevokedTokens.AnyAsync(item => item.TokenHash == HashToken(token) && item.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken);

    // Calcula el hash criptográfico del token para indexación y búsqueda
    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
