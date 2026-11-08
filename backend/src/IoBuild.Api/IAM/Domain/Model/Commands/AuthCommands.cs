namespace IoBuild.Api.IAM.Domain.Model.Commands;

/// <summary>
/// Comandos y resultados del dominio en el contexto IAM.
/// Modela los contratos de registro, inicio de sesión y usuario autenticado con contexto de condominio.
/// </summary>
public sealed record RegisterUser(string Email, string Password, string Role);

public sealed record SignIn(string Email, string Password);

// Representa la sesión autenticada con datos del departamento vinculado para residentes
public sealed record AuthenticatedUser(
    int Id,
    string Email,
    string Role,
    string Token,
    int? ProjectId = null,
    int? UnitId = null,
    string? UnitNumber = null,
    decimal? AliquotPercentage = null);
