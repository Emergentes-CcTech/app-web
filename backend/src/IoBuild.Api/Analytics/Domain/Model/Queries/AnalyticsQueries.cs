namespace IoBuild.Api.Analytics.Domain.Model.Queries;

public sealed record GetBuilderDashboardQuery(int UserId);
public sealed record GetOwnerDashboardQuery(int UserId);
public sealed record GetHistoricalDataQuery(int ProjectId, string Metric, DateTime StartDate, DateTime EndDate);
public sealed record GetBuilderLiveEnergyQuery(int UserId, int Minutes);
public sealed record GetOwnerLiveEnergyQuery(int UserId, int Minutes);
public sealed record EnergyMinutePoint(DateTime Timestamp, double TotalEnergyKwh);
