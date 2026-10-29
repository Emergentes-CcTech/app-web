using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Repositories;
using IoBuild.Api.Publishing.Domain.Services;
using IoBuild.Api.Publishing.Domain.Services.Commands;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Publishing.Application.Internal.CommandServices;

public class ClientCommandService : IClientCommandService
{
    private readonly IClientRepository _clientRepository;
    private readonly IoBuildDbContext _dbContext;

    public ClientCommandService(IClientRepository clientRepository, IoBuildDbContext dbContext)
    {
        _clientRepository = clientRepository;
        _dbContext = dbContext;
    }

    public async Task<int> Handle(CreateClientCommand command, CancellationToken ct = default)
    {
        string? resolvedUnitNumber = command.UnitNumber;
        if (command.UnitId.HasValue)
        {
            var unit = await _dbContext.Units.FirstOrDefaultAsync(u => u.Id == command.UnitId.Value, ct);
            if (unit is not null)
            {
                resolvedUnitNumber = unit.UnitNumber;
                if (!string.IsNullOrWhiteSpace(command.Email))
                {
                    var normalizedEmail = command.Email.Trim().ToLowerInvariant();
                    var user = await _dbContext.IamUsers.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
                    unit.AssignOwner(command.Email.Trim(), user?.Id);

                    if (user is not null)
                    {
                        var ownerProj = await _dbContext.UnitOwnerProjections.FirstOrDefaultAsync(p => p.UnitId == unit.Id, ct);
                        if (ownerProj is null)
                        {
                            _dbContext.UnitOwnerProjections.Add(new UnitOwnerProjection
                            {
                                UnitId = unit.Id,
                                OwnerUserId = user.Id,
                                UpdatedAt = DateTimeOffset.UtcNow
                            });
                        }
                        else
                        {
                            ownerProj.OwnerUserId = user.Id;
                            ownerProj.UpdatedAt = DateTimeOffset.UtcNow;
                        }

                        // Also synchronize devices in this unit
                        var unitDevices = await _dbContext.Devices.Where(d => d.UnitId == unit.Id).ToListAsync(ct);
                        foreach (var d in unitDevices)
                        {
                            d.OwnerId = user.Id;
                            var dp = await _dbContext.DeviceProjections.FirstOrDefaultAsync(p => p.DeviceId == d.Id, ct);
                            if (dp is not null)
                            {
                                dp.OwnerUserId = user.Id;
                                dp.LastEventAt = DateTime.UtcNow;
                            }
                        }
                    }

                    var unitProj = await _dbContext.UnitProjections.FirstOrDefaultAsync(p => p.UnitId == unit.Id, ct);
                    if (unitProj is not null)
                    {
                        unitProj.OwnerUserId = user?.Id;
                        unitProj.OwnerEmail = command.Email.Trim();
                        unitProj.Status = "occupied";
                        unitProj.LastEventAt = DateTime.UtcNow;
                    }
                }
            }
        }

        var client = new Client(
            command.FullName,
            command.ProjectName,
            command.AccountStatement,
            command.BuilderId,
            command.ProjectId,
            command.Email ?? string.Empty,
            command.PhoneNumber ?? string.Empty,
            command.Address ?? string.Empty,
            command.UnitId,
            resolvedUnitNumber);

        await _clientRepository.AddAsync(client, ct);
        await _dbContext.SaveChangesAsync(ct);
        return client.Id;
    }

