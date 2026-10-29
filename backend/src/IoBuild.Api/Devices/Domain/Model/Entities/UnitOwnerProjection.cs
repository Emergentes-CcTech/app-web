namespace IoBuild.Api.Devices.Domain.Model.Entities;

public sealed class UnitOwnerProjection
{
    public int UnitId { get; set; }
    public int OwnerUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
