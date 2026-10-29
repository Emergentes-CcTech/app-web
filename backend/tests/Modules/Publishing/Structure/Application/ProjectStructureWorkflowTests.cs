using IoBuild.Api.Publishing.Application.Internal.CommandServices;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Publishing.Structure.Application;

[Trait("Context", "Publishing")]
[Trait("Capability", "Structure")]
[Trait("Layer", "Application")]
[Trait("Dependency", "InMemory")]
public sealed class ProjectStructureWorkflowTests
{
    [Fact]
    [Trait("Category", "DDD")]
    public async Task DefineProjectStructureAsync_provisions_units_and_validates_100_percent_aliquot_invariant()
    {
        await using var db = CreateDb();
        var project = new Project { Id = 1, Name = "Residencial Los Robles", Description = "Edificio de Departamentos", Location = "Jesús María", TotalUnits = 10, BuilderId = 5 };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var service = new ProjectCommandService(db);
        await service.DefineProjectStructureAsync(1, floors: 2, unitsPerFloor: 3, floorNumbers: null);

        var updatedProject = await db.Projects.FindAsync(1);
        Assert.NotNull(updatedProject);
        Assert.True(updatedProject!.StructureDefined);

        // 2 pisos * 3 departamentos = 6 unidades creadas
        var units = await db.Units.Where(u => u.ProjectId == 1).OrderBy(u => u.UnitNumber).ToListAsync();
        Assert.Equal(6, units.Count);

        // Invariante Sagrado (Ley N.° 27157): La suma de alícuotas debe ser exactamente 100.0000%
        decimal totalAliquot = units.Sum(u => u.AliquotPercentage);
        Assert.Equal(100.0000m, totalAliquot);

        // Regla de Oro: Se eliminó cualquier dispositivo domótico privado (UnitId en Device = 0)
        var privateUnitDevices = await db.Devices.Where(d => d.ProjectId == 1 && d.UnitId != null).ToListAsync();
        Assert.Empty(privateUnitDevices);
    }
}
