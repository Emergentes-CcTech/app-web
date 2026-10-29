using IoBuild.Api.Analytics.Domain.Services;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Analytics.Infrastructure.Adapters;

/// <summary>
/// Adaptador simulado determinístico para el servicio cognitivo de análisis de consumo.
/// Construye diagnósticos basados en datos reales de telemetría, series temporales (ConsumptionRecord) y prorrateo legal (Ley N.° 27157).
/// </summary>
public sealed class MockAiCognitiveAdapter(IoBuildDbContext db) : IAiCognitiveService
{
    public async Task<AiExplanationDto> ExplainConsumptionVariationAsync(int projectId, int? unitId, string billingPeriod, string question)
    {
        var project = await db.Projects.FindAsync([projectId]);
        var projectName = project?.Name ?? $"Condominio #{projectId}";

        // Determina el rango de fechas para el periodo mensual de facturación (ej. "2026-10")
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(billingPeriod))
        {
            var parts = billingPeriod.Split('-');
            if (parts.Length >= 2 && int.TryParse(parts[0], out var year) && int.TryParse(parts[1], out var month))
            {
                startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
                endDate = startDate.AddMonths(1).AddTicks(-1);
            }
        }

        // Consulta de series temporales de consumo en áreas comunes del periodo
        var consumptionRecords = await db.ConsumptionRecords
            .Where(r => r.ProjectId == projectId && r.TimestampUtc >= startDate && r.TimestampUtc <= endDate)
            .ToListAsync();

        var totalWaterM3 = consumptionRecords
            .Where(r => r.MetricType == "WaterM3" || r.Subsystem == "MainCistern")
            .Select(r => r.Value)
            .DefaultIfEmpty(0.0)
            .Max(); // Lectura acumulada máxima del periodo

        var totalEnergyKwh = consumptionRecords
            .Where(r => r.MetricType == "EnergyKwh" || r.Subsystem == "PressurePumpsRoom" || r.Subsystem == "ElevatorsBank" || r.Subsystem == "GeneralElectricalPanel")
            .Select(r => r.Value)
            .DefaultIfEmpty(0.0)
            .Max();

        // Consulta de alertas de anomalías activas del condominio registradas en el periodo
        var periodAlerts = await db.AnomalyAlerts
            .Where(alert => alert.ProjectId == projectId && alert.DetectedAtUtc >= startDate && alert.DetectedAtUtc <= endDate)
            .OrderByDescending(alert => alert.DetectedAtUtc)
            .ToListAsync();

        // Consulta del umbral nocturno configurado para los caudalímetros del condominio
        var waterSensor = await db.Devices.FirstOrDefaultAsync(d => d.ProjectId == projectId && d.Type == "WaterFlowSensor");
        var nightThreshold = waterSensor?.NightFlowThresholdM3H ?? 0.1500m;

        // Filtra únicamente anomalías activas para el cómputo de sobrecostos no resueltos
        var waterLeakAlerts = periodAlerts.Where(a => a.AlertType == "NocturnalWaterLeak" && a.Status == "Active").ToList();
        var overloadAlerts = periodAlerts.Where(a => a.AlertType == "PumpElectricalOverload" && a.Status == "Active").ToList();

        var waterLeakOvercost = waterLeakAlerts.Sum(a => a.EstimatedOvercostSoles);
        var overloadOvercost = overloadAlerts.Sum(a => a.EstimatedOvercostSoles);
        var totalOvercost = waterLeakOvercost + overloadOvercost;

        var hasNightLeak = waterLeakAlerts.Count > 0;
        var hasOverload = overloadAlerts.Count > 0;
        var hasActiveAnomalies = periodAlerts.Any(a => a.Status == "Active");

        var unitContext = string.Empty;
        var aliquotImpact = "El costo de las áreas comunes se distribuye entre los departamentos según su alícuota legal (Ley N.° 27157).";

        if (unitId.HasValue)
        {
            var unit = await db.Units.FindAsync([unitId.Value]);
            if (unit is not null && unit.ProjectId == projectId)
            {
                unitContext = $" para el Departamento {unit.UnitNumber} (Alícuota: {unit.AliquotPercentage:F4}%)";
                var unitOvercost = totalOvercost * (unit.AliquotPercentage / 100m);
                aliquotImpact = $"Para su departamento ({unit.UnitNumber}), su alícuota legal de {unit.AliquotPercentage:F4}% representa un impacto estimado de S/ {unitOvercost:F2} sobre el sobrecosto comunal reportado este mes.";
            }
        }

        var summary = $"Análisis de consumo para {projectName}{unitContext} correspondiente al periodo {billingPeriod}. " +
            (hasNightLeak ? "Se identificó flujo nocturno anómalo en cisterna durante la madrugada." : "Consumo hídrico y eléctrico comunal dentro de parámetros operativos normales.");

        var waterAnalysis = hasNightLeak
            ? $"Se identificó fuga nocturna continua (> {nightThreshold:F2} m3/h) en el subsistema de Cisterna entre 01:00 y 04:00 AM, generando un sobrecosto estimado acumulado de S/ {waterLeakOvercost:F2}."
            : $"El caudalímetro no invasivo de la cisterna reportó estabilidad sin superar el umbral nocturno de {nightThreshold:F2} m3/h (01:00 a 04:00 AM). Consumo hídrico acumulado: {totalWaterM3:F2} m3.";

        var energyAnalysis = hasOverload
            ? $"El transformador de corriente CT en el Cuarto de Bombas Hidroneumáticas registró sobrecargas eléctricas superiores al 25% sobre la línea base, con sobrecosto estimado de S/ {overloadOvercost:F2}."
            : $"Los tableros generales, banco de ascensores e iluminación mantuvieron consumos estables dentro de la línea base esperada. Consumo eléctrico acumulado: {totalEnergyKwh:F2} kWh.";

        var recommendations = new List<string>();
        if (hasNightLeak)
        {
            recommendations.Add("Inspeccionar boyas y válvulas de llenado de la cisterna principal en el sótano.");
            recommendations.Add("Revisar posibles filtraciones en las líneas matrices de impulsión hacia la red común.");
        }
        else
        {
            recommendations.Add("Mantener la calibración y lectura periódica del caudalímetro en cisterna.");
        }

        if (hasOverload)
        {
            recommendations.Add("Programar mantenimiento mecánico y eléctrico a las bombas hidroneumáticas.");
        }
        else
        {
            recommendations.Add("Continuar con el régimen de alternancia automática de bombas hidroneumáticas.");
        }

        return new AiExplanationDto(
            Summary: summary,
            WaterAnalysis: waterAnalysis,
            EnergyAnalysis: energyAnalysis,
            AliquotImpact: aliquotImpact,
            Recommendations: recommendations,
            HasAnomalies: hasActiveAnomalies);
    }
}
