namespace IoBuild.Api.Analytics.Domain.Model.Aggregates;

public sealed class ProjectProjection
{
    public int ProjectId { get; set; }
    public int BuilderUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime LastEventAt { get; set; }
}
