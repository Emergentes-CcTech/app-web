namespace IoBuild.Api.IAM.Interfaces.REST.Resources;

/// <summary>
/// IAM REST resources (DTOs). Wire contracts unchanged (snake/kebab not applied to IAM).
/// Transform assemblers live in Transform/.
/// </summary>
public sealed record RegisterUserResource(string Email, string Password, string Role);
public sealed record SignInResource(string Email, string Password);
public sealed record AuthenticatedUserResource(int Id, string Email, string Role, string Token);
