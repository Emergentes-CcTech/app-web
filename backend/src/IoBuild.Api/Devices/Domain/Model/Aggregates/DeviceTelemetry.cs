namespace IoBuild.Api.Devices.Domain.Model.Aggregates;

public sealed class DeviceTelemetry
{
    public long Id { get; set; }
    public int DeviceId { get; set; }
    public string EventId { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ReportedJson { get; set; } = "{}";
    public double EnergyKwh { get; set; }
    public double TemperatureC { get; set; }
    public double VoltageV { get; set; }
    public DateTimeOffset? InfluxWrittenAt { get; set; }
}
