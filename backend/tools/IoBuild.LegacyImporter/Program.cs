using IoBuild.Api.Analytics;
using IoBuild.Api.Shared.Application.Cutover;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();
var connectionString = configuration.GetConnectionString("IoBuild") ?? "Server=localhost;Port=3306;Database=iobuild;User=root;Password=iobuild";
var services = new ServiceCollection();
services.AddDbContext<IoBuildDbContext>(options => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
services.AddSingleton<CutoverReadiness>();
services.AddScoped<ICutoverHarness, CutoverHarness>();
var provider = services.BuildServiceProvider();

var checkpointPath = args.Length > 0 ? args[0] : "checkpoint.json";
var legacyPath = args.Length > 1 ? args[1] : null;

Console.WriteLine($"IoBuild LegacyImporter — checkpoint: {checkpointPath} (now wraps CutoverHarness for full IAM→Projects/Profiles→Subscriptions→Devices order)");

using var scope = provider.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
var importer = new AnalyticsProjectionImporter(db);
var harness = scope.ServiceProvider.GetRequiredService<ICutoverHarness>();

var checkpoint = await CheckpointStore.LoadAsync(checkpointPath);
Console.WriteLine($"Checkpoint loaded: {checkpoint.LastImportAt:O}, counts: projects={checkpoint.ProjectCount}, units={checkpoint.UnitCount}, devices={checkpoint.DeviceCount}");

// Full ordered import via CutoverHarness when full dump provided, otherwise fallback to projection-only demo
if (legacyPath is not null && File.Exists(legacyPath))
{
    var json = await File.ReadAllTextAsync(legacyPath);
    // Try full cutover dump first
    try
    {
        var cutover = JsonSerializer.Deserialize<LegacyCutoverDumpFile>(json);
        if (cutover is not null && (cutover.IamUsers.Count > 0 || cutover.Subscriptions.Count > 0 || cutover.Profiles.Count > 0))
        {
            var dump = new LegacyCutoverDump
            {
                IamUsers = cutover.IamUsers.Select(u => new IoBuild.Api.Shared.Application.Cutover.LegacyIamUser(u.Id, u.Email, u.PasswordHash, u.Role, u.UpdatedAt)).ToList(),
                Projects = cutover.Projects.Select(p => new IoBuild.Api.Shared.Application.Cutover.LegacyProject(p.ProjectId, p.Name, p.Description, p.Location, p.BuilderId, p.TotalUnits, p.CreatedAt, p.UpdatedAt)).ToList(),
                Profiles = cutover.Profiles.Select(p => new IoBuild.Api.Shared.Application.Cutover.LegacyProfile(p.Id, p.UserId, p.Name, p.Username, p.UpdatedAt)).ToList(),
                Subscriptions = cutover.Subscriptions.Select(s => new IoBuild.Api.Shared.Application.Cutover.LegacySubscription(s.Id, s.BuilderId, s.PlanId, s.Status, s.UpdatedAt)).ToList(),
                Devices = cutover.Devices.Select(d => new IoBuild.Api.Shared.Application.Cutover.LegacyDevice(d.Id, d.Name, d.Type, d.Location, d.ProjectId, d.UnitId, d.OwnerId, d.Status, d.UpdatedAt, d.MacAddress)).ToList()
            };
            var before = (await db.IamUsers.CountAsync(), await db.Projects.CountAsync(), await db.Profiles.CountAsync(), await db.Subscriptions.CountAsync(), await db.Devices.CountAsync());
            var result = await harness.ImportAsync(dump);
            var after = (await db.IamUsers.CountAsync(), await db.Projects.CountAsync(), await db.Profiles.CountAsync(), await db.Subscriptions.CountAsync(), await db.Devices.CountAsync());
            Console.WriteLine($"Cutover import done (order {string.Join("→", result.ImportOrder)}): iam {before.Item1}->{after.Item1}, projects {before.Item2}->{after.Item2}, profiles {before.Item3}->{after.Item3}, subscriptions {before.Item4}->{after.Item4}, devices {before.Item5}->{after.Item5} hash={result.ParityHash}");
            var verified = await harness.VerifyParityAsync(dump, result);
            Console.WriteLine($"Parity verified: {verified}");
            checkpoint.LastImportAt = DateTime.UtcNow;
            checkpoint.ProjectCount = after.Item2;
            checkpoint.UnitCount = after.Item3;
            checkpoint.DeviceCount = after.Item5;
            await CheckpointStore.SaveAsync(checkpointPath, checkpoint);
        }
        else
        {
            // Fallback to legacy analytics projection import
            var data = JsonSerializer.Deserialize<LegacyDump>(json) ?? new LegacyDump();
            var before = (await db.ProjectProjections.CountAsync(), await db.UnitProjections.CountAsync(), await db.DeviceProjections.CountAsync());
            await importer.ImportAsync(
                data.Projects.Select(p => new ProjectProjection { ProjectId = p.ProjectId, BuilderUserId = p.BuilderUserId, Name = p.Name, Status = p.Status, LastEventAt = p.LastEventAt }),
                data.Units.Select(u => new UnitProjection { UnitId = u.UnitId, ProjectId = u.ProjectId, BuilderUserId = u.BuilderUserId, OwnerUserId = u.OwnerUserId, Status = u.Status, LastEventAt = u.LastEventAt, Floor = u.Floor, RoomNumber = u.RoomNumber, OwnerEmail = u.OwnerEmail }),
                data.Devices.Select(d => new DeviceProjection { DeviceId = d.DeviceId, ProjectId = d.ProjectId, UnitId = d.UnitId, DeviceType = d.DeviceType, Status = d.Status, LastEventAt = d.LastEventAt, FloorNumber = d.FloorNumber, DeviceName = d.DeviceName }));
            var after = (await db.ProjectProjections.CountAsync(), await db.UnitProjections.CountAsync(), await db.DeviceProjections.CountAsync());
            Console.WriteLine($"Import done: projects {before.Item1}->{after.Item1}, units {before.Item2}->{after.Item2}, devices {before.Item3}->{after.Item3}");
            checkpoint.LastImportAt = DateTime.UtcNow;
            checkpoint.ProjectCount = after.Item1;
            checkpoint.UnitCount = after.Item2;
            checkpoint.DeviceCount = after.Item3;
            await CheckpointStore.SaveAsync(checkpointPath, checkpoint);
        }
    }
    catch (JsonException)
    {
        var data = JsonSerializer.Deserialize<LegacyDump>(json) ?? new LegacyDump();
        var before = (await db.ProjectProjections.CountAsync(), await db.UnitProjections.CountAsync(), await db.DeviceProjections.CountAsync());
        await importer.ImportAsync(
            data.Projects.Select(p => new ProjectProjection { ProjectId = p.ProjectId, BuilderUserId = p.BuilderUserId, Name = p.Name, Status = p.Status, LastEventAt = p.LastEventAt }),
            data.Units.Select(u => new UnitProjection { UnitId = u.UnitId, ProjectId = u.ProjectId, BuilderUserId = u.BuilderUserId, OwnerUserId = u.OwnerUserId, Status = u.Status, LastEventAt = u.LastEventAt, Floor = u.Floor, RoomNumber = u.RoomNumber, OwnerEmail = u.OwnerEmail }),
            data.Devices.Select(d => new DeviceProjection { DeviceId = d.DeviceId, ProjectId = d.ProjectId, UnitId = d.UnitId, DeviceType = d.DeviceType, Status = d.Status, LastEventAt = d.LastEventAt, FloorNumber = d.FloorNumber, DeviceName = d.DeviceName }));
        var after = (await db.ProjectProjections.CountAsync(), await db.UnitProjections.CountAsync(), await db.DeviceProjections.CountAsync());
        Console.WriteLine($"Import done: projects {before.Item1}->{after.Item1}, units {before.Item2}->{after.Item2}, devices {before.Item3}->{after.Item3}");
        checkpoint.LastImportAt = DateTime.UtcNow;
        checkpoint.ProjectCount = after.Item1;
        checkpoint.UnitCount = after.Item2;
        checkpoint.DeviceCount = after.Item3;
        await CheckpointStore.SaveAsync(checkpointPath, checkpoint);
    }
}
else
{
    // Demo: idempotent repeatability check without external file
    Console.WriteLine("No legacy file provided — verifying idempotent repeatability with empty import.");
    var before = (await db.ProjectProjections.CountAsync(), await db.UnitProjections.CountAsync(), await db.DeviceProjections.CountAsync());
    await importer.ImportAsync([], [], []);
    var after = (await db.ProjectProjections.CountAsync(), await db.UnitProjections.CountAsync(), await db.DeviceProjections.CountAsync());
    Console.WriteLine($"Repeatability check: {before} -> {after} (should be equal)");
    var cutoverBefore = (await db.IamUsers.CountAsync(), await db.Projects.CountAsync(), await db.Devices.CountAsync());
    await harness.ImportAsync(new LegacyCutoverDump());
    var cutoverAfter = (await db.IamUsers.CountAsync(), await db.Projects.CountAsync(), await db.Devices.CountAsync());
    Console.WriteLine($"Cutover repeatability: {cutoverBefore} -> {cutoverAfter} (should be equal)");
}

// Handle pending UnitOwnerMatchedEvent placeholders (out-of-order delivery)
var pendingOwners = await db.UnitProjections.Where(u => u.OwnerUserId != null && u.Status != "Occupied").ToListAsync();
foreach (var unit in pendingOwners)
{
    await importer.ApplyUnitOwnerMatchedAsync(unit.UnitId, unit.ProjectId, unit.OwnerUserId!.Value, unit.OwnerEmail ?? string.Empty, unit.LastEventAt);
}
Console.WriteLine("LegacyImporter completed successfully.");

public sealed class CheckpointStore
{
    public DateTime LastImportAt { get; set; } = DateTime.MinValue;
    public int ProjectCount { get; set; }
    public int UnitCount { get; set; }
    public int DeviceCount { get; set; }

    public static async Task<CheckpointStore> LoadAsync(string path)
    {
        if (!File.Exists(path)) return new CheckpointStore();
        try { return JsonSerializer.Deserialize<CheckpointStore>(await File.ReadAllTextAsync(path)) ?? new CheckpointStore(); }
        catch { return new CheckpointStore(); }
    }

    public static async Task SaveAsync(string path, CheckpointStore checkpoint)
    {
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(checkpoint, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class LegacyDump
{
    public List<LegacyProject> Projects { get; set; } = [];
    public List<LegacyUnit> Units { get; set; } = [];
    public List<LegacyDevice> Devices { get; set; } = [];
}

public sealed class LegacyProject { public int ProjectId { get; set; } public int BuilderUserId { get; set; } public string Name { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; public DateTime LastEventAt { get; set; } }
public sealed class LegacyUnit { public int UnitId { get; set; } public int ProjectId { get; set; } public int BuilderUserId { get; set; } public int? OwnerUserId { get; set; } public string Status { get; set; } = string.Empty; public DateTime LastEventAt { get; set; } public int? Floor { get; set; } public string? RoomNumber { get; set; } public string? OwnerEmail { get; set; } }
public sealed class LegacyDevice { public int DeviceId { get; set; } public int? ProjectId { get; set; } public int? UnitId { get; set; } public string DeviceType { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; public DateTime LastEventAt { get; set; } public int? FloorNumber { get; set; } public string? DeviceName { get; set; } }

public sealed class LegacyCutoverDumpFile
{
    public List<LegacyCutoverIam> IamUsers { get; set; } = [];
    public List<LegacyCutoverProject> Projects { get; set; } = [];
    public List<LegacyCutoverProfile> Profiles { get; set; } = [];
    public List<LegacyCutoverSubscription> Subscriptions { get; set; } = [];
    public List<LegacyCutoverDevice> Devices { get; set; } = [];
}
public sealed class LegacyCutoverIam { public int Id { get; set; } public string Email { get; set; } = string.Empty; public string PasswordHash { get; set; } = string.Empty; public string Role { get; set; } = string.Empty; public DateTime UpdatedAt { get; set; } }
public sealed class LegacyCutoverProject { public int ProjectId { get; set; } public string Name { get; set; } = string.Empty; public string Description { get; set; } = string.Empty; public string Location { get; set; } = string.Empty; public int BuilderId { get; set; } public int TotalUnits { get; set; } public DateTime CreatedAt { get; set; } public DateTime UpdatedAt { get; set; } }
public sealed class LegacyCutoverProfile { public int Id { get; set; } public int UserId { get; set; } public string Name { get; set; } = string.Empty; public string Username { get; set; } = string.Empty; public DateTime UpdatedAt { get; set; } }
public sealed class LegacyCutoverSubscription { public int Id { get; set; } public int BuilderId { get; set; } public int PlanId { get; set; } public string Status { get; set; } = string.Empty; public DateTime UpdatedAt { get; set; } }
public sealed class LegacyCutoverDevice { public int Id { get; set; } public string Name { get; set; } = string.Empty; public string Type { get; set; } = string.Empty; public string Location { get; set; } = string.Empty; public int ProjectId { get; set; } public int? UnitId { get; set; } public int OwnerId { get; set; } public string Status { get; set; } = string.Empty; public DateTime UpdatedAt { get; set; } public string? MacAddress { get; set; } }
