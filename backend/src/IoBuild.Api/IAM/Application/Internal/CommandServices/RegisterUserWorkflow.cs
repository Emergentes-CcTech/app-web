using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Devices.Domain.Model.Entities;
using IoBuild.Api.IAM.Domain.Model.Aggregates;
using IoBuild.Api.IAM.Domain.Model.Commands;
using IoBuild.Api.IAM.Infrastructure.Hashing;
using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using IoBuild.Api.Workflows;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace IoBuild.Api.IAM.Application.Internal.CommandServices;

/// <summary>
/// Excepción lanzada cuando un usuario con rol de Residente/Propietario intenta registrarse sin tener una unidad asignada.
/// </summary>
public sealed class OwnerUnitAssignmentRequiredException : InvalidOperationException
{
    public OwnerUnitAssignmentRequiredException()
        : base("An Owner/Resident account requires an assigned unit.") { }
}

/// <summary>
/// Flujo de trabajo para el registro de nuevos usuarios en el contexto IAM.
/// Soporta los roles PropertyManager (B2B) y Resident (B2C), ejecutando la auto-vinculación con Cadastre.
/// </summary>
public sealed class RegisterUserWorkflow(
    IoBuildDbContext dbContext,
    PasswordHasher passwordHasher,
    IIntegrationDispatchQueue queue,
    WorkflowExecutor workflowExecutor) : IWorkflow<RegisterUser, int>
{
    private const int MinimumPasswordLength = 8;
    private static readonly Regex RegistrationEmailPattern = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}\z",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Registra al usuario y dispara la auto-vinculación con el departamento y alícuota en Cadastre
    public Task<int> ExecuteAsync(RegisterUser request, CancellationToken cancellationToken = default) =>
        workflowExecutor.ExecuteAsync(async cancellationToken =>
        {
            var rawEmail = request.Email?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rawEmail) || rawEmail.Length > 320 || !RegistrationEmailPattern.IsMatch(rawEmail))
                throw new InvalidOperationException("Invalid registration data.");
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < MinimumPasswordLength)
                throw new InvalidOperationException("Invalid registration data.");

            // Canonicaliza los roles admitidos: PropertyManager (B2B) y Resident (B2C), con alias retrocompatibles
            var canonicalRole = request.Role?.Trim() switch
            {
                var role when string.Equals(role, "PropertyManager", StringComparison.OrdinalIgnoreCase) => "PropertyManager",
                var role when string.Equals(role, "Builder", StringComparison.OrdinalIgnoreCase) => "Builder",
                var role when string.Equals(role, "Resident", StringComparison.OrdinalIgnoreCase) => "Resident",
                var role when string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase) => "Owner",
                _ => throw new InvalidOperationException("Invalid registration data.")
            };

            var email = rawEmail.ToLowerInvariant();
            var existing = await dbContext.IamUsers.SingleOrDefaultAsync(user => user.Email == email, cancellationToken);
            if (existing is not null) return 0;

            // Valida que los residentes tengan asignada previamente una unidad en el condominio
            var isResident = string.Equals(canonicalRole, "Resident", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(canonicalRole, "Owner", StringComparison.OrdinalIgnoreCase);

            if (isResident && !await HasAssignedUnitAsync(email, cancellationToken))
                throw new OwnerUnitAssignmentRequiredException();

            var newUser = new IamUser { Email = email, PasswordHash = passwordHasher.Hash(request.Password), Role = canonicalRole };
            dbContext.IamUsers.Add(newUser);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException mysql && mysql.Number == 1062)
            {
                dbContext.Entry(newUser).State = EntityState.Detached;
                return 0;
            }

            // Auto-vinculación con unidades en el contexto de Catastro (Cadastre)
            var matchingUnits = await dbContext.Units
                .Where(u => u.OwnerEmail != null && u.OwnerEmail.ToLower() == email)
                .ToListAsync(cancellationToken);

            var clientUnitIds = await dbContext.Clients
                .Where(c => !string.IsNullOrEmpty(c.Email) && c.Email.ToLower() == email && c.UnitId != null)
                .Select(c => c.UnitId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (clientUnitIds.Count > 0)
            {
                var existingUnitIds = matchingUnits.Select(u => u.Id).ToHashSet();
                var additionalUnits = await dbContext.Units
                    .Where(u => clientUnitIds.Contains(u.Id) && !existingUnitIds.Contains(u.Id))
                    .ToListAsync(cancellationToken);
                matchingUnits.AddRange(additionalUnits);
            }

            foreach (var unit in matchingUnits)
            {
                unit.OwnerId = newUser.Id;
                unit.ResidentEmail = email;
                unit.OwnerEmail = email;
                unit.Status = "occupied";
                unit.AccountStatus = "Al día";

                var ownerProj = await dbContext.UnitOwnerProjections
                    .FirstOrDefaultAsync(p => p.UnitId == unit.Id, cancellationToken);
                if (ownerProj is null)
                {
                    dbContext.UnitOwnerProjections.Add(new UnitOwnerProjection
                    {
                        UnitId = unit.Id,
                        OwnerUserId = newUser.Id,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                }
                else
                {
                    ownerProj.OwnerUserId = newUser.Id;
                    ownerProj.UpdatedAt = DateTimeOffset.UtcNow;
                }

                if (!await dbContext.ProjectProjections.AnyAsync(p => p.ProjectId == unit.ProjectId, cancellationToken))
                {
                    var proj = await dbContext.Projects.FindAsync([unit.ProjectId], cancellationToken);
                    if (proj is not null)
                    {
                        dbContext.ProjectProjections.Add(new ProjectProjection
                        {
                            ProjectId = proj.Id,
                            BuilderUserId = proj.BuilderId,
                            Name = proj.Name,
                            Status = "OnGoing",
                            LastEventAt = DateTime.UtcNow
                        });
                    }
                }

                var unitProj = await dbContext.UnitProjections
                    .FirstOrDefaultAsync(p => p.UnitId == unit.Id, cancellationToken);
                if (unitProj is null)
                {
                    var proj = await dbContext.Projects.FindAsync([unit.ProjectId], cancellationToken);
                    dbContext.UnitProjections.Add(new UnitProjection
                    {
                        UnitId = unit.Id,
                        ProjectId = unit.ProjectId,
                        BuilderUserId = proj?.BuilderId ?? 0,
                        OwnerUserId = newUser.Id,
                        OwnerEmail = email,
                        Status = "occupied",
                        Floor = unit.Floor,
                        RoomNumber = unit.RoomNumber,
                        LastEventAt = DateTime.UtcNow
                    });
                }
                else
                {
                    unitProj.OwnerUserId = newUser.Id;
                    unitProj.OwnerEmail = email;
                    unitProj.Status = "occupied";
                    unitProj.LastEventAt = DateTime.UtcNow;
                }

                // Sincroniza retrocompatiblemente proyecciones de dispositivos si existen en pruebas
                var unitDevices = await dbContext.Devices
                    .Where(d => d.UnitId == unit.Id)
                    .ToListAsync(cancellationToken);

                foreach (var device in unitDevices)
                {
                    device.OwnerId = newUser.Id;

                    var devProj = await dbContext.DeviceProjections
                        .FirstOrDefaultAsync(dp => dp.DeviceId == device.Id, cancellationToken);
                    if (devProj is null)
                    {
                        dbContext.DeviceProjections.Add(new DeviceProjection
                        {
                            DeviceId = device.Id,
                            ProjectId = device.ProjectId,
                            UnitId = device.UnitId,
                            DeviceName = device.Name,
                            DeviceType = device.Type,
                            Status = device.Status,
                            OwnerUserId = newUser.Id,
                            LastEventAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        devProj.OwnerUserId = newUser.Id;
                        devProj.Status = device.Status;
                        devProj.LastEventAt = DateTime.UtcNow;
                    }
                }
            }

            // Sincroniza registros de clientes asociados si existen
            var matchingClients = await dbContext.Clients
                .Where(c => !string.IsNullOrEmpty(c.Email) && c.Email.ToLower() == email)
                .ToListAsync(cancellationToken);

            foreach (var client in matchingClients)
            {
                if (!client.UnitId.HasValue && matchingUnits.Count > 0)
                {
                    var primaryUnit = matchingUnits.First();
                    client.UnitId = primaryUnit.Id;
                    client.UnitNumber = primaryUnit.UnitNumber;
                }
            }

            // Aprovisiona el perfil si no existe para permitir operaciones inmediatas
            if (!await dbContext.Profiles.AnyAsync(p => p.UserId == newUser.Id, cancellationToken))
            {
                var primaryUnit = matchingUnits.FirstOrDefault();
                dbContext.Profiles.Add(new Profiles.Domain.Model.Aggregates.Profile
                {
                    UserId = newUser.Id,
                    Name = primaryUnit?.ResidentFullName ?? email.Split('@')[0],
                    Username = email.Split('@')[0],
                    Address = primaryUnit != null ? $"Dpto {primaryUnit.UnitNumber}" : null
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            await queue.EnqueueAsync(new DispatchRequest("iam", "domain-event", $"iam-user:{email}", 1, $"{{\"email\":\"{email}\",\"role\":\"{canonicalRole}\"}}", $"iam.user-registered:{email}"), cancellationToken);
            return newUser.Id;
        }, cancellationToken);

    // Consulta si existe una unidad privativa previamente asignada al correo en Cadastre
    private Task<bool> HasAssignedUnitAsync(string email, CancellationToken cancellationToken) =>
        dbContext.Units.AnyAsync(unit =>
            (!string.IsNullOrEmpty(unit.OwnerEmail) && unit.OwnerEmail.ToLower() == email) ||
            dbContext.Clients.Any(client =>
                !string.IsNullOrEmpty(client.Email) &&
                client.Email.ToLower() == email &&
                client.UnitId == unit.Id),
            cancellationToken);
}
