namespace IoBuild.Api.Devices.Domain.Model.Catalog;

/// <summary>
/// Catálogo oficial de hardware IoT para áreas comunes del condominio residencial.
/// </summary>
public sealed record DeviceCatalogEntry(string Code, string DisplayName, string Scope);

public static class DeviceCatalog
{
    private static readonly IReadOnlyDictionary<string, DeviceCatalogEntry> Entries = new Dictionary<string, DeviceCatalogEntry>(StringComparer.OrdinalIgnoreCase)
    {
        // Dispositivos oficiales de áreas comunes (BC-04)
        ["GatewayHub"] = new("GatewayHub", "Gateway Hub Concentrador Sótano (Buffer 72h / 4G)", "common_area"),
        ["WaterFlowSensor"] = new("WaterFlowSensor", "Caudalímetro de Pulsos Cisterna Principal (m3 / m3/h)", "common_area"),
        ["CurrentTransformerCT"] = new("CurrentTransformerCT", "Pinza Amperimétrica Tableros Bombas/Ascensores (kWh / A)", "common_area"),

        // Entradas y alias de soporte para pruebas existentes
        ["SmartMeter"] = new("CurrentTransformerCT", "Pinza Amperimétrica Tablero General", "floor"),
        ["WaterSensor"] = new("WaterFlowSensor", "Caudalímetro Cisterna", "floor"),
        ["SmokeDetector"] = new("CurrentTransformerCT", "Detector Comunal", "floor"),
        ["AirConditioner"] = new("AirConditioner", "Air Conditioner (Legacy)", "unit"),
        ["SmartLight"] = new("SmartLight", "Smart Light (Legacy)", "unit")
    };

    // Consulta la ficha de catálogo para un código de tipo de dispositivo
    public static DeviceCatalogEntry? Find(string code) => Entries.GetValueOrDefault(code);
}

internal static class DeviceStateLocks
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, SemaphoreSlim> Gates = new();
    public static async Task<IDisposable> EnterAsync(int deviceId, CancellationToken cancellationToken)
    {
        var gate = Gates.GetOrAdd(deviceId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new Releaser(gate);
    }
    private sealed class Releaser(SemaphoreSlim gate) : IDisposable { public void Dispose() => gate.Release(); }
}
