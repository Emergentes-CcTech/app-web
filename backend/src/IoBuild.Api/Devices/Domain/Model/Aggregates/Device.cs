namespace IoBuild.Api.Devices.Domain.Model.Aggregates;

/// <summary>
/// Devices BC aggregate root.
/// Physical location: Devices/Domain/Model/Aggregates/Device.cs
/// Namespace preserved as IoBuild.Api.Persistence for compatibility.
/// </summary>
public sealed class Device
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? MacAddress { get; set; }
    public int ProjectId { get; set; }
    public int? UnitId { get; set; }
    public int OwnerId { get; set; }
    public string? Source { get; set; }
    public string Status { get; set; } = "unknown";
}
