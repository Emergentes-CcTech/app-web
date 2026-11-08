namespace IoBuild.Api.IAM.Interfaces.REST.Resources;

/// <summary>
/// Recursos REST (DTOs) del contexto IAM.
/// Incluye los campos de auto-vinculación de departamentos y alícuota para residentes.
/// </summary>
public sealed record RegisterUserResource(string Email, string Password, string Role);

public sealed record SignInResource(string Email, string Password);

public sealed record AuthenticatedUserResource(
    int Id,
    string Email,
    string Role,
    string Token,
    int? ProjectId = null,
    int? UnitId = null,
    string? UnitNumber = null,
    decimal? AliquotPercentage = null);
