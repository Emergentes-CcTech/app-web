namespace IoBuild.Api.Readiness;

public sealed class MigrationReadiness
{
    private readonly object _gate = new();
    private string? _failureReason;
    private bool _migrationsSucceeded;

    public bool IsReady { get { lock (_gate) return _migrationsSucceeded && _failureReason is null; } }
    public bool ShouldBlockRequests => !IsReady;
    public string? FailureReason { get { lock (_gate) return _failureReason; } }

    public void RecordMigrationSuccess()
    {
        lock (_gate)
        {
            _migrationsSucceeded = true;
            _failureReason = null;
        }
    }

    public void RecordMigrationFailure(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        lock (_gate)
        {
            _migrationsSucceeded = false;
            _failureReason = reason;
        }
    }
}
