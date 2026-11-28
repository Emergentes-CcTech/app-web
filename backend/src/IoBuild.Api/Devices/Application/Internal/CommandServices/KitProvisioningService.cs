using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Model.ValueObjects;
using IoBuild.Api.Subscriptions.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Devices.Application.Internal.CommandServices;

/// <summary>
/// Entrega el kit HaaS del plan que corresponde al condominio: un gateway, los puntos de agua y los tableros de energía.
/// En la demo no hay equipos físicos: los equipos se registran solos y el simulador empieza a enviar lecturas al anunciarlos.
/// Solo agrega lo que falta; nunca quita equipos (un plan menor no retira el kit instalado).
/// </summary>
public sealed class KitProvisioningService(IoBuildDbContext db, DeviceRegistryService registry)
{
    public const string KitSource = "HaaSKit";

    // Orden de instalación: el agua se mide en la cisterna y en la impulsión; la energía en bombas, ascensores y tablero general
    private static readonly CommonAreaSubsystem[] WaterPoints = [CommonAreaSubsystem.MainCistern, CommonAreaSubsystem.PressurePumpsRoom];
    private static readonly CommonAreaSubsystem[] EnergyPanels = [CommonAreaSubsystem.PressurePumpsRoom, CommonAreaSubsystem.ElevatorsBank, CommonAreaSubsystem.GeneralElectricalPanel];

    // Dónde instala el kit de cada plan cuando el condominio no registró áreas: las áreas monitoreadas salen de aquí.
    // Esencial mide el agua de la cisterna y el tablero general (el que cuadra con el recibo de luz)
    public static (CommonAreaSubsystem[] Water, CommonAreaSubsystem[] Energy) KitPlacement(string planName) => planName switch
    {
        "Esencial" => ([CommonAreaSubsystem.MainCistern], [CommonAreaSubsystem.GeneralElectricalPanel]),
        "Urbano" => ([CommonAreaSubsystem.MainCistern, CommonAreaSubsystem.PressurePumpsRoom], [CommonAreaSubsystem.PressurePumpsRoom, CommonAreaSubsystem.ElevatorsBank]),
        _ => ([CommonAreaSubsystem.MainCistern, CommonAreaSubsystem.PressurePumpsRoom, CommonAreaSubsystem.MainCistern],
              [CommonAreaSubsystem.PressurePumpsRoom, CommonAreaSubsystem.ElevatorsBank, CommonAreaSubsystem.GeneralElectricalPanel])
    };

    public static string DefaultAreaName(CommonAreaSubsystem subsystem) => subsystem switch
    {
        CommonAreaSubsystem.MainCistern => "Cisterna",
        CommonAreaSubsystem.PressurePumpsRoom => "Cuarto de bombas",
        CommonAreaSubsystem.ElevatorsBank => "Ascensores",
        _ => "Tablero general"
    };

    // Un equipo instalado en un subsistema sin área registrada crea el área: lo monitoreado siempre se ve en el condominio
    public async Task EnsureAreasAsync(int projectId, IEnumerable<string> subsystems, CancellationToken ct = default)
    {
        var existing = await db.CommonAreas.Where(a => a.ProjectId == projectId).Select(a => a.Subsystem).ToListAsync(ct);
        foreach (var name in subsystems.Distinct())
        {
            if (!Enum.TryParse<CommonAreaSubsystem>(name, out var subsystem) || existing.Contains(subsystem)) continue;
            db.CommonAreas.Add(new IoBuild.Api.Publishing.Domain.Model.Aggregates.CommonArea(projectId, DefaultAreaName(subsystem), subsystem, string.Empty));
            existing.Add(subsystem);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Device>> ProvisionAsync(int projectId, CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return [];
        var plan = new Plan { Name = Plan.NameForUnits(project.TotalUnits) };
        var areas = await db.CommonAreas.Where(a => a.ProjectId == projectId).ToListAsync(ct);
        var existing = await db.Devices.Where(d => d.ProjectId == projectId && d.UnitId == null).ToListAsync(ct);
        var ownerId = project.AdministratorId;

        var added = new List<Device>();
        var placement = KitPlacement(plan.Name);
        void AddMissing(string type, int allowance, CommonAreaSubsystem[] preferred, CommonAreaSubsystem[] planDefault, string prefix, string label)
        {
            // Con áreas registradas (condominios anteriores) se instala en ellas; si no, donde lo ubica el kit del plan
            var registered = preferred.Where(s => areas.Any(a => a.Subsystem == s)).ToArray();
            var targets = registered.Length > 0 ? registered : planDefault;
            for (var i = existing.Count(d => d.Type == type) + added.Count(d => d.Type == type); i < allowance; i++)
            {
                var subsystem = targets[i % targets.Length];
                var area = areas.FirstOrDefault(a => a.Subsystem == subsystem);
                var location = area is null ? $"{label} {i + 1}" : $"{area.Name} · {label}";
                added.Add(new Device
                {
                    Name = $"{prefix}-{i + 1:00}",
                    Type = type,
                    Subsystem = subsystem.ToString(),
                    LocationArea = location,
                    MacAddress = KitMac(projectId, type, i),
                    ProjectId = projectId,
                    NightFlowThresholdM3H = 0.15m,
                    Source = KitSource,
                    Status = "Online",
                    // Instalado ahora: tiene el margen normal hasta su primer latido antes de alertarse como sin señal
                    LastHeartbeatUtc = DateTime.UtcNow,
                    OwnerId = ownerId
                });
            }
        }

                AddMissing("GatewayHub", plan.GatewayHubAllowance, [CommonAreaSubsystem.MainCistern], [CommonAreaSubsystem.MainCistern], "GW", "Gateway del edificio");
        AddMissing("WaterFlowSensor", plan.WaterFlowSensorAllowance, WaterPoints, placement.Water, "AGUA", "Punto de agua");
        AddMissing("CurrentTransformerCT", plan.CurrentTransformerAllowance, EnergyPanels, placement.Energy, "ENERGIA", "Tablero de energía");
        if (added.Count == 0) return [];

        // Las áreas monitoreadas son las del kit: se registran las que aún no existan
        await EnsureAreasAsync(projectId, added.Where(d => d.Type != "GatewayHub").Select(d => d.Subsystem), ct);
        db.Devices.AddRange(added);
        await db.SaveChangesAsync(ct);
        db.DeviceProjections.AddRange(added.Select(d => new DeviceProjection
        {
            DeviceId = d.Id,
            ProjectId = d.ProjectId,
            DeviceName = d.Name,
            DeviceType = d.Type,
            Status = d.Status,
            OwnerUserId = ownerId,
            LastEventAt = DateTime.UtcNow
        }));
        await db.SaveChangesAsync(ct);
        foreach (var device in added) await registry.AnnounceAsync(device, ct);
        return added;
    }

    // MAC administrada localmente (02:...): única por condominio, tipo y posición dentro del kit
    private static string KitMac(int projectId, string type, int index)
    {
        var kind = type switch { "GatewayHub" => 1, "WaterFlowSensor" => 2, _ => 3 };
        return $"02:1B:{(projectId >> 8) & 0xFF:X2}:{projectId & 0xFF:X2}:{kind:X2}:{index & 0xFF:X2}";
    }
}
