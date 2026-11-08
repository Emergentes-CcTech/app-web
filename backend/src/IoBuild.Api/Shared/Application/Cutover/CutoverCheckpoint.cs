namespace IoBuild.Api.Shared.Application.Cutover;

public sealed class CutoverCheckpoint
{
    public DateTime CheckpointAt { get; set; }
    public int IamCount { get; set; }
    public int ProjectCount { get; set; }
    public int ProfileCount { get; set; }
    public int SubscriptionCount { get; set; }
    public int DeviceCount { get; set; }
    public string Hash { get; set; } = string.Empty;
    public List<int> IamIds { get; set; } = [];
    public List<int> ProjectIds { get; set; } = [];
    public List<int> ProfileIds { get; set; } = [];
    public List<int> SubscriptionIds { get; set; } = [];
    public List<int> DeviceIds { get; set; } = [];
}

public sealed record CutoverImportResult(
    int IamInserted,
    int IamUpdated,
    int ProjectInserted,
    int ProjectUpdated,
    int ProfileInserted,
    int ProfileUpdated,
    int SubscriptionInserted,
    int SubscriptionUpdated,
    int DeviceInserted,
    int DeviceUpdated,
    string ParityHash,
    IReadOnlyList<string> ImportOrder);
