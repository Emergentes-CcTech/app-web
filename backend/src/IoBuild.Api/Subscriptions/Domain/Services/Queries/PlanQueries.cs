namespace IoBuild.Api.Subscriptions.Domain.Services.Queries;

public record GetAllPlansQuery;
public record GetPlanByIdQuery(int Id);
public record GetPlanByNameQuery(string Name);
