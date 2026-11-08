using System.Security.Claims;

namespace IoBuild.Api.Shared.Application.Cutover;

public interface ICutoverHarness
{
    Task FreezeAsync(CancellationToken ct = default);
    Task UnfreezeAsync(CancellationToken ct = default);
    Task<CutoverCheckpoint> BackupAsync(string checkpointPath, CancellationToken ct = default);
    Task<CutoverImportResult> ImportAsync(LegacyCutoverDump dump, CancellationToken ct = default);
    Task<bool> VerifyParityAsync(LegacyCutoverDump dump, CutoverImportResult result, CancellationToken ct = default);
    Task SwitchAsync(string nginxConfPath, CancellationToken ct = default);
    Task RestoreAsync(string checkpointPath, CancellationToken ct = default);
    Task<bool> StabilizeAsync(ClaimsPrincipal user, CancellationToken ct = default);
    string ComputeHash(LegacyCutoverDump dump);
}
