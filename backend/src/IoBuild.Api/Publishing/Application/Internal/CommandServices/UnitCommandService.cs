using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Repositories;
using IoBuild.Api.Publishing.Domain.Services;
using IoBuild.Api.Publishing.Domain.Services.Commands;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Publishing.Application.Internal.CommandServices;

public class UnitCommandService : IUnitCommandService
{
    private readonly IUnitRepository _unitRepository;
    private readonly IoBuildDbContext _dbContext;

    public UnitCommandService(IUnitRepository unitRepository, IoBuildDbContext dbContext)
    {
        _unitRepository = unitRepository;
        _dbContext = dbContext;
    }

    public async Task<int> Handle(CreateUnitCommand command, CancellationToken ct = default)
    {
        var unit = new Unit(command.ProjectId, command.UnitNumber, command.OwnerId, command.Floor, command.RoomNumber);
        await _unitRepository.AddAsync(unit, ct);
        await _dbContext.SaveChangesAsync(ct);
        return unit.Id;
    }

    public async Task Handle(AssignUnitOwnerEmailCommand command, CancellationToken ct = default)
    {
        var unit = await _unitRepository.FindByIdAsync(command.UnitId, ct);
        if (unit is null)
        {
            throw new KeyNotFoundException($"Unit with ID {command.UnitId} not found.");
        }

        // Try to match owner email with an existing IamUser
        var user = await _dbContext.IamUsers.FirstOrDefaultAsync(u => u.Email == command.OwnerEmail, ct);
        var resolvedOwnerId = command.OwnerId ?? user?.Id;

        unit.AssignOwner(command.OwnerEmail, resolvedOwnerId);
        await _unitRepository.UpdateAsync(unit, ct);

        // Synchronize with UnitOwnerProjections for instant device actuation authorization
        var existingProjection = await _dbContext.UnitOwnerProjections
            .FirstOrDefaultAsync(p => p.UnitId == unit.Id, ct);

        if (resolvedOwnerId.HasValue && !string.IsNullOrWhiteSpace(command.OwnerEmail))
        {
            if (existingProjection is null)
            {
                _dbContext.UnitOwnerProjections.Add(new UnitOwnerProjection
                {
                    UnitId = unit.Id,
                    OwnerUserId = resolvedOwnerId.Value,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                existingProjection.OwnerUserId = resolvedOwnerId.Value;
                existingProjection.UpdatedAt = DateTimeOffset.UtcNow;
            }

            // Also synchronize devices in this unit to the assigned owner
            var unitDevices = await _dbContext.Devices.Where(d => d.UnitId == unit.Id).ToListAsync(ct);
            foreach (var d in unitDevices)
            {
                d.OwnerId = resolvedOwnerId.Value;
                var dp = await _dbContext.DeviceProjections.FirstOrDefaultAsync(p => p.DeviceId == d.Id, ct);
                if (dp is not null)
                {
                    dp.OwnerUserId = resolvedOwnerId.Value;
                    dp.LastEventAt = DateTime.UtcNow;
                }
            }
        }
        else if (existingProjection is not null)
        {
            _dbContext.UnitOwnerProjections.Remove(existingProjection);
        }

        // Synchronize with UnitProjections
        var existingUnitProj = await _dbContext.UnitProjections
            .FirstOrDefaultAsync(p => p.UnitId == unit.Id, ct);
        if (existingUnitProj is not null)
        {
            existingUnitProj.OwnerUserId = resolvedOwnerId;
            existingUnitProj.OwnerEmail = string.IsNullOrWhiteSpace(command.OwnerEmail) ? null : command.OwnerEmail.Trim();
            existingUnitProj.Status = unit.Status;
            existingUnitProj.LastEventAt = DateTime.UtcNow;
        }
        else
        {
            var p = await _dbContext.Projects.FindAsync([unit.ProjectId], ct);
            _dbContext.UnitProjections.Add(new IoBuild.Api.Analytics.Domain.Model.Aggregates.UnitProjection
            {
                UnitId = unit.Id,
                ProjectId = unit.ProjectId,
                BuilderUserId = p?.BuilderId ?? 0,
                OwnerUserId = resolvedOwnerId,
                OwnerEmail = string.IsNullOrWhiteSpace(command.OwnerEmail) ? null : command.OwnerEmail.Trim(),
                Status = unit.Status,
                Floor = unit.Floor,
                RoomNumber = unit.RoomNumber,
                LastEventAt = DateTime.UtcNow
            });
        }

        // Synchronize with Clients in this project
        if (!string.IsNullOrWhiteSpace(command.OwnerEmail))
        {
            var normalized = command.OwnerEmail.Trim().ToLowerInvariant();
            var matchingClient = await _dbContext.Clients
                .FirstOrDefaultAsync(c => c.ProjectId == unit.ProjectId && c.Email.ToLower() == normalized, ct);
            if (matchingClient is not null)
            {
                matchingClient.UnitId = unit.Id;
                matchingClient.UnitNumber = unit.UnitNumber;
            }
        }
        else
        {
            var prevClient = await _dbContext.Clients
                .FirstOrDefaultAsync(c => c.ProjectId == unit.ProjectId && c.UnitId == unit.Id, ct);
            if (prevClient is not null)
            {
                prevClient.UnitId = null;
                prevClient.UnitNumber = null;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
