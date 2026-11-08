namespace IoBuild.Api.Devices.Domain.Model.Aggregates;

/// <summary>
/// Tipos de dispositivo IoT homologados para áreas comunes en condominios residenciales:
/// GatewayHub: Concentrador IoT en sótano con buffer local de 72h y respaldo 4G.
/// WaterFlowSensor: Caudalímetro no invasivo de pulsos en tuberías de cisterna (m3 y m3/h).
/// CurrentTransformerCT: Pinza amperimétrica en tableros eléctricos de bombas, ascensores e iluminación (kWh y A).
/// </summary>
public enum DeviceType
{
    // Concentrador IoT ubicado en sótanos
    GatewayHub,

    // Caudalímetro en cisterna
    WaterFlowSensor,

    // Transformador de corriente en tableros generales
    CurrentTransformerCT
}
