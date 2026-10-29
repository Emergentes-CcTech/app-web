using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IoBuild.Api.IAM.Domain.Model.Aggregates;
using Microsoft.IdentityModel.Tokens;

namespace IoBuild.Api.IAM.Infrastructure.Tokens;

/// <summary>
/// IAM Infrastructure: JWT issuance. Depends only on IamUser aggregate.
/// </summary>
public sealed class JwtTokenIssuer(string secret)
{
    private readonly byte[] key = Encoding.UTF8.GetBytes(secret);

    public string Issue(IamUser user)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(ClaimTypes.Sid, user.Id.ToString()), new Claim(ClaimTypes.Email, user.Email), new Claim(ClaimTypes.Role, user.Role)]),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
