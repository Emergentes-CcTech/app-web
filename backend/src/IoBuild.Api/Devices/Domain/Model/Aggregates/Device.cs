using System.ComponentModel.DataAnnotations.Schema;

namespace IoBuild.Api.Devices.Domain.Model.Aggregates;

/// <summary>
/// Agregado Device en el contexto de Hardware e IoT (Devices Context).
/// Modela los dispositivos IoT vinculados exclusivamente a áreas comunes y subsistemas del condominio.
/// </summary>
public sealed class Device
{
    // Identificador único del dispositivo IoT
    public int Id { get; set; }

    // Dirección física única de hardware (MAC Address)
    public string? MacAddress { get; set; }

    // Nombre descriptivo del dispositivo (ej. "GW-SOT-01", "WFS-CIS-IN")
    public string Name { get; set; } = string.Empty;

    // Tipo de dispositivo dentro del catálogo de áreas comunes (GatewayHub, WaterFlowSensor, CurrentTransformerCT)
    public string Type { get; set; } = "WaterFlowSensor";

    // Subsistema comunal al que pertenece (MainCistern, PressurePumpsRoom, ElevatorsBank, GeneralElectricalPanel)
    public string Subsystem { get; set; } = "MainCistern";

    // Identificador de la entidad de área común a la que se vincula el equipo
    public int? CommonAreaId { get; set; }

    // Identificador del condominio al que pertenece el dispositivo (obligatorio)
    public int ProjectId { get; set; }

    // Ubicación física específica en el condominio (ej. "Sótano 1 - Cisterna")
    public string LocationArea { get; set; } = string.Empty;

    // Propiedad puente para compatibilidad con la columna Location en la base de datos existente
    public string Location
    {
        get => string.IsNullOrEmpty(_location) ? LocationArea : _location;
        set { _location = value; if (string.IsNullOrEmpty(LocationArea)) LocationArea = value; }
    }
    private string _location = string.Empty;

    // Umbral de caudal nocturno para la cisterna (m3/h) en la ventana 01:00-04:00 AM (por defecto 0.15)
    public decimal NightFlowThresholdM3H { get; set; } = 0.15m;

    // Última lectura acumulada recibida (m3 para agua o kWh para energía)
    public double LastCumulativeReading { get; set; } = 0.0;

    // Estado operativo actual del dispositivo: Online, Offline, Maintenance
    public string Status { get; set; } = "Online";

    // Marca de tiempo UTC del último latido o paquete de telemetría recibido
    public DateTime? LastHeartbeatUtc { get; set; }

    // Identificador del administrador o responsable técnico
    public int OwnerId { get; set; }

    // Origen del aprovisionamiento
    public string? Source { get; set; }

    // Identificador de unidad privativa (desaconsejado en áreas comunes; preservado para interoperabilidad)
    public int? UnitId { get; set; }
}
