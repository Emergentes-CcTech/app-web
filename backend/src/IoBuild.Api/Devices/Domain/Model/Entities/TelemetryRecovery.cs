namespace IoBuild.Api.Devices.Domain.Model.Entities;

public sealed class TelemetryRecovery
{
    public long Id { get; set; }
    public string EventId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
