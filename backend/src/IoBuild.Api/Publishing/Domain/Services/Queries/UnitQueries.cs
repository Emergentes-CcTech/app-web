namespace IoBuild.Api.Publishing.Domain.Services.Queries;

public record GetAllUnitsQuery;
public record GetUnitByIdQuery(int Id);
public record GetUnitsByProjectIdQuery(int ProjectId);
public record GetUnitsByOwnerIdQuery(int OwnerId);
