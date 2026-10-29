using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using IoBuild.Api.Publishing.Domain.Model.ValueObjects;
using IoBuild.Api.Publishing.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Publishing.Application.Internal.CommandServices;

/// <summary>
/// Servicio de aplicación para la gestión de Condominios (Project) en el contexto de Catastro (Cadastre).
/// Implementa la creación de condominios, el aprovisionamiento de áreas comunes y la definición
/// de estructura de unidades bajo el Invariante Sagrado de la Ley N.° 27157 (Suma de alícuotas = 100.0000%).
/// </summary>
public sealed class ProjectCommandService(IoBuildDbContext dbContext) : IProjectCommandService
{
    // Registra un nuevo condominio residencial en el padrón de catastro
    public async Task<Project> CreateProjectAsync(string name, string description, string location, int totalUnits, int builderId, string? imageUrl, CancellationToken cancellationToken = default)
    {
        var project = new Project
        {
            Name = name,
            Description = description,
            Location = location,
            Address = location,
            TotalUnits = totalUnits,
            AdministratorId = builderId,
            BuilderId = builderId,
            ImageUrl = imageUrl,
            Status = "Activo"
        };
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Aprovisiona automáticamente los 4 subsistemas de áreas comunes centrales del condominio
        var commonAreas = new List<CommonArea>
        {
            new(project.Id, "Cisterna Principal de Agua Potable", CommonAreaSubsystem.MainCistern, "Sótano 2", "Tanque principal de almacenamiento con control de nivel y caudal"),
            new(project.Id, "Cuarto de Bombas Hidroneumáticas", CommonAreaSubsystem.PressurePumpsRoom, "Sótano 2", "Bombas de impulsión de agua a presión constante para todos los pisos"),
            new(project.Id, "Banco Central de Ascensores", CommonAreaSubsystem.ElevatorsBank, "Núcleo Central", "Ascensores de pasajeros para acceso a departamentos y áreas comunes"),
            new(project.Id, "Tablero Eléctrico General e Iluminación Común", CommonAreaSubsystem.GeneralElectricalPanel, "Sótano 1", "Distribución trifásica para alumbrado comunal, pasadizos y equipos de emergencia")
        };
        await dbContext.CommonAreas.AddRangeAsync(commonAreas, cancellationToken);

        // Sincroniza la proyección para consultas de analítica
        dbContext.ProjectProjections.Add(new IoBuild.Api.Analytics.Domain.Model.Aggregates.ProjectProjection
        {
            ProjectId = project.Id,
            BuilderUserId = builderId,
            Name = project.Name,
            Status = "Active",
            LastEventAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return project;
    }

    // Valida el Invariante Sagrado de la Ley N.° 27157: la suma de alícuotas debe ser exactamente 100.0000%
    public static bool ValidateAliquotSum(IEnumerable<decimal> aliquots, out string errorMessage)
    {
        decimal sum = aliquots.Sum();
        // Margen de tolerancia legal máximo de 0.0001
        if (Math.Abs(sum - 100.0000m) > 0.0001m)
        {
            errorMessage = $"La suma de alícuotas del condominio debe ser exactamente 100.0000% (Ley N.° 27157). La suma actual es {sum:F4}%.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    // Define la estructura de pisos y departamentos del condominio garantizando el 100.0000% de alícuota
    public async Task DefineProjectStructureAsync(int projectId, int floors, int unitsPerFloor, List<int>? floorNumbers, CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects.FindAsync([projectId], cancellationToken);
        if (project is null) throw new KeyNotFoundException($"Condominio con identificador {projectId} no encontrado.");
        if (project.StructureDefined) throw new InvalidOperationException("La estructura del condominio ya ha sido definida previamente.");

        var targetFloors = floorNumbers is { Count: > 0 }
            ? floorNumbers.Distinct().Where(f => f >= 1 && f <= floors).ToList()
            : Enumerable.Range(1, floors).ToList();

        int totalUnits = targetFloors.Count * unitsPerFloor;
        if (totalUnits <= 0) throw new ArgumentException("El número de departamentos debe ser mayor a cero.");

        project.StructureDefined = true;
        project.TotalUnits = totalUnits;
        project.TotalFloors = targetFloors.Count;

        // Distribuye matemáticamente la alícuota del 100.0000% entre todas las unidades con cuadre exacto al céntimo
        decimal baseAliquot = Math.Round(100.0000m / totalUnits, 4);
        decimal accumulated = 0m;

        // Aprovisiona los departamentos sin dispositivos domóticos privados
        var units = new List<Unit>();
        for (int i = 0; i < targetFloors.Count; i++)
        {
            int floor = targetFloors[i];
            for (int u = 1; u <= unitsPerFloor; u++)
            {
                var unitNumber = $"{floor}{u:D2}";
                int unitIndex = (i * unitsPerFloor) + u;
                
                // Asigna la alícuota base o ajusta en la última unidad para cuadre exacto al 100.0000%
                decimal aliquot = (unitIndex == totalUnits) ? (100.0000m - accumulated) : baseAliquot;
                accumulated += aliquot;

                var unit = new Unit(projectId, unitNumber, floor, areaM2: 75.0m, aliquotPercentage: aliquot)
                {
                    RoomNumber = $"{u:D2}"
                };
                units.Add(unit);
            }
        }

        // Valida el invariante antes de persistir
        if (!ValidateAliquotSum(units.Select(u => u.AliquotPercentage), out var aliquotError))
        {
            throw new InvalidOperationException(aliquotError);
        }

        await dbContext.Units.AddRangeAsync(units, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Sincroniza las unidades en las proyecciones de analítica
        foreach (var unit in units)
        {
            dbContext.UnitProjections.Add(new IoBuild.Api.Analytics.Domain.Model.Aggregates.UnitProjection
            {
                UnitId = unit.Id,
                ProjectId = projectId,
                BuilderUserId = project.AdministratorId,
                Status = "Available",
                Floor = unit.Floor,
                RoomNumber = unit.RoomNumber,
                LastEventAt = DateTime.UtcNow
            });
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
