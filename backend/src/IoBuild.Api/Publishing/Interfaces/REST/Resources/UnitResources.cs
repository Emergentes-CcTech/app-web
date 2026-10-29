namespace IoBuild.Api.Publishing.Interfaces.REST.Resources;

public record UnitResource(int Id, int ProjectId, string UnitNumber, int Floor, string RoomNumber, string? OwnerEmail, int? OwnerId, string Status);
public record CreateUnitResource(int ProjectId, string UnitNumber, int? OwnerId = null, int Floor = 0, string RoomNumber = "");
public record AssignUnitOwnerResource(string? OwnerEmail = null, int? OwnerId = null);
