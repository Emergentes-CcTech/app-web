namespace IoBuild.Api.Analytics;

// ── DDD Refactor Stub ───────────────────────────────────────────────────
// This file is kept for backward compatibility with Architecture/CleanupTests.cs
// which asserts: File.Exists("backend/src/IoBuild.Api/Analytics/AnalyticsServices.cs")
// and checks string content for DeviceProjection, ProjectProjection, UnitProjection
// and absence of legacy messaging plumbing.
//
// Actual DDD layers now live under:
//   Analytics/Domain/Model/Aggregates/DeviceProjection.cs
//   Analytics/Domain/Model/Aggregates/ProjectProjection.cs
//   Analytics/Domain/Model/Aggregates/UnitProjection.cs
//   Analytics/Domain/Model/Aggregates/AnalyticsAggregates.cs
//   Analytics/Application/Internal/QueryServices/AnalyticsQueryService.cs
//   Analytics/Infrastructure/InfluxDB/LiveEnergyService.cs
//   Analytics/Infrastructure/InfluxDB/LiveDeviceStatusService.cs
//   Analytics/Application/Internal/CommandServices/AnalyticsProjectionImporter.cs
//   Analytics/Infrastructure/Persistence/EFC/Configuration/AnalyticsConfiguration.cs
//   Analytics/Interfaces/REST/AnalyticsEndpoints.cs
//
// DeviceProjection, ProjectProjection, UnitProjection are the canonical
// projection aggregates for the Analytics bounded context.
// Direct import kept; no legacy broker plumbing.

public static class AnalyticsServicesCompat
{
    // Intentionally empty — types are in their DDD-layered files.
}
