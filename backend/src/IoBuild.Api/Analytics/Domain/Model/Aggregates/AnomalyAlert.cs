namespace IoBuild.Api.Analytics.Domain.Model.Aggregates;

/// <summary>
/// Alerta temprana de anomalía detectada en áreas comunes (fugas nocturnas, sobrecargas eléctricas, desconexión de gateway).
/// </summary>
public sealed class AnomalyAlert
{
    // Identificador único de la alerta
    public int Id { get; set; }

    // Identificador del condominio
    public int ProjectId { get; set; }

    // Identificador del dispositivo relacionado
    public int DeviceId { get; set; }

    // Subsistema comunal afectado
    public string Subsystem { get; set; } = "MainCistern";

    // Tipo de anomalía: NocturnalWaterLeak, PumpElectricalOverload, GatewayOfflineInBasement
    public string AlertType { get; set; } = "NocturnalWaterLeak";

    // Severidad: Critical, Warning, Info
    public string Severity { get; set; } = "Critical";

    // Título descriptivo de la anomalía
    public string Title { get; set; } = string.Empty;

    // Detalle técnico y causa de la alerta
    public string Description { get; set; } = string.Empty;

    // Sobrecosto estimado en Soles (PEN) por el desperdicio o sobreconsumo
    public decimal EstimatedOvercostSoles { get; set; }

    // Fecha y hora UTC en que se detectó la anomalía
    public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;

    // Fecha y hora UTC de resolución
    public DateTime? ResolvedAtUtc { get; set; }

    // Estado de la alerta: Active, Resolved
    public string Status { get; set; } = "Active";

    // Marca la alerta como resuelta registrando el momento exacto
    public void Resolve()
    {
        Status = "Resolved";
        ResolvedAtUtc = DateTime.UtcNow;
    }
}
