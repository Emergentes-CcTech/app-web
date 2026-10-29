using System.Text.Json;
using System.Text.Json.Nodes;
using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Devices.Domain.Model.Catalog;
using IoBuild.Api.Devices.Domain.Model.Entities;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Devices.Application.Internal.CommandServices;

/// <summary>
/// Mensaje de telemetría emitido por un dispositivo IoT comunal (Gateway, Caudalímetro, Transformador CT).
/// </summary>
public sealed record TelemetryMessage(
    int DeviceId,
    string EventId,
    DateTimeOffset OccurredAt,
    string Status,
    string ReportedJson,
    double EnergyKwh,
    double TemperatureC = 0,
    double VoltageV = 0,
    string? MacAddress = null,
    double? CumulativeValue = null);

public interface IInfluxTelemetrySink { Task WriteAsync(TelemetryMessage message, CancellationToken cancellationToken = default); }
public interface IDeviceMqttPublisher { Task PublishAsync(string topic, string payload, bool qos1, bool retain, CancellationToken cancellationToken = default); }

public sealed class DisabledInfluxTelemetrySink : IInfluxTelemetrySink
{
    public Task WriteAsync(TelemetryMessage message, CancellationToken cancellationToken = default) => throw new HttpRequestException("InfluxDB is not configured.");
}

