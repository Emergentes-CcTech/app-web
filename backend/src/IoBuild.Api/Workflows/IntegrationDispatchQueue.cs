using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Workflows;

public sealed record DispatchRequest(string OwnerModule, string Channel, string OrderingKey, long Sequence, string Payload, string IdempotencyKey);
public interface IIntegrationDispatchQueue
{
    Task<IntegrationDispatch> EnqueueAsync(DispatchRequest request, CancellationToken cancellationToken = default);
    Task<IntegrationDispatch?> LeaseDueAsync(string worker, DateTimeOffset now, TimeSpan leaseDuration, CancellationToken cancellationToken = default);
    Task CompleteAsync(long id, string worker, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task FailAsync(long id, string worker, DateTimeOffset now, bool retryable, int maxAttempts, CancellationToken cancellationToken = default);
    Task ReplayAsync(long id, DateTimeOffset now, CancellationToken cancellationToken = default);
}

public sealed class IntegrationDispatchQueue(IoBuildDbContext dbContext) : IIntegrationDispatchQueue
{
    public async Task<IntegrationDispatch> EnqueueAsync(DispatchRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.IntegrationDispatches.SingleOrDefaultAsync(row => row.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (existing is not null) return existing;
        var now = DateTimeOffset.UtcNow;
        var row = new IntegrationDispatch { OwnerModule = request.OwnerModule, Channel = request.Channel, OrderingKey = request.OrderingKey, Sequence = request.Sequence, Payload = request.Payload, IdempotencyKey = request.IdempotencyKey, NextAttemptAt = now, CreatedAt = now, UpdatedAt = now };
        dbContext.IntegrationDispatches.Add(row);
        return row;
    }

    public async Task<IntegrationDispatch?> LeaseDueAsync(string worker, DateTimeOffset now, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        var expired = await dbContext.IntegrationDispatches.Where(row => row.Status == DispatchStatus.InProgress && row.LeaseExpiresAt <= now).ToListAsync(cancellationToken);
        foreach (var row in expired) { row.Status = DispatchStatus.Pending; row.LeaseOwner = null; row.LeaseExpiresAt = null; row.NextAttemptAt = now; row.UpdatedAt = now; }
        if (expired.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
        var candidate = await dbContext.IntegrationDispatches.Where(row => row.Status == DispatchStatus.Pending && row.NextAttemptAt <= now)
            .Where(row => !dbContext.IntegrationDispatches.Any(other => other.OrderingKey == row.OrderingKey && other.Sequence < row.Sequence && (other.Status == DispatchStatus.Pending || other.Status == DispatchStatus.InProgress)))
            .OrderBy(row => row.NextAttemptAt).ThenBy(row => row.OrderingKey).ThenBy(row => row.Sequence).FirstOrDefaultAsync(cancellationToken);
        if (candidate is null) return null;
        candidate.Status = DispatchStatus.InProgress; candidate.LeaseOwner = worker; candidate.LeaseExpiresAt = now.Add(leaseDuration); candidate.Attempts++; candidate.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return candidate;
    }

    public async Task CompleteAsync(long id, string worker, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var row = await dbContext.IntegrationDispatches.SingleAsync(item => item.Id == id, cancellationToken);
        if (row.Status != DispatchStatus.InProgress || row.LeaseOwner != worker) throw new InvalidOperationException("The dispatch lease is not owned by this worker.");
        row.Status = DispatchStatus.Completed; row.LeaseOwner = null; row.LeaseExpiresAt = null; row.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(long id, string worker, DateTimeOffset now, bool retryable, int maxAttempts, CancellationToken cancellationToken = default)
    {
        var row = await dbContext.IntegrationDispatches.SingleAsync(item => item.Id == id, cancellationToken);
        if (row.Status != DispatchStatus.InProgress || row.LeaseOwner != worker) throw new InvalidOperationException("The dispatch lease is not owned by this worker.");
        row.LastError = retryable ? "retryable failure" : "non-retryable failure"; row.LeaseOwner = null; row.LeaseExpiresAt = null; row.UpdatedAt = now;
        row.Status = !retryable || row.Attempts >= maxAttempts ? DispatchStatus.DeadLetter : DispatchStatus.Pending;
        row.NextAttemptAt = row.Status == DispatchStatus.Pending ? now.AddSeconds(Math.Min(3600, Math.Pow(2, row.Attempts))) : now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplayAsync(long id, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var row = await dbContext.IntegrationDispatches.SingleAsync(item => item.Id == id, cancellationToken);
        if (row.Status != DispatchStatus.DeadLetter) throw new InvalidOperationException("Only dead-letter dispatches can be replayed.");
        row.Status = DispatchStatus.Pending; row.Attempts = 0; row.NextAttemptAt = now; row.LeaseOwner = null; row.LeaseExpiresAt = null; row.LastError = "audited replay"; row.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
