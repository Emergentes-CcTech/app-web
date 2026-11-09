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

    public async Task<IReadOnlyList<Device>> ProvisionAsync(int projectId, CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return [];
        var plan = new Plan { Name = Plan.NameForUnits(project.TotalUnits) };
        var areas = await db.CommonAreas.Where(a => a.ProjectId == projectId).ToListAsync(ct);
        var existing = await db.Devices.Where(d => d.ProjectId == projectId && d.UnitId == null).ToListAsync(ct);
        var ownerId = project.AdministratorId;

        var added = new List<Device>();
        void AddMissing(string type, int allowance, CommonAreaSubsystem[] preferred, CommonAreaSubsystem fallback, string prefix, string label)
        {
            // Se instalan primero en las áreas comunes que el condominio registró; si no tiene ninguna del tipo, en el lugar por defecto
            var targets = preferred.Where(s => areas.Any(a => a.Subsystem == s)).DefaultIfEmpty(fallback).ToArray();
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

        AddMissing("GatewayHub", plan.GatewayHubAllowance, [CommonAreaSubsystem.MainCistern], CommonAreaSubsystem.MainCistern, "GW", "Gateway del edificio");
        AddMissing("WaterFlowSensor", plan.WaterFlowSensorAllowance, WaterPoints, CommonAreaSubsystem.MainCistern, "AGUA", "Punto de agua");
        AddMissing("CurrentTransformerCT", plan.CurrentTransformerAllowance, EnergyPanels, CommonAreaSubsystem.GeneralElectricalPanel, "ENERGIA", "Tablero de energía");
        if (added.Count == 0) return [];

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
