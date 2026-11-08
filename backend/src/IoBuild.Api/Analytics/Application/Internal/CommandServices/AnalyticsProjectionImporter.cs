using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Analytics.Application.Internal.CommandServices;

public sealed class AnalyticsProjectionImporter
{
    private readonly IoBuildDbContext _db;

    public AnalyticsProjectionImporter(IoBuildDbContext db) => _db = db;

    public async Task UpsertDeviceAsync(DeviceProjection incoming, CancellationToken ct = default)
    {
        if (incoming.ProjectId.HasValue)
        {
            var exists = await _db.ProjectProjections.AnyAsync(p => p.ProjectId == incoming.ProjectId.Value, ct);
            if (!exists) incoming.ProjectId = null;
        }
        if (incoming.UnitId.HasValue)
        {
            var exists = await _db.UnitProjections.AnyAsync(u => u.UnitId == incoming.UnitId.Value, ct);
            if (!exists) incoming.UnitId = null;
        }

        var row = await _db.DeviceProjections.FindAsync([incoming.DeviceId], ct);
        if (row is null)
        {
            _db.DeviceProjections.Add(incoming);
            await _db.SaveChangesAsync(ct);
            return;
        }
        if (incoming.LastEventAt < row.LastEventAt) return;
        row.OwnerUserId = incoming.OwnerUserId;
        row.ProjectId = incoming.ProjectId;
        row.UnitId = incoming.UnitId;
        row.DeviceType = incoming.DeviceType;
        row.Status = incoming.Status;
        row.FloorNumber = incoming.FloorNumber;
        row.DeviceName = incoming.DeviceName;
        row.LastEventAt = incoming.LastEventAt;
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpsertProjectAsync(ProjectProjection incoming, CancellationToken ct = default)
    {
        var row = await _db.ProjectProjections.FindAsync([incoming.ProjectId], ct);
        if (row is null)
        {
            _db.ProjectProjections.Add(incoming);
            await _db.SaveChangesAsync(ct);
            return;
        }
        if (incoming.LastEventAt < row.LastEventAt) return;
        row.BuilderUserId = incoming.BuilderUserId;
        row.Name = incoming.Name;
        row.Status = incoming.Status;
        row.LastEventAt = incoming.LastEventAt;
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpsertUnitAsync(UnitProjection incoming, CancellationToken ct = default)
    {
        var row = await _db.UnitProjections.FindAsync([incoming.UnitId], ct);
        if (row is null)
        {
            _db.UnitProjections.Add(incoming);
            await _db.SaveChangesAsync(ct);
            return;
        }
        if (incoming.LastEventAt < row.LastEventAt) return;
        row.ProjectId = incoming.ProjectId;
        row.BuilderUserId = incoming.BuilderUserId;
        if (incoming.OwnerUserId.HasValue) row.OwnerUserId = incoming.OwnerUserId;
        row.Status = incoming.Status;
        row.Floor = incoming.Floor;
        row.RoomNumber = incoming.RoomNumber;
        row.OwnerEmail = incoming.OwnerEmail;
        row.LastEventAt = incoming.LastEventAt;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ImportAsync(IEnumerable<ProjectProjection> projects, IEnumerable<UnitProjection> units, IEnumerable<DeviceProjection> devices, CancellationToken ct = default)
    {
        foreach (var p in projects) await UpsertProjectAsync(p, ct);
        foreach (var u in units) await UpsertUnitAsync(u, ct);
        foreach (var d in devices) await UpsertDeviceAsync(d, ct);
    }

    public async Task ApplyUnitOwnerMatchedAsync(int unitId, int projectId, int ownerUserId, string ownerEmail, DateTime occurredOn, CancellationToken ct = default)
    {
        var row = await _db.UnitProjections.FindAsync([unitId], ct);
        if (row is null)
        {
            row = new UnitProjection
            {
                UnitId = unitId,
                ProjectId = projectId,
                BuilderUserId = 0,
                Status = "Occupied",
                OwnerUserId = ownerUserId,
                OwnerEmail = ownerEmail,
                LastEventAt = occurredOn
            };
            _db.UnitProjections.Add(row);
            await _db.SaveChangesAsync(ct);
            return;
        }
        if (occurredOn < row.LastEventAt) return;
        row.OwnerUserId = ownerUserId;
        if (!string.IsNullOrEmpty(ownerEmail)) row.OwnerEmail = ownerEmail;
        row.Status = "Occupied";
        row.LastEventAt = occurredOn;
        await _db.SaveChangesAsync(ct);
    }
}
