namespace IoBuild.Api.Analytics.Domain.Model.Aggregates;

public sealed class DeviceProjection
{
    public int DeviceId { get; set; }
    public int OwnerUserId { get; set; }
    public int? ProjectId { get; set; }
    public int? UnitId { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime LastEventAt { get; set; }
    public int? FloorNumber { get; set; }
    public string? DeviceName { get; set; }
}
