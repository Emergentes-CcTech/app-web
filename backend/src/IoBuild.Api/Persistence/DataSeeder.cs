using System.Text.Json;
using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.IAM.Domain.Model.Aggregates;
using IoBuild.Api.Persistence;
using IoBuild.Api.Profiles.Domain.Model.Aggregates;
using IoBuild.Api.Proration.Domain.Model.Aggregates;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using IoBuild.Api.Publishing.Domain.Model.ValueObjects;
using IoBuild.Api.Subscriptions.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Persistence;

/// <summary>
/// Siembra de datos inicial (Seed Data) idempotente para la plataforma IoBuild.
/// Modela exclusivamente áreas comunes, condominios residenciales y prorrateo legal (Ley N.° 27157).
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(IoBuildDbContext context, CancellationToken cancellationToken = default)
    {
        await SeedPlansAsync(context, cancellationToken);
        await SeedUsersAndCondominiumAsync(context, cancellationToken);
        await SeedSubscriptionIfMissingAsync(context, cancellationToken);
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

    private static async Task SeedUsersAndCondominiumAsync(IoBuildDbContext context, CancellationToken cancellationToken)
    {
        if (await context.Projects.AnyAsync(cancellationToken))
        {
            return;
        }

        // 1. Usuarios del sistema: Administrador y Residente
        var adminPasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!");
        var residentPasswordHash = BCrypt.Net.BCrypt.HashPassword("Resident123!");

        var adminUser = new IamUser
        {
            Email = "mveramendi@iobuild.pe",
            PasswordHash = adminPasswordHash,
            Role = "PropertyManager"
        };
        var residentUser = new IamUser
        {
            Email = "cflores@gmail.com",
            PasswordHash = residentPasswordHash,
            Role = "Resident"
        };

        context.IamUsers.AddRange(adminUser, residentUser);
        await context.SaveChangesAsync(cancellationToken);

        // Perfiles de usuario
        var adminProfile = new Profile
        {
            UserId = adminUser.Id,
            Name = "Miguel Veramendi",
            Username = "mveramendi",
            PhoneNumber = "+51 987654321",
            Address = "Av. Javier Prado Este 456, San Isidro",
            YearsInBusiness = 8
        };
        var residentProfile = new Profile
        {
            UserId = residentUser.Id,
            Name = "Carla Flores",
            Username = "cflores",
            PhoneNumber = "+51 912345678",
            Address = "Av. Arnaldo Márquez 1234, Dpto. 402, Jesús María"
        };

        context.Profiles.AddRange(adminProfile, residentProfile);
        await context.SaveChangesAsync(cancellationToken);

        // 2. Condominio Residencial Los Robles (Jesús María)
        var project = new Project
        {
            Name = "Residencial Los Robles",
            Description = "Condominio residencial de 25 departamentos en Jesús María monitoreado integralmente con IoBuild IoT.",
            Address = "Av. Arnaldo Márquez 1234",
            District = "Jesús María",
            TotalFloors = 5,
            TotalUnits = 25,
            SedapalSupplyCode = "SED-8899221",
            LuzDelSurSupplyCode = "LDS-4433112",
            CisternCapacityM3 = 45.0m,
            Status = "Activo",
            AdministratorId = adminUser.Id
        };

        context.Projects.Add(project);
        await context.SaveChangesAsync(cancellationToken);

        // Suscripción HaaS activa vinculada al condominio y al administrador
        var urbanoPlan = await context.Plans.FirstOrDefaultAsync(p => p.Name == "Urbano", cancellationToken)
            ?? await context.Plans.FirstAsync(cancellationToken);
        var initialSubscription = new Subscription
        {
            ProjectId = project.Id,
            BuilderId = adminUser.Id,
            PlanId = urbanoPlan.Id,
            Status = "active",
            StartDate = DateTimeOffset.UtcNow.AddMonths(-1),
            EndDate = DateTimeOffset.UtcNow.AddMonths(11)
        };
        context.Subscriptions.Add(initialSubscription);
        await context.SaveChangesAsync(cancellationToken);

        // 3. Áreas Comunes Técnicas
        var commonAreas = new[]
        {
            new CommonArea(project.Id, "Cisterna Principal de Agua Potable", CommonAreaSubsystem.MainCistern, "Sótano 1 - Cisterna", "Almacenamiento de 45 m3 de agua potable con boya telemétrica"),
            new CommonArea(project.Id, "Cuarto de Bombas Hidroneumáticas", CommonAreaSubsystem.PressurePumpsRoom, "Sótano 1 - Bombas", "Sistema de 2 bombas de 7.5 HP de presión constante"),
            new CommonArea(project.Id, "Banco de Ascensores Principales", CommonAreaSubsystem.ElevatorsBank, "Torre Central - Azotea", "2 ascensores electromecánicos con regeneración de energía"),
            new CommonArea(project.Id, "Tablero Eléctrico General e Iluminación Común", CommonAreaSubsystem.GeneralElectricalPanel, "Sótano 1 - Tableros", "Tablero matriz de distribución de iluminación y servicios comunes")
        };
        context.CommonAreas.AddRange(commonAreas);
        await context.SaveChangesAsync(cancellationToken);

        // 4. 25 Departamentos con Invariante Estricto de Alícuota al 100.0000% (Ley N.° 27157)
        // Dpto 402: 3.8500% vinculado a Carla Flores
        // Restantes 24 departamentos: 23 de 4.0062% y 1 de 4.0074% (Suma = 92.1426 + 4.0074 + 3.8500 = 100.0000%)
        var units = new List<Unit>();
        int unitCount = 0;

        for (int floor = 1; floor <= 5; floor++)
        {
            for (int u = 1; u <= 5; u++)
            {
                unitCount++;
                var unitNumber = $"{floor}0{u}";
                decimal aliquot;
                string residentName;
                string residentEmail;
                int? ownerId = null;

                if (unitNumber == "402")
                {
                    aliquot = 3.8500m;
                    residentName = "Carla Flores";
                    residentEmail = "cflores@gmail.com";
                    ownerId = residentUser.Id;
                }
                else if (unitCount == 25)
                {
                    aliquot = 4.0074m;
                    residentName = $"Propietario Dpto. {unitNumber}";
                    residentEmail = $"residente.{unitNumber}@losrobles.pe";
                }
                else
                {
                    aliquot = 4.0062m;
                    residentName = $"Propietario Dpto. {unitNumber}";
                    residentEmail = $"residente.{unitNumber}@losrobles.pe";
                }

                units.Add(new Unit
                {
                    ProjectId = project.Id,
                    Floor = floor,
                    FloorNumber = floor,
                    RoomNumber = unitNumber,
                    UnitNumber = unitNumber,
                    AreaM2 = 78.50m,
                    AliquotPercentage = aliquot,
                    ResidentFullName = residentName,
                    ResidentEmail = residentEmail,
                    OwnerEmail = residentEmail,
                    OwnerId = ownerId,
                    AccountStatus = "Al día"
                });
            }
        }

        context.Units.AddRange(units);
        await context.SaveChangesAsync(cancellationToken);

        // 5. Los 6 Dispositivos IoT Comunales (Cero dispositivos en departamentos privados)
        var devices = new[]
        {
            new Device
            {
                Name = "GW-SOT-01",
                Type = "GatewayHub",
                Subsystem = "MainCistern",
                MacAddress = "AA:BB:CC:11:00:01",
                ProjectId = project.Id,
                LocationArea = "Sótano 1 - Cisterna",
                Status = "Online",
                LastHeartbeatUtc = DateTime.UtcNow
            },
            new Device
            {
                Name = "WFS-CIS-IN",
                Type = "WaterFlowSensor",
                Subsystem = "MainCistern",
                MacAddress = "AA:BB:CC:11:00:02",
                ProjectId = project.Id,
                LocationArea = "Sótano 1 - Tubería Matriz Cisterna",
                NightFlowThresholdM3H = 0.15m,
                LastCumulativeReading = 420.50,
                Status = "Online",
                LastHeartbeatUtc = DateTime.UtcNow
            },
            new Device
            {
                Name = "WFS-CIS-OUT",
                Type = "WaterFlowSensor",
                Subsystem = "MainCistern",
                MacAddress = "AA:BB:CC:11:00:03",
                ProjectId = project.Id,
                LocationArea = "Sótano 1 - Impulsión Hidroneumática",
                NightFlowThresholdM3H = 0.15m,
                LastCumulativeReading = 412.10,
                Status = "Online",
                LastHeartbeatUtc = DateTime.UtcNow
            },
            new Device
            {
                Name = "CT-BOMB-01",
                Type = "CurrentTransformerCT",
                Subsystem = "PressurePumpsRoom",
                MacAddress = "AA:BB:CC:11:00:04",
                ProjectId = project.Id,
                LocationArea = "Sótano 1 - Tablero de Bombas",
                LastCumulativeReading = 1250.00,
                Status = "Online",
                LastHeartbeatUtc = DateTime.UtcNow
            },
            new Device
            {
                Name = "CT-ASC-01",
                Type = "CurrentTransformerCT",
                Subsystem = "ElevatorsBank",
                MacAddress = "AA:BB:CC:11:00:05",
                ProjectId = project.Id,
                LocationArea = "Azotea - Tablero de Ascensores",
                LastCumulativeReading = 890.30,
                Status = "Online",
                LastHeartbeatUtc = DateTime.UtcNow
            },
            new Device
            {
                Name = "CT-TAB-GRAL",
                Type = "CurrentTransformerCT",
                Subsystem = "GeneralElectricalPanel",
                MacAddress = "AA:BB:CC:11:00:06",
                ProjectId = project.Id,
                LocationArea = "Sótano 1 - Tablero Eléctrico General",
                LastCumulativeReading = 2150.80,
                Status = "Online",
                LastHeartbeatUtc = DateTime.UtcNow
            }
        };

        context.Devices.AddRange(devices);
        await context.SaveChangesAsync(cancellationToken);

        // 6. Telemetría y Alerta Temprana en Analytics (Fuga Nocturna en Cisterna)
        var leakTimestamp = DateTime.UtcNow.Date.AddHours(7).AddMinutes(15); // 02:15 AM hora local Perú (UTC-5)
        context.ConsumptionRecords.AddRange(
            new ConsumptionRecord
            {
                ProjectId = project.Id,
                DeviceId = devices[1].Id,
                Subsystem = "MainCistern",
                MetricType = "WaterM3",
                Value = 420.50,
                TimestampUtc = leakTimestamp
            },
            new ConsumptionRecord
            {
                ProjectId = project.Id,
                DeviceId = devices[3].Id,
                Subsystem = "PressurePumpsRoom",
                MetricType = "EnergyKwh",
                Value = 1250.00,
                TimestampUtc = leakTimestamp
            }
        );

        context.AnomalyAlerts.Add(new AnomalyAlert
        {
            ProjectId = project.Id,
            DeviceId = devices[1].Id,
            Subsystem = "MainCistern",
            AlertType = "NocturnalWaterLeak",
            Severity = "Critical",
            Title = "Fuga Nocturna en Cisterna Principal",
            Description = "Se detectó flujo continuo de 0.28 m3/h entre las 01:00 y 04:00 AM (umbral 0.15 m3/h). Posible fuga en válvula de llenado o cisterna con fisura.",
            EstimatedOvercostSoles = 145.00m,
            DetectedAtUtc = leakTimestamp,
            Status = "Active"
        });

        // 7. Periodo Conciliado y Liquidado de Octubre 2026 en Proration
        // Sedapal: S/ 1,680.00 (420.00 m3) | Luz del Sur: S/ 1,160.50 (1,450.00 kWh) | Total: S/ 2,840.50
        var period = new BillingPeriod
        {
            ProjectId = project.Id,
            PeriodCode = "2026-10",
            StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 11, 15, 23, 59, 59, DateTimeKind.Utc),
            MacroWaterM3 = 420.00m,
            MacroWaterAmountSoles = 1680.00m,
            WaterReceiptImageUrl = "https://storage.iobuild.pe/receipts/sedapal-oct2026.pdf",
            MacroEnergyKwh = 1450.00m,
            MacroEnergyAmountSoles = 1160.50m,
            EnergyReceiptImageUrl = "https://storage.iobuild.pe/receipts/luzdelsur-oct2026.pdf",
            MeasuredCommonWaterM3 = 420.50m,
            MeasuredCommonEnergyKwh = 1450.00m,
            WaterLossM3 = 0.00m,
            EnergyLossKwh = 0.00m,
            OtherCommonExpensesSoles = 0.00m,
            TotalCommonExpenseSoles = 2840.50m,
            TotalBilledSoles = 2840.50m,
            TotalCollectedSoles = 0.00m,
            DelinquencyRate = 0.00m,
            Status = "Settled",
            ReconciledAtUtc = DateTime.UtcNow.AddDays(-2),
            SettledAtUtc = DateTime.UtcNow.AddDays(-1)
        };

        context.BillingPeriods.Add(period);
        await context.SaveChangesAsync(cancellationToken);

        // Genera estados de cuenta individuales con cuadre al céntimo
        var waterCentsTotal = (long)(period.MacroWaterAmountSoles * 100m);
        var energyCentsTotal = (long)(period.MacroEnergyAmountSoles * 100m);

        var statements = new List<StatementOfAccount>();
        foreach (var u in units)
        {
            var indWater = Math.Round(period.MacroWaterAmountSoles * (u.AliquotPercentage / 100.0000m), 2);
            var indEnergy = Math.Round(period.MacroEnergyAmountSoles * (u.AliquotPercentage / 100.0000m), 2);
            var indTotal = indWater + indEnergy;

            statements.Add(new StatementOfAccount
            {
                BillingPeriodId = period.Id,
                ProjectId = project.Id,
                UnitId = u.Id,
                UnitNumber = u.UnitNumber,
                ResidentFullName = u.ResidentFullName ?? $"Residente Dpto. {u.UnitNumber}",
                ResidentEmail = u.ResidentEmail ?? string.Empty,
                AliquotPercentage = u.AliquotPercentage,
                IndividualWaterSoles = indWater,
                IndividualEnergySoles = indEnergy,
                IndividualOtherExpensesSoles = 0.00m,
                IndividualAmountSoles = indTotal,
                PaidAmountSoles = 0.00m,
                BalanceSoles = indTotal,
                Status = "Pending"
            });
        }

        // Ajuste de céntimos al departamento con mayor alícuota
        var sumDiff = period.TotalCommonExpenseSoles - statements.Sum(s => s.IndividualAmountSoles);
        if (sumDiff != 0.00m)
        {
            var top = statements.OrderByDescending(s => s.AliquotPercentage).First();
            top.IndividualAmountSoles += sumDiff;
            top.BalanceSoles += sumDiff;
        }

        context.StatementOfAccounts.AddRange(statements);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSubscriptionIfMissingAsync(IoBuildDbContext context, CancellationToken cancellationToken)
    {
        if (await context.Subscriptions.AnyAsync(cancellationToken))
        {
            return;
        }

        var project = await context.Projects.FirstOrDefaultAsync(cancellationToken);
        if (project is null) return;

        var plan = await context.Plans.FirstOrDefaultAsync(p => p.Name == "Urbano", cancellationToken)
            ?? await context.Plans.FirstOrDefaultAsync(cancellationToken);
        if (plan is null) return;

        var subscription = new Subscription
        {
            ProjectId = project.Id,
            BuilderId = project.AdministratorId > 0 ? project.AdministratorId : project.Id,
            PlanId = plan.Id,
            Status = "active",
            StartDate = DateTimeOffset.UtcNow.AddMonths(-1),
            EndDate = DateTimeOffset.UtcNow.AddMonths(11)
        };

        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync(cancellationToken);
    }
}