    public async Task Handle(UpdateClientCommand command, CancellationToken ct = default)
    {
        var client = await _clientRepository.FindByIdAsync(command.Id, ct);
        if (client is null)
        {
            throw new KeyNotFoundException($"Client with ID {command.Id} not found.");
        }

        string? resolvedUnitNumber = command.UnitNumber;

        // If unit changed or unassigned, free previous unit if it belonged to this client
        if (client.UnitId.HasValue && client.UnitId != command.UnitId)
        {
            var oldUnit = await _dbContext.Units.FirstOrDefaultAsync(u => u.Id == client.UnitId.Value, ct);
            if (oldUnit is not null && string.Equals(oldUnit.OwnerEmail, client.Email, StringComparison.OrdinalIgnoreCase))
            {
                oldUnit.AssignOwner(null, null);
                var oldProj = await _dbContext.UnitOwnerProjections.FirstOrDefaultAsync(p => p.UnitId == oldUnit.Id, ct);
                if (oldProj is not null) _dbContext.UnitOwnerProjections.Remove(oldProj);

                var oldUnitProj = await _dbContext.UnitProjections.FirstOrDefaultAsync(p => p.UnitId == oldUnit.Id, ct);
                if (oldUnitProj is not null)
                {
                    oldUnitProj.OwnerUserId = null;
                    oldUnitProj.OwnerEmail = null;
                    oldUnitProj.Status = "available";
                    oldUnitProj.LastEventAt = DateTime.UtcNow;
                }
            }
        }

        if (command.UnitId.HasValue)
        {
            var unit = await _dbContext.Units.FirstOrDefaultAsync(u => u.Id == command.UnitId.Value, ct);
            if (unit is not null)
            {
                resolvedUnitNumber = unit.UnitNumber;
                if (!string.IsNullOrWhiteSpace(command.Email))
                {
                    var normalizedEmail = command.Email.Trim().ToLowerInvariant();
                    var user = await _dbContext.IamUsers.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
                    unit.AssignOwner(command.Email.Trim(), user?.Id);

                    if (user is not null)
                    {
                        var ownerProj = await _dbContext.UnitOwnerProjections.FirstOrDefaultAsync(p => p.UnitId == unit.Id, ct);
                        if (ownerProj is null)
                        {
                            _dbContext.UnitOwnerProjections.Add(new UnitOwnerProjection
                            {
                                UnitId = unit.Id,
                                OwnerUserId = user.Id,
                                UpdatedAt = DateTimeOffset.UtcNow
                            });
                        }
                        else
                        {
                            ownerProj.OwnerUserId = user.Id;
                            ownerProj.UpdatedAt = DateTimeOffset.UtcNow;
                        }

                        // Also synchronize devices in this unit
                        var unitDevices = await _dbContext.Devices.Where(d => d.UnitId == unit.Id).ToListAsync(ct);
                        foreach (var d in unitDevices)
                        {
                            d.OwnerId = user.Id;
                            var dp = await _dbContext.DeviceProjections.FirstOrDefaultAsync(p => p.DeviceId == d.Id, ct);
                            if (dp is not null)
                            {
                                dp.OwnerUserId = user.Id;
                                dp.LastEventAt = DateTime.UtcNow;
                            }
                        }
                    }

                    var unitProj = await _dbContext.UnitProjections.FirstOrDefaultAsync(p => p.UnitId == unit.Id, ct);
                    if (unitProj is not null)
                    {
                        unitProj.OwnerUserId = user?.Id;
                        unitProj.OwnerEmail = command.Email.Trim();
                        unitProj.Status = "occupied";
                        unitProj.LastEventAt = DateTime.UtcNow;
                    }
                }
            }
        }

        client.Update(
            command.FullName,
            command.ProjectName,
            command.AccountStatement,
            command.BuilderId,
            command.ProjectId,
            command.Email ?? string.Empty,
            command.PhoneNumber ?? string.Empty,
            command.Address ?? string.Empty,
            command.UnitId,
            resolvedUnitNumber);

        await _clientRepository.UpdateAsync(client, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task Handle(DeleteClientCommand command, CancellationToken ct = default)
    {
        var client = await _clientRepository.FindByIdAsync(command.Id, ct);
        if (client is null)
        {
            throw new KeyNotFoundException($"Client with ID {command.Id} not found.");
        }

        if (client.UnitId.HasValue)
        {
            var unit = await _dbContext.Units.FirstOrDefaultAsync(u => u.Id == client.UnitId.Value, ct);
            if (unit is not null && string.Equals(unit.OwnerEmail, client.Email, StringComparison.OrdinalIgnoreCase))
            {
                unit.AssignOwner(null, null);
                var oldProj = await _dbContext.UnitOwnerProjections.FirstOrDefaultAsync(p => p.UnitId == unit.Id, ct);
                if (oldProj is not null) _dbContext.UnitOwnerProjections.Remove(oldProj);

                var oldUnitProj = await _dbContext.UnitProjections.FirstOrDefaultAsync(p => p.UnitId == unit.Id, ct);
                if (oldUnitProj is not null)
                {
                    oldUnitProj.OwnerUserId = null;
                    oldUnitProj.OwnerEmail = null;
                    oldUnitProj.Status = "available";
                    oldUnitProj.LastEventAt = DateTime.UtcNow;
                }
            }
        }

        await _clientRepository.DeleteAsync(client, ct);
        await _dbContext.SaveChangesAsync(ct);
    }
}
