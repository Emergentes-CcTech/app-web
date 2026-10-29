using IoBuild.Api.Publishing.Domain.Model.ValueObjects;

namespace IoBuild.Api.Publishing.Interfaces.REST.Resources;

/// <summary>
/// Contratos de datos (DTOs) para consultas y operaciones sobre departamentos en Catastro.
/// </summary>
public record UnitResource(
    int Id,
    int ProjectId,
    string UnitNumber,
    int Floor,
    string RoomNumber,
    string? OwnerEmail,
    int? OwnerId,
    string Status,
    decimal AreaM2 = 75.0m,
    decimal AliquotPercentage = 0.0m,
    string? ResidentEmail = null,
    string? ResidentFullName = null,
    string AccountStatus = "Al día"
);

public record CreateUnitResource(
    int ProjectId,
    string UnitNumber,
    int? OwnerId = null,
    int Floor = 0,
    string RoomNumber = "",
    decimal AreaM2 = 75.0m,
    decimal AliquotPercentage = 0.0m
);

public record AssignUnitOwnerResource(string? OwnerEmail = null, int? OwnerId = null, string? ResidentFullName = null);

// DTO para actualización de alícuotas bajo Ley 27157
public sealed record UpdateUnitAliquotItem(int UnitId, decimal AliquotPercentage);

// DTO para registro de áreas comunes del condominio
public sealed record CreateCommonAreaRequest(string Name, CommonAreaSubsystem Subsystem, string Location, string Description);
