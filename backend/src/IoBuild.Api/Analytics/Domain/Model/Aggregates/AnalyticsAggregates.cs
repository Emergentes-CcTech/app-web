namespace IoBuild.Api.Analytics.Domain.Model.Aggregates;

public sealed class BuilderMetrics
{
    public int TotalDevices { get; set; }
    public int OnlineDevices { get; set; }
    public int OfflineDevices { get; set; }
    public int AlertsCount { get; set; }
    public int ActiveProjectsCount { get; set; }
    public int TotalUnits { get; set; }
    public int OccupiedUnits { get; set; }
    public double OccupancyRate { get; set; }
    public double EnergyEfficiencyAvg { get; set; }
    public List<HistoricalDataPoint> TemperatureHistory { get; set; } = [];
    public List<HistoricalDataPoint> EnergyHistory { get; set; } = [];
    public List<HistoricalDataPoint> HourlyEnergyData { get; set; } = [];
    public List<HistoricalDataPoint> MonthlyOccupancy { get; set; } = [];
    public Dictionary<string, int> DevicesByType { get; set; } = [];
    public List<Dictionary<string, object>> ProjectsOverview { get; set; } = [];
}

public sealed class OwnerMetrics
{
    public int TotalDevices { get; set; }
    public int OnlineDevices { get; set; }
    public int OfflineDevices { get; set; }
    public int AlertsCount { get; set; }
    public int MyUnitsCount { get; set; }
    public double EnergyThisMonth { get; set; }
    public double TemperatureAvg { get; set; }
    public double WaterUsageThisMonth { get; set; }
    public List<HistoricalDataPoint> TemperatureHistory { get; set; } = [];
    public List<HistoricalDataPoint> EnergyHistory { get; set; } = [];
    public List<HistoricalDataPoint> DailyEnergyConsumption { get; set; } = [];
    public List<HistoricalDataPoint> WaterUsageWeekly { get; set; } = [];
    public List<DeviceHealthStatus> DeviceHealthStatus { get; set; } = [];
    public List<Dictionary<string, object>> MyUnitsDetails { get; set; } = [];
}

public sealed class DeviceHealthStatus
{
    public int DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime LastOnline { get; set; }
}

public sealed class HistoricalDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string Metric { get; set; } = string.Empty;
}
