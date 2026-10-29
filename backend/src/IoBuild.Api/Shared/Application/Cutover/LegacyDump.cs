namespace IoBuild.Api.Shared.Application.Cutover;

public sealed record LegacyIamUser(int Id, string Email, string PasswordHash, string Role, DateTime UpdatedAt);
public sealed record LegacyProject(int Id, string Name, string Description, string Location, int BuilderId, int TotalUnits, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record LegacyProfile(int Id, int UserId, string Name, string Username, DateTime UpdatedAt);
public sealed record LegacySubscription(int Id, int BuilderId, int PlanId, string Status, DateTime UpdatedAt);
public sealed record LegacyDevice(int Id, string Name, string Type, string Location, int ProjectId, int? UnitId, int OwnerId, string Status, DateTime UpdatedAt, string? MacAddress);

public sealed class LegacyCutoverDump
{
    public List<LegacyIamUser> IamUsers { get; init; } = [];
    public List<LegacyProject> Projects { get; init; } = [];
    public List<LegacyProfile> Profiles { get; init; } = [];
    public List<LegacySubscription> Subscriptions { get; init; } = [];
    public List<LegacyDevice> Devices { get; init; } = [];
}
