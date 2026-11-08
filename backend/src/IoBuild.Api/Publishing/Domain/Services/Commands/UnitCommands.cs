namespace IoBuild.Api.Publishing.Domain.Services.Commands;

public record CreateUnitCommand(int ProjectId, string UnitNumber, int? OwnerId = null, int Floor = 0, string RoomNumber = "", decimal AreaM2 = 75.0m, decimal AliquotPercentage = 0.0m);
public record AssignUnitOwnerEmailCommand(int UnitId, string? OwnerEmail, int? OwnerId = null);
