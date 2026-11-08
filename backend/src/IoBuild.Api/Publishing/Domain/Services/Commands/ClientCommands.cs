namespace IoBuild.Api.Publishing.Domain.Services.Commands;

public record CreateClientCommand(
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

public record UpdateClientCommand(
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
    string? UnitNumber = null);

public record DeleteClientCommand(int Id);
