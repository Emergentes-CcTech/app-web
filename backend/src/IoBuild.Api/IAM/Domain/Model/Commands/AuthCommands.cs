namespace IoBuild.Api.IAM.Domain.Model.Commands;

/// <summary>
/// IAM Domain: commands and results. Kept in IoBuild.Api.Iam namespace for
/// backward compatibility with existing tests and Program.cs.
/// In DDD terms these are application commands; they live in Domain/Model
/// to make the ubiquitous language explicit per BC.
/// </summary>
public sealed record RegisterUser(string Email, string Password, string Role);
public sealed record SignIn(string Email, string Password);
public sealed record AuthenticatedUser(int Id, string Email, string Role, string Token);
