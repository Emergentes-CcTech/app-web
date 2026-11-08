namespace IoBuild.Api.Subscriptions.Domain.Services.Commands;

public record CreatePlanCommand(string Name, string Description, decimal Price, string Interval = "monthly", string FeaturesJson = "[]");
public record UpdatePlanCommand(int Id, string Name, string Description, decimal Price, string Interval, string FeaturesJson);