public sealed class DisabledDeviceMqttPublisher : IDeviceMqttPublisher
{
    public Task PublishAsync(string topic, string payload, bool qos1, bool retain, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
/// Servicio de aplicación para la ingesta de telemetría IoT con validación de idempotencia y monotonicidad.
/// </summary>
public sealed class DeviceTelemetryService(IoBuildDbContext db, IInfluxTelemetrySink influx, IoBuild.Api.Analytics.Domain.Services.IAnomalyDetectionEngine? anomalyEngine = null)
{
    // Procesa e ingesta una lectura de telemetría asegurando aislamiento por dispositivo
    public async Task<bool> IngestAsync(TelemetryMessage message, CancellationToken cancellationToken = default)
    {
        using var lease = await DeviceStateLocks.EnterAsync(message.DeviceId, cancellationToken);
        return await IngestLockedAsync(message, cancellationToken);
    }

    private async Task<bool> IngestLockedAsync(TelemetryMessage message, CancellationToken cancellationToken)
    {
        var device = await db.Devices.FindAsync([message.DeviceId], cancellationToken);
        if (device is null) return false;

        // Regla de Ingesta 1: Idempotencia por EventId o por tupla (MacAddress, TimestampUtc)
        // Permite la recepción segura de lotes reenviados desde el buffer local de 72h tras reconexión
        var mac = message.MacAddress ?? device.MacAddress;
        var existing = await db.DeviceTelemetry.FirstOrDefaultAsync(item =>
            item.EventId == message.EventId ||
            (!string.IsNullOrEmpty(mac) && item.DeviceId == message.DeviceId && item.OccurredAt == message.OccurredAt), cancellationToken);

        if (existing is not null)
        {
            if (existing.InfluxWrittenAt is null)
            {
                if (!await db.TelemetryRecoveries.AnyAsync(item => item.EventId == existing.EventId, cancellationToken))
                {
                    db.TelemetryRecoveries.Add(new TelemetryRecovery { EventId = existing.EventId, CreatedAt = DateTimeOffset.UtcNow });
                    await db.SaveChangesAsync(cancellationToken);
                }
                try { await ReplayExistingAsync(existing, cancellationToken); }
                catch (HttpRequestException) { }
            }
            // Registro ya procesado: idempotencia garantizada sin duplicar
            return true;
        }

        // Regla de Ingesta 2: Monotonicidad en lecturas acumuladas (CumulativeValue, cumulative_m3 o cumulative_kwh)
        double? incomingCumulative = message.CumulativeValue;
        if (!incomingCumulative.HasValue && !string.IsNullOrEmpty(message.ReportedJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(message.ReportedJson);
                if (doc.RootElement.TryGetProperty("cumulative_m3", out var m3Prop)) incomingCumulative = m3Prop.GetDouble();
                else if (doc.RootElement.TryGetProperty("cumulative_kwh", out var kwhProp)) incomingCumulative = kwhProp.GetDouble();
                else if (doc.RootElement.TryGetProperty("cumulative_value", out var valProp)) incomingCumulative = valProp.GetDouble();
            }
            catch { }
        }

        if (incomingCumulative.HasValue && incomingCumulative.Value > 0.0)
        {
            var isChronologicallyNewer = !device.LastHeartbeatUtc.HasValue || message.OccurredAt.UtcDateTime >= device.LastHeartbeatUtc.Value;
            if (isChronologicallyNewer)
            {
                if (device.LastCumulativeReading > 0.0 && incomingCumulative.Value < device.LastCumulativeReading)
                {
                    // Rechaza paquetes corruptos con lecturas acumuladas decrecientes
                    throw new InvalidOperationException($"Lectura no monotónica rechazada para dispositivo {device.Id}: lectura recibida {incomingCumulative.Value} < acumulada {device.LastCumulativeReading}.");
                }
                device.LastCumulativeReading = incomingCumulative.Value;
            }
        }

        // Evaluación previa en el motor determinístico de anomalías (antes de actualizar LastHeartbeatUtc)
        if (anomalyEngine is not null)
        {
            try
            {
                if (device.Type == "GatewayHub")
                {
                    // Evalúa el estado del concentrador antes de sobreescribir el último latido
                    await anomalyEngine.EvaluateGatewayHeartbeatAsync(device, message.OccurredAt, TimeSpan.FromMinutes(15), cancellationToken);
                }
                else if (device.Type == "WaterFlowSensor")
                {
                    double flowRate = 0.0;
                    if (!string.IsNullOrEmpty(message.ReportedJson))
                    {
                        using var doc = JsonDocument.Parse(message.ReportedJson);
                        if (doc.RootElement.TryGetProperty("flow_m3h", out var fProp)) flowRate = fProp.GetDouble();
                        else if (doc.RootElement.TryGetProperty("flow_rate", out var frProp)) flowRate = frProp.GetDouble();
                        else if (doc.RootElement.TryGetProperty("flow_rate_m3h", out var frmProp)) flowRate = frmProp.GetDouble();
                    }
                    if (flowRate > 0.0)
                    {
                        await anomalyEngine.EvaluateWaterFlowAsync(device, flowRate, message.OccurredAt, cancellationToken);
                    }
                }
                else if (device.Subsystem == "PressurePumpsRoom" && message.EnergyKwh > 0.0)
                {
                    // Deriva la línea base promedio histórica a partir de ConsumptionRecords de este dispositivo
                    var historicalBaseline = await db.ConsumptionRecords
                        .Where(r => r.DeviceId == device.Id && r.MetricType == "EnergyKwh")
                        .OrderByDescending(r => r.TimestampUtc)
                        .Take(10)
                        .Select(r => (double?)r.Value)
                        .AverageAsync(cancellationToken) ?? 2.0;

                    await anomalyEngine.EvaluatePumpElectricalAsync(device, message.EnergyKwh, baselineKwh: historicalBaseline, message.OccurredAt, cancellationToken);
                }
            }
            catch (Exception)
            {
                // Silenciamiento controlado para preservar la continuidad de ingesta
            }
        }

        // Escritura de la serie temporal en ConsumptionRecord
        var metricType = device.Type == "WaterFlowSensor" ? "WaterM3" : "EnergyKwh";
        var metricVal = incomingCumulative ?? message.EnergyKwh;
        if (metricVal > 0.0)
        {
            db.ConsumptionRecords.Add(new ConsumptionRecord
            {
                ProjectId = device.ProjectId,
                DeviceId = device.Id,
                Subsystem = device.Subsystem,
                MetricType = metricType,
                Value = metricVal,
                TimestampUtc = message.OccurredAt.UtcDateTime
            });
        }

        // Actualización del estado operativo y último latido UTC del dispositivo
        device.Status = message.Status;
        device.LastHeartbeatUtc = message.OccurredAt.UtcDateTime;

        var devProj = await db.DeviceProjections.FindAsync([message.DeviceId], cancellationToken);
        if (devProj is not null)
        {
            devProj.Status = message.Status;
            devProj.LastEventAt = message.OccurredAt.UtcDateTime;
        }

        var shadow = await db.DeviceShadows.FindAsync([message.DeviceId], cancellationToken);
        var isNewer = shadow is null || message.OccurredAt > shadow.ReportedAt;

        var record = new DeviceTelemetry
        {
            DeviceId = message.DeviceId,
            EventId = message.EventId,
            OccurredAt = message.OccurredAt,
            Status = message.Status,
            ReportedJson = message.ReportedJson,
            EnergyKwh = message.EnergyKwh,
            TemperatureC = message.TemperatureC,
            VoltageV = message.VoltageV
        };
        db.DeviceTelemetry.Add(record);

        if (isNewer)
        {
            if (shadow is null)
            {
                shadow = new DeviceShadow
                {
                    DeviceId = message.DeviceId,
                    ReportedJson = message.ReportedJson,
                    ReportedAt = message.OccurredAt,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                db.DeviceShadows.Add(shadow);
            }
            else
            {
                shadow.ReportedJson = message.ReportedJson;
                shadow.ReportedAt = message.OccurredAt;
            }
        }

        var command = await db.DeviceCommands.SingleOrDefaultAsync(item => item.CommandId == message.EventId, cancellationToken);
        if (command is null)
        {
            var pending = await db.DeviceCommands
                .Where(item => item.DeviceId == message.DeviceId && item.AcknowledgedAt == null && item.IssuedAt <= message.OccurredAt)
                .OrderByDescending(item => item.IssuedAt)
                .ToListAsync(cancellationToken);
            command = pending.FirstOrDefault(item => EquivalentJson(item.DesiredJson, message.ReportedJson));
        }

        if (command is not null) command.AcknowledgedAt = message.OccurredAt;
        db.TelemetryRecoveries.Add(new TelemetryRecovery { EventId = message.EventId, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(cancellationToken);

        try { await ReplayExistingAsync(record, cancellationToken); }
        catch (Exception) { }

        return true;
    }

    private async Task ReplayExistingAsync(DeviceTelemetry record, CancellationToken cancellationToken)
    {
        try
        {
            await influx.WriteAsync(new TelemetryMessage(record.DeviceId, record.EventId, record.OccurredAt, record.Status, record.ReportedJson, record.EnergyKwh, record.TemperatureC, record.VoltageV), cancellationToken);
            record.InfluxWrittenAt = DateTimeOffset.UtcNow;
            var intent = await db.TelemetryRecoveries.SingleOrDefaultAsync(item => item.EventId == record.EventId, cancellationToken);
            if (intent is not null) db.TelemetryRecoveries.Remove(intent);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // La falla en InfluxDB no debe abortar la ingesta en el almacenamiento relacional
        }
    }

    private static bool EquivalentJson(string expected, string actual)
    {
        try { return JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)); }
        catch (JsonException) { return false; }
    }

    public async Task<int> ReplayInfluxAsync(CancellationToken cancellationToken = default)
    {
        var recoveries = await db.TelemetryRecoveries.OrderBy(item => item.Id).ToListAsync(cancellationToken);
        var replayed = 0;
        foreach (var recovery in recoveries)
        {
            var record = await db.DeviceTelemetry.SingleAsync(item => item.EventId == recovery.EventId, cancellationToken);
            try { await ReplayExistingAsync(record, cancellationToken); replayed++; }
            catch (Exception) { break; }
        }
        return replayed;
    }
}
