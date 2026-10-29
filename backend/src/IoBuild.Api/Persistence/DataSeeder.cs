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
            // Plan Boutique: para condominios pequeños de hasta 20 unidades con 4 equipos IoT
            new Plan(
                name: "Boutique",
                description: "Plan HaaS para condominios boutique de hasta 20 departamentos. Dotación en comodato: 4 equipos IoT.",
                price: 500.00m,
                interval: "monthly",
                featuresJson: JsonSerializer.Serialize(new[]
                {
                    "Hasta 20 departamentos",
                    "4 equipos IoT en comodato (1 GatewayHub, 1 WaterFlowSensor, 2 CurrentTransformerCT)",
                    "Detección continua de fugas nocturnas (01:00 a 04:00 AM)",
                    "Monitoreo de Cisterna Principal y Tableros Eléctricos",
                    "Prorrateo mensual automatizado según Alícuota Legal (Ley 27157)",
                    "Contrato de 12 meses + 2 meses de garantía de servicio",
                    "Soporte técnico prioritario y reemplazo de hardware por falla"
                })
            ),
            // Plan Urbano: para edificios medianos de 21 a 80 unidades con 6 equipos IoT
            new Plan(
                name: "Urbano",
                description: "Plan HaaS para condominios urbanos de 21 a 80 departamentos. Dotación en comodato: 6 equipos IoT.",
                price: 700.00m,
                interval: "monthly",
                featuresJson: JsonSerializer.Serialize(new[]
                {
                    "De 21 a 80 departamentos",
                    "6 equipos IoT en comodato (1 GatewayHub, 2 WaterFlowSensor, 3 CurrentTransformerCT)",
                    "Monitoreo integral: Cisterna, Bombas, Ascensores e Iluminación Común",
                    "Detección de sobrecargas eléctricas y fugas nocturnas en cisterna",
                    "Conciliación mensual contra recibos matriz oficiales (Sedapal y Luz del Sur)",
                    "Prorrateo legal al céntimo y liquidación digital por departamento",
                    "Contrato de 12 meses + 2 meses de garantía de servicio",
                    "Acceso BFF para Administrador y App Web para Residentes"
                })
            ),
            // Plan GranTorre: para macro-condominios de más de 80 unidades con 10 equipos IoT
            new Plan(
                name: "GranTorre",
                description: "Plan HaaS para macro-condominios de más de 80 departamentos. Dotación en comodato: 10 equipos IoT.",
                price: 1000.00m,
                interval: "monthly",
                featuresJson: JsonSerializer.Serialize(new[]
                {
                    "Más de 80 departamentos",
                    "10 equipos IoT en comodato (2 GatewayHub, 3 WaterFlowSensor, 5 CurrentTransformerCT)",
                    "Monitoreo extendido de múltiples tableros, ascensores y cuartos hidroneumáticos",
                    "Detección avanzada de anomalías y conciliación de macro-medición",
                    "Prorrateo por alícuotas con cuadre estricto al 100.0000%",
                    "Contrato de 12 meses + 2 meses de garantía de servicio",
                    "Mantenimiento preventivo presencial y calibración periódica de sensores"
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
