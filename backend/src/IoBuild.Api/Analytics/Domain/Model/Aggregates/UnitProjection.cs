namespace IoBuild.Api.Analytics.Domain.Model.Aggregates;

public sealed class UnitProjection
{
    public int UnitId { get; set; }
    public int ProjectId { get; set; }
    public int BuilderUserId { get; set; }
    public int? OwnerUserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime LastEventAt { get; set; }
    public int? Floor { get; set; }
    public string? RoomNumber { get; set; }
    public string? OwnerEmail { get; set; }
}
