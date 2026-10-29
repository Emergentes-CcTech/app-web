namespace IoBuild.Api.Publishing.Domain.Services.Commands;

public record CreateUnitCommand(int ProjectId, string UnitNumber, int? OwnerId = null, int Floor = 0, string RoomNumber = "");
public record AssignUnitOwnerEmailCommand(int UnitId, string? OwnerEmail, int? OwnerId = null);
