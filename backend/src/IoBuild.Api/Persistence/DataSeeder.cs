using System.Text.Json;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Persistence;

/// <summary>
/// Provee la siembra inicial (Seed Data) idempotente para Planes de Suscripción y Dispositivos IoT,
/// respetando el enfoque "demo from zero" para el resto de entidades.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(IoBuildDbContext context, CancellationToken cancellationToken = default)
    {
        await SeedPlansAsync(context, cancellationToken);
        await SeedDevicesAsync(context, cancellationToken);
    }

    private static async Task SeedPlansAsync(IoBuildDbContext context, CancellationToken cancellationToken)
    {
        if (await context.Plans.AnyAsync(cancellationToken))
        {
            return;
        }

        var plans = new[]
        {
            new Plan(
                name: "Starter",
                description: "Perfect for small projects",
                price: 299m,
                interval: "monthly",
                featuresJson: JsonSerializer.Serialize(new[]
                {
                    "Up to 50 IoT devices",
                    "Basic dashboard",
                    "Email support",
                    "Updates included",
                    "1 administrator",
                    "Monthly reports"
                })
            ),
            new Plan(
                name: "Professional",
                description: "Ideal for medium-sized projects",
                price: 799m,
                interval: "monthly",
                featuresJson: JsonSerializer.Serialize(new[]
                {
                    "Up to 200 IoT devices",
                    "Advanced dashboard",
                    "24/7 priority support",
                    "Updates and new features",
                    "3 administrators",
                    "Real-time reports",
                    "Custom API",
                    "Training included"
                })
            ),
            new Plan(
                name: "Enterprise",
                description: "For big developments",
                price: 1299m,
                interval: "monthly",
                featuresJson: JsonSerializer.Serialize(new[]
                {
                    "Unlimited IoT devices",
                    "Enterprise dashboard",
                    "Dedicated 24/7 support",
                    "Development of custom features",
                    "Unlimited administrators",
                    "Advanced analytics",
                    "Complete API",
                    "Specialized consulting",
                    "Guaranteed SLA"
                })
            )
        };

        context.Plans.AddRange(plans);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDevicesAsync(IoBuildDbContext context, CancellationToken cancellationToken)
    {
        if (await context.Devices.AnyAsync(cancellationToken))
        {
            return;
        }

        var devices = new[]
        {
            // Proyecto 1
            new Device
            {
                Name = "Sensor de Temperatura - Torre A",
                Type = "SmartMeter",
                Location = "Torre A - Piso 5",
                MacAddress = "00:11:22:33:44:55",
                ProjectId = 1,
                Status = "Online",
                OwnerId = 1
            },
            new Device
            {
                Name = "Monitor de Humedad - Torre B",
                Type = "SmartLight",
                Location = "Torre B - Piso 8",
                MacAddress = "00:11:22:33:44:56",
                ProjectId = 1,
                Status = "Online",
                OwnerId = 1
            },
            new Device
            {
                Name = "Medidor de Energía - Áreas Comunes",
                Type = "SmokeDetector",
                Location = "Áreas Comunes - Gimnasio",
                MacAddress = "00:11:22:33:44:57",
                ProjectId = 1,
                Status = "Online",
                OwnerId = 1
            },
            new Device
            {
                Name = "Medidor de Agua - Torre A",
                Type = "WaterSensor",
                Location = "Torre A - Sistema Central",
                MacAddress = "00:11:22:33:44:60",
                ProjectId = 1,
                Status = "Online",
                OwnerId = 1
            },
            new Device
            {
                Name = "Control Climatización - Lobby",
                Type = "AirConditioner",
                Location = "Lobby Principal",
                MacAddress = "00:11:22:33:44:61",
                ProjectId = 1,
                Status = "Online",
                OwnerId = 1
            },

            // Proyecto 2
            new Device
            {
                Name = "Sensor de Temperatura - Torre 1",
                Type = "SmokeDetector",
                Location = "Torre 1 - Lobby Principal",
                MacAddress = "00:11:22:33:44:58",
                ProjectId = 2,
                Status = "Online",
                OwnerId = 1
            },
            new Device
            {
                Name = "Medidor de Agua - Torre 2",
                Type = "WaterSensor",
                Location = "Torre 2 - Sistema Central",
                MacAddress = "00:11:22:33:44:59",
                ProjectId = 2,
                Status = "Online",
                OwnerId = 1
            },
            new Device
            {
                Name = "Monitor de Energía - Piscina",
                Type = "SmartMeter",
                Location = "Área de Piscina - Terraza",
                MacAddress = "00:11:22:33:44:5A",
                ProjectId = 2,
                Status = "Online",
                OwnerId = 1
            },
            new Device
            {
                Name = "Control Iluminación - Entrada",
                Type = "SmartLight",
                Location = "Entrada Principal - Torre 1",
                MacAddress = "00:11:22:33:44:62",
                ProjectId = 2,
                Status = "Online",
                OwnerId = 1
            },
            new Device
            {
                Name = "Climatización - Áreas Comunes",
                Type = "AirConditioner",
                Location = "Áreas Comunes",
                MacAddress = "00:11:22:33:44:63",
                ProjectId = 2,
                Status = "Online",
                OwnerId = 1
            },

            // Proyecto 3
            new Device
            {
                Name = "Sensor de Construcción - Área 1",
                Type = "SmartMeter",
                Location = "Zona de Construcción - Sector A",
                MacAddress = "00:11:22:33:44:5B",
                ProjectId = 3,
                Status = "Online",
                OwnerId = 1
            },
            new Device
            {
                Name = "Monitor de Seguridad - Perímetro",
                Type = "SmokeDetector",
                Location = "Perímetro de Obra",
                MacAddress = "00:11:22:33:44:5C",
                ProjectId = 3,
                Status = "Online",
                OwnerId = 1
            }
        };

        context.Devices.AddRange(devices);
        await context.SaveChangesAsync(cancellationToken);
    }
}
