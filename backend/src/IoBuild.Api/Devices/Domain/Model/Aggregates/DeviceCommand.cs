namespace IoBuild.Api.Devices.Domain.Model.Aggregates;

public sealed class DeviceCommand
{
    public long Id { get; set; }
    public int DeviceId { get; set; }
    public string CommandId { get; set; } = string.Empty;
    public string DesiredJson { get; set; } = "{}";
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public int PublishAttempts { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
}
