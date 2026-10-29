namespace IoBuild.Api.Devices.Domain.Model.Entities;

public sealed class DeviceRegistryTombstone
{
    public int DeviceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
    public int PublishAttempts { get; set; }
}
