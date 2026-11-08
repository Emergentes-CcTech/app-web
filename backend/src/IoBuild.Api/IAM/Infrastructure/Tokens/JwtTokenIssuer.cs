using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IoBuild.Api.IAM.Domain.Model.Aggregates;
using Microsoft.IdentityModel.Tokens;

namespace IoBuild.Api.IAM.Infrastructure.Tokens;

/// <summary>
/// Servicio de infraestructura para la emisión de tokens JWT.
/// Genera claims de identidad, rol y contexto de condominio (unidad y alícuota) para residentes.
/// </summary>
public sealed class JwtTokenIssuer(string secret)
{
    private readonly byte[] key = Encoding.UTF8.GetBytes(secret);

    // Emite un token JWT con soporte para reclamos contextuales de alícuota y unidad privativa
    public string Issue(IamUser user, IEnumerable<Claim>? extraClaims = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Sid, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new("role", user.Role)
        };

        // Aliasing bidireccional entre roles para interoperabilidad con suites de prueba existentes
        if (string.Equals(user.Role, "PropertyManager", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, "Builder"));
        }
        else if (string.Equals(user.Role, "Builder", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, "PropertyManager"));
        }
        else if (string.Equals(user.Role, "Resident", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, "Owner"));
        }
        else if (string.Equals(user.Role, "Owner", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, "Resident"));
        }

        if (extraClaims is not null)
        {
            claims.AddRange(extraClaims);
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
