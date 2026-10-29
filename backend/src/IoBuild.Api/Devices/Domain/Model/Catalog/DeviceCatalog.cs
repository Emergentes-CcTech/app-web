namespace IoBuild.Api.Devices.Domain.Model.Catalog;

public sealed record DeviceCatalogEntry(string Code, string DisplayName, string Scope);
public static class DeviceCatalog
{
    private static readonly IReadOnlyDictionary<string, DeviceCatalogEntry> Entries = new Dictionary<string, DeviceCatalogEntry>(StringComparer.Ordinal)
    {
        ["SmartMeter"] = new("SmartMeter", "Smart Meter", "floor"),
        ["WaterSensor"] = new("WaterSensor", "Water Sensor", "floor"),
        ["SmokeDetector"] = new("SmokeDetector", "Smoke Detector", "floor"),
        ["AirConditioner"] = new("AirConditioner", "Air Conditioner", "unit"),
        ["SmartLight"] = new("SmartLight", "Smart Light", "unit")
    };
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
