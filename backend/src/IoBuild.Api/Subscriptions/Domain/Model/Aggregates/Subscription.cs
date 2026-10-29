namespace IoBuild.Api.Subscriptions.Domain.Model.Aggregates;

/// <summary>
/// Subscriptions BC aggregate.
/// </summary>
public sealed class Subscription
{
    public int Id { get; set; }
    public int BuilderId { get; set; }
    public int PlanId { get; set; }
    public string Status { get; set; } = "active";
    public DateTimeOffset StartDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndDate { get; set; }
}
