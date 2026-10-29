using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Shared.Application.Cutover;

public sealed class CutoverHarness : ICutoverHarness
{
    private readonly IoBuildDbContext _db;
    private readonly CutoverReadiness _readiness;

    private readonly Dictionary<int, DateTime> _iamTimestamps = new();
    private readonly Dictionary<int, DateTime> _projectTimestamps = new();
    private readonly Dictionary<int, DateTime> _profileTimestamps = new();
    private readonly Dictionary<int, DateTime> _subscriptionTimestamps = new();
    private readonly Dictionary<int, DateTime> _deviceTimestamps = new();

    public CutoverHarness(IoBuildDbContext db, CutoverReadiness readiness)
    {
        _db = db;
        _readiness = readiness;
    }

    public Task FreezeAsync(CancellationToken ct = default)
    {
        _readiness.Freeze();
        return Task.CompletedTask;
    }

    public Task UnfreezeAsync(CancellationToken ct = default)
    {
        _readiness.Unfreeze();
        return Task.CompletedTask;
    }

    public async Task<CutoverCheckpoint> BackupAsync(string checkpointPath, CancellationToken ct = default)
    {
        var iamIds = await _db.IamUsers.AsNoTracking().Select(u => u.Id).ToListAsync(ct);
        var projectIds = await _db.Projects.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
        var profileIds = await _db.Profiles.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
        var subscriptionIds = await _db.Subscriptions.AsNoTracking().Select(s => s.Id).ToListAsync(ct);
        var deviceIds = await _db.Devices.AsNoTracking().Select(d => d.Id).ToListAsync(ct);

        var checkpoint = new CutoverCheckpoint
        {
            CheckpointAt = DateTime.UtcNow,
            IamCount = iamIds.Count,
            ProjectCount = projectIds.Count,
            ProfileCount = profileIds.Count,
            SubscriptionCount = subscriptionIds.Count,
            DeviceCount = deviceIds.Count,
            Hash = ComputeCountsHash(iamIds.Count, projectIds.Count, profileIds.Count, subscriptionIds.Count, deviceIds.Count),
            IamIds = iamIds,
            ProjectIds = projectIds,
            ProfileIds = profileIds,
            SubscriptionIds = subscriptionIds,
            DeviceIds = deviceIds
        };

        var dir = Path.GetDirectoryName(checkpointPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(checkpoint, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(checkpointPath, json, ct);

        var dumpPath = checkpointPath + ".mysqldump.sql";
        var dumpContent = $"-- mysqldump concept -- checkpoint at {checkpoint.CheckpointAt:O}\n-- counts: iam={checkpoint.IamCount} projects={checkpoint.ProjectCount} profiles={checkpoint.ProfileCount} subscriptions={checkpoint.SubscriptionCount} devices={checkpoint.DeviceCount}\n";
        await File.WriteAllTextAsync(dumpPath, dumpContent, ct);

        var hashPath = checkpointPath + ".hash";
        await File.WriteAllTextAsync(hashPath, checkpoint.Hash, ct);

        return checkpoint;
    }

    public async Task<CutoverImportResult> ImportAsync(LegacyCutoverDump dump, CancellationToken ct = default)
    {
        var order = new List<string> { "IAM", "Projects", "Profiles", "Subscriptions", "Devices" };

        int iamInserted = 0, iamUpdated = 0;
        int projectInserted = 0, projectUpdated = 0;
        int profileInserted = 0, profileUpdated = 0;
        int subscriptionInserted = 0, subscriptionUpdated = 0;
        int deviceInserted = 0, deviceUpdated = 0;

        foreach (var u in dump.IamUsers.OrderBy(x => x.Id))
        {
            if (IsStale(_iamTimestamps, u.Id, u.UpdatedAt)) continue;
            var existing = await _db.IamUsers.FindAsync([u.Id], ct);
            if (existing is null)
            {
                var byEmail = await _db.IamUsers.FirstOrDefaultAsync(x => x.Email == u.Email, ct);
                if (byEmail is not null)
                {
                    if (u.UpdatedAt >= _iamTimestamps.GetValueOrDefault(byEmail.Id, DateTime.MinValue))
                    {
                        byEmail.Role = u.Role;
                        byEmail.PasswordHash = u.PasswordHash;
                        await _db.SaveChangesAsync(ct);
                        iamUpdated++;
                        _iamTimestamps[byEmail.Id] = u.UpdatedAt;
                    }
                    _iamTimestamps[u.Id] = u.UpdatedAt;
                    continue;
                }
                _db.IamUsers.Add(new IamUser { Id = u.Id, Email = u.Email, PasswordHash = u.PasswordHash, Role = u.Role });
                await _db.SaveChangesAsync(ct);
                iamInserted++;
            }
            else
            {
                if (u.UpdatedAt < _iamTimestamps.GetValueOrDefault(u.Id, DateTime.MinValue)) continue;
                existing.Email = u.Email;
                existing.PasswordHash = u.PasswordHash;
                existing.Role = u.Role;
                await _db.SaveChangesAsync(ct);
                iamUpdated++;
            }
            _iamTimestamps[u.Id] = u.UpdatedAt;
        }

        foreach (var p in dump.Projects.OrderBy(x => x.Id))
        {
            if (IsStale(_projectTimestamps, p.Id, p.UpdatedAt)) continue;
            var existing = await _db.Projects.FindAsync([p.Id], ct);
            if (existing is null)
            {
                _db.Projects.Add(new Project
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Location = p.Location,
                    BuilderId = p.BuilderId,
                    TotalUnits = p.TotalUnits,
                    CreatedAt = p.UpdatedAt
                });
                await _db.SaveChangesAsync(ct);
                projectInserted++;
            }
            else
            {
                if (p.UpdatedAt < _projectTimestamps.GetValueOrDefault(p.Id, DateTime.MinValue)) continue;
                if (_projectTimestamps.TryGetValue(p.Id, out var last) == false && p.UpdatedAt < existing.CreatedAt.UtcDateTime) continue;
                existing.Name = p.Name;
                existing.Description = p.Description;
                existing.Location = p.Location;
                existing.BuilderId = p.BuilderId;
                existing.TotalUnits = p.TotalUnits;
                existing.CreatedAt = p.UpdatedAt;
                await _db.SaveChangesAsync(ct);
                projectUpdated++;
            }
            _projectTimestamps[p.Id] = p.UpdatedAt;
        }

        foreach (var pr in dump.Profiles.OrderBy(x => x.Id))
        {
            if (IsStale(_profileTimestamps, pr.Id, pr.UpdatedAt)) continue;
            var existing = await _db.Profiles.FindAsync([pr.Id], ct);
            if (existing is null)
            {
                _db.Profiles.Add(new Profile { Id = pr.Id, UserId = pr.UserId, Name = pr.Name, Username = pr.Username });
                await _db.SaveChangesAsync(ct);
                profileInserted++;
            }
            else
            {
                if (pr.UpdatedAt < _profileTimestamps.GetValueOrDefault(pr.Id, DateTime.MinValue)) continue;
                existing.UserId = pr.UserId;
                existing.Name = pr.Name;
                existing.Username = pr.Username;
                await _db.SaveChangesAsync(ct);
                profileUpdated++;
            }
            _profileTimestamps[pr.Id] = pr.UpdatedAt;
        }

        foreach (var s in dump.Subscriptions.OrderBy(x => x.Id))
        {
            if (IsStale(_subscriptionTimestamps, s.Id, s.UpdatedAt)) continue;
            var existing = await _db.Subscriptions.FindAsync([s.Id], ct);
            if (existing is null)
            {
                _db.Subscriptions.Add(new Subscription { Id = s.Id, BuilderId = s.BuilderId, PlanId = s.PlanId, Status = s.Status });
                await _db.SaveChangesAsync(ct);
                subscriptionInserted++;
            }
            else
            {
                if (s.UpdatedAt < _subscriptionTimestamps.GetValueOrDefault(s.Id, DateTime.MinValue)) continue;
                existing.BuilderId = s.BuilderId;
                existing.PlanId = s.PlanId;
                existing.Status = s.Status;
                await _db.SaveChangesAsync(ct);
                subscriptionUpdated++;
            }
            _subscriptionTimestamps[s.Id] = s.UpdatedAt;
        }

        foreach (var d in dump.Devices.OrderBy(x => x.Id))
        {
            if (IsStale(_deviceTimestamps, d.Id, d.UpdatedAt)) continue;

            var unitId = d.UnitId;
            var projectExists = await _db.Projects.AnyAsync(p => p.Id == d.ProjectId, ct);
            if (!projectExists)
            {
                unitId = null;
            }

            var existing = await _db.Devices.FindAsync([d.Id], ct);
            if (existing is null)
            {
                var duplicate = d.UnitId.HasValue
                    ? await _db.Devices.FirstOrDefaultAsync(x => x.ProjectId == d.ProjectId && x.UnitId == unitId && x.Type == d.Type, ct)
                    : null;
                if (duplicate is not null)
                {
                    if (d.UpdatedAt >= _deviceTimestamps.GetValueOrDefault(duplicate.Id, DateTime.MinValue))
                    {
                        duplicate.Name = d.Name;
                        duplicate.Location = d.Location;
                        duplicate.Status = d.Status;
                        duplicate.MacAddress = d.MacAddress;
                        duplicate.OwnerId = d.OwnerId;
                        duplicate.UnitId = unitId;
                        await _db.SaveChangesAsync(ct);
                        deviceUpdated++;
                        _deviceTimestamps[duplicate.Id] = d.UpdatedAt;
                    }
                    _deviceTimestamps[d.Id] = d.UpdatedAt;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(d.MacAddress) && await _db.Devices.AnyAsync(x => x.MacAddress == d.MacAddress, ct))
                {
                    var byMac = await _db.Devices.FirstAsync(x => x.MacAddress == d.MacAddress, ct);
                    if (d.UpdatedAt >= _deviceTimestamps.GetValueOrDefault(byMac.Id, DateTime.MinValue))
                    {
                        byMac.Name = d.Name;
                        byMac.Type = d.Type;
                        byMac.Location = d.Location;
                        byMac.Status = d.Status;
                        await _db.SaveChangesAsync(ct);
                        deviceUpdated++;
                        _deviceTimestamps[byMac.Id] = d.UpdatedAt;
                    }
                    _deviceTimestamps[d.Id] = d.UpdatedAt;
                    continue;
                }

                _db.Devices.Add(new Device
                {
                    Id = d.Id,
                    Name = d.Name,
                    Type = d.Type,
                    Location = d.Location,
                    ProjectId = d.ProjectId,
                    UnitId = unitId,
                    OwnerId = d.OwnerId,
                    Status = d.Status,
                    MacAddress = d.MacAddress
                });
                await _db.SaveChangesAsync(ct);
                deviceInserted++;
            }
            else
            {
                if (d.UpdatedAt < _deviceTimestamps.GetValueOrDefault(d.Id, DateTime.MinValue)) continue;
                existing.Name = d.Name;
                existing.Type = d.Type;
                existing.Location = d.Location;
                existing.ProjectId = d.ProjectId;
                existing.UnitId = unitId;
                existing.OwnerId = d.OwnerId;
                existing.Status = d.Status;
                existing.MacAddress = d.MacAddress;
                await _db.SaveChangesAsync(ct);
                deviceUpdated++;
            }
            _deviceTimestamps[d.Id] = d.UpdatedAt;
        }

        var hash = ComputeHash(dump);
        return new CutoverImportResult(
            iamInserted, iamUpdated,
            projectInserted, projectUpdated,
            profileInserted, profileUpdated,
            subscriptionInserted, subscriptionUpdated,
            deviceInserted, deviceUpdated,
            hash, order);
    }

    public Task<bool> VerifyParityAsync(LegacyCutoverDump dump, CutoverImportResult result, CancellationToken ct = default)
    {
        var expectedHash = ComputeHash(dump);
        if (!string.Equals(expectedHash, result.ParityHash, StringComparison.Ordinal)) return Task.FromResult(false);

        var totalInserted = result.IamInserted + result.ProjectInserted + result.ProfileInserted + result.SubscriptionInserted + result.DeviceInserted;
        var totalDump = dump.IamUsers.Count + dump.Projects.Count + dump.Profiles.Count + dump.Subscriptions.Count + dump.Devices.Count;
        if (totalInserted > totalDump) return Task.FromResult(false);

        return Task.FromResult(true);
    }

    public async Task SwitchAsync(string nginxConfPath, CancellationToken ct = default)
    {
        var config = """
            server {
                listen 80 default_server;
                server_name iobuild.arroz.dev;

                # API → Monolith (cutover)
                location /api/ {
                    proxy_pass http://iobuild-api:8080;
                    proxy_http_version 1.1;
                    proxy_set_header Upgrade $http_upgrade;
                    proxy_set_header Connection 'upgrade';
                    proxy_set_header Host $host;
                    proxy_set_header X-Real-IP $remote_addr;
                    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
                    proxy_set_header X-Forwarded-Proto $scheme;
                }

                location /health {
                    proxy_pass http://iobuild-api:8080;
                }

                location / {
                    root /usr/share/nginx/html;
                    try_files $uri $uri/ /index.html;
                }
            }
            """;
        var dir = Path.GetDirectoryName(nginxConfPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(nginxConfPath, config, ct);
    }

    public async Task RestoreAsync(string checkpointPath, CancellationToken ct = default)
    {
        if (!File.Exists(checkpointPath)) throw new FileNotFoundException("Checkpoint not found", checkpointPath);

        var json = await File.ReadAllTextAsync(checkpointPath, ct);
        var checkpoint = JsonSerializer.Deserialize<CutoverCheckpoint>(json) ?? throw new InvalidOperationException("Invalid checkpoint");

        var committedIamIds = new HashSet<int>(checkpoint.IamIds);
        var committedProjectIds = new HashSet<int>(checkpoint.ProjectIds);
        var committedProfileIds = new HashSet<int>(checkpoint.ProfileIds);
        var committedSubscriptionIds = new HashSet<int>(checkpoint.SubscriptionIds);
        var committedDeviceIds = new HashSet<int>(checkpoint.DeviceIds);

        var extraIam = await _db.IamUsers.Where(u => !committedIamIds.Contains(u.Id)).ToListAsync(ct);
        var extraProjects = await _db.Projects.Where(p => !committedProjectIds.Contains(p.Id)).ToListAsync(ct);
        var extraProfiles = await _db.Profiles.Where(p => !committedProfileIds.Contains(p.Id)).ToListAsync(ct);
        var extraSubscriptions = await _db.Subscriptions.Where(s => !committedSubscriptionIds.Contains(s.Id)).ToListAsync(ct);
        var extraDevices = await _db.Devices.Where(d => !committedDeviceIds.Contains(d.Id)).ToListAsync(ct);

        _db.IamUsers.RemoveRange(extraIam);
        _db.Projects.RemoveRange(extraProjects);
        _db.Profiles.RemoveRange(extraProfiles);
        _db.Subscriptions.RemoveRange(extraSubscriptions);
        _db.Devices.RemoveRange(extraDevices);
        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> StabilizeAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var role = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value ?? string.Empty;
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(role, "Administrator", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }
        _readiness.Unfreeze();
        return Task.FromResult(true);
    }

    public string ComputeHash(LegacyCutoverDump dump)
    {
        var sb = new StringBuilder();
        foreach (var u in dump.IamUsers.OrderBy(x => x.Id))
            sb.Append($"IAM:{u.Id}:{u.Email}:{u.Role}:{u.UpdatedAt:O}|");
        foreach (var p in dump.Projects.OrderBy(x => x.Id))
            sb.Append($"P:{p.Id}:{p.Name}:{p.BuilderId}:{p.UpdatedAt:O}|");
        foreach (var pr in dump.Profiles.OrderBy(x => x.Id))
            sb.Append($"PR:{pr.Id}:{pr.UserId}:{pr.Username}:{pr.UpdatedAt:O}|");
        foreach (var s in dump.Subscriptions.OrderBy(x => x.Id))
            sb.Append($"S:{s.Id}:{s.BuilderId}:{s.PlanId}:{s.Status}:{s.UpdatedAt:O}|");
        foreach (var d in dump.Devices.OrderBy(x => x.Id))
            sb.Append($"D:{d.Id}:{d.ProjectId}:{d.UnitId}:{d.Type}:{d.Status}:{d.UpdatedAt:O}|");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeCountsHash(int iam, int proj, int prof, int sub, int dev)
    {
        var raw = $"{iam}:{proj}:{prof}:{sub}:{dev}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsStale(Dictionary<int, DateTime> timestamps, int id, DateTime incoming)
    {
        if (timestamps.TryGetValue(id, out var last) && incoming < last) return true;
        return false;
    }
}
