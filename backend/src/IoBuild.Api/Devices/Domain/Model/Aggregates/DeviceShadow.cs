namespace IoBuild.Api.Devices.Domain.Model.Aggregates;

public sealed class DeviceShadow
{
    public int DeviceId { get; set; }
    public string DesiredJson { get; set; } = "{}";
    public string? ReportedJson { get; set; }
    public DateTimeOffset ReportedAt { get; set; } = DateTimeOffset.MinValue;
    public long ShadowVersion { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
