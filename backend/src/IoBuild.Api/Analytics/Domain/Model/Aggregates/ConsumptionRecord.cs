namespace IoBuild.Api.Analytics.Domain.Model.Aggregates;

/// <summary>
/// Registro de serie temporal de consumo de áreas comunes (agua en m3 o energía en kWh).
/// </summary>
public sealed class ConsumptionRecord
{
    // Identificador único de la lectura de serie temporal
    public long Id { get; set; }

    // Identificador del condominio
    public int ProjectId { get; set; }

    // Identificador del dispositivo emisor
    public int DeviceId { get; set; }

    // Subsistema comunal monitoreado (MainCistern, PressurePumpsRoom, ElevatorsBank, GeneralElectricalPanel)
    public string Subsystem { get; set; } = "MainCistern";

    // Tipo de métrica: WaterM3, EnergyKwh, FlowRateM3H, CurrentAmperes
    public string MetricType { get; set; } = "WaterM3";

    // Valor medido en la unidad correspondiente
    public double Value { get; set; }

    // Marca temporal UTC de la lectura
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
