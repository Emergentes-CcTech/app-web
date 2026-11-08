using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using IoBuild.Api.Publishing.Domain.Repositories;
using IoBuild.Api.Publishing.Domain.Services;
using IoBuild.Api.Publishing.Domain.Services.Commands;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Publishing.Application.Internal.CommandServices;

/// <summary>
/// Servicio de aplicación para la gestión y asignación de departamentos (Unit).
/// Gestiona el registro de unidades, la asignación de residentes y la auto-vinculación con IAM.
/// </summary>
public class UnitCommandService : IUnitCommandService
{
    private readonly IUnitRepository _unitRepository;
    private readonly IoBuildDbContext _dbContext;

    public UnitCommandService(IUnitRepository unitRepository, IoBuildDbContext dbContext)
    {
        _unitRepository = unitRepository;
        _dbContext = dbContext;
    }

    // Registra una nueva unidad privativa en el condominio
    public async Task<int> Handle(CreateUnitCommand command, CancellationToken ct = default)
    {
        var unit = new Unit(command.ProjectId, command.UnitNumber, command.Floor, areaM2: 75.0m, aliquotPercentage: 0.0m)
        {
            RoomNumber = command.RoomNumber,
            OwnerId = command.OwnerId
        };
        await _unitRepository.AddAsync(unit, ct);
        await _dbContext.SaveChangesAsync(ct);
        return unit.Id;
    }

    // Asigna el correo de residente a una unidad privativa y dispara la sincronización con IAM
    public async Task Handle(AssignUnitOwnerEmailCommand command, CancellationToken ct = default)
    {
        var unit = await _unitRepository.FindByIdAsync(command.UnitId, ct);
        if (unit is null)
        {
            throw new KeyNotFoundException($"Departamento con ID {command.UnitId} no encontrado.");
        }

        // Intenta vincular con una cuenta IAM de residente si ya existe registrada
        var user = await _dbContext.IamUsers.FirstOrDefaultAsync(u => u.Email == command.OwnerEmail, ct);
        var resolvedOwnerId = command.OwnerId ?? user?.Id;

        // Asigna el residente en la unidad privativa
        unit.AssignResident(command.OwnerEmail, fullName: null, iamUserId: resolvedOwnerId);
        await _unitRepository.UpdateAsync(unit, ct);

        // Sincroniza la proyección de vinculación de residente
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
        }
        else if (existingProjection is not null)
        {
            _dbContext.UnitOwnerProjections.Remove(existingProjection);
        }

        // Sincroniza la proyección analítica de la unidad
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
                BuilderUserId = p?.AdministratorId ?? 0,
                OwnerUserId = resolvedOwnerId,
                OwnerEmail = string.IsNullOrWhiteSpace(command.OwnerEmail) ? null : command.OwnerEmail.Trim(),
                Status = unit.Status,
                Floor = unit.Floor,
                RoomNumber = unit.RoomNumber,
                LastEventAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
