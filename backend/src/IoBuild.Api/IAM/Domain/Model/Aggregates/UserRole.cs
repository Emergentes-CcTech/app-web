namespace IoBuild.Api.IAM.Domain.Model.Aggregates;

/// <summary>
/// Roles de usuario en el contexto IAM para la plataforma IoBuild:
/// PropertyManager: Empresa administradora de condominios residenciales (B2B).
/// Resident: Residente o propietario de departamento privativo (B2C).
/// </summary>
public enum UserRole
{
    // Administrador de condominios residenciales
    PropertyManager,

    // Residente o propietario de un departamento en el condominio
    Resident
}
