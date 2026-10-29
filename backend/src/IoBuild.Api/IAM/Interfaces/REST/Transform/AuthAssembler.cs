using IoBuild.Api.IAM.Domain.Model.Commands;

namespace IoBuild.Api.IAM.Interfaces.REST.Transform;

/// <summary>
/// Assembler between REST resources and domain commands.
/// </summary>
public static class AuthAssembler
{
    public static RegisterUser ToCommand(IAM.Interfaces.REST.Resources.RegisterUserResource r) => new(r.Email, r.Password, r.Role);
    public static SignIn ToCommand(IAM.Interfaces.REST.Resources.SignInResource r) => new(r.Email, r.Password);
}
