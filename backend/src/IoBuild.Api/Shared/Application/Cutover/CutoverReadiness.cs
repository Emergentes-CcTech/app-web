namespace IoBuild.Api.Shared.Application.Cutover;

public sealed class CutoverReadiness
{
    private readonly object _gate = new();
    private bool _frozen;

    public bool IsFrozen { get { lock (_gate) return _frozen; } }
    public bool ShouldBlockWrites { get { lock (_gate) return _frozen; } }
    public bool IsReady { get { lock (_gate) return !_frozen; } }
    public string? FailureReason { get { lock (_gate) return _frozen ? "cutover_freeze_active" : null; } }
    public void Freeze() { lock (_gate) _frozen = true; }
    public void Unfreeze() { lock (_gate) _frozen = false; }
}
