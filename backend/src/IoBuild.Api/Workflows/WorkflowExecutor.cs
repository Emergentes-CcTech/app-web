using Microsoft.EntityFrameworkCore;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace IoBuild.Api.Workflows;

public interface IWorkflow<in TCommand, TResult>
{
    Task<TResult> ExecuteAsync(TCommand command, CancellationToken cancellationToken = default);
}

public sealed class WorkflowExecutor(IoBuildDbContext dbContext)
{
    public async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken cancellationToken = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction? transaction = dbContext.Database.IsRelational()
                ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
            var result = await work(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}
