namespace IoBuild.Api.Publishing.Interfaces.REST.Resources;

public record ClientResource(
    int Id,
    string FullName,
    string ProjectName,
    string AccountStatement,
    int BuilderId,
    int ProjectId,
    string? Email = null,
    string? PhoneNumber = null,
    string? Address = null,
    int? UnitId = null,
    string? UnitNumber = null,
    int DeviceCount = 0);

public record CreateClientResource(
    string FullName,
    string ProjectName,
    string AccountStatement,
    int BuilderId,
    int ProjectId,
    string? Email = null,
    string? PhoneNumber = null,
    string? Address = null,
    int? UnitId = null,
    string? UnitNumber = null);

public record UpdateClientResource(
    string FullName,
    string ProjectName,
    string AccountStatement,
    int BuilderId,
    int ProjectId,
    string? Email = null,
    string? PhoneNumber = null,
    string? Address = null,
    int? UnitId = null,
    string? UnitNumber = null);
