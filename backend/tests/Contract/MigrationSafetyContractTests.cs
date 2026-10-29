using IoBuild.Api.Persistence;
using IoBuild.Api.Readiness;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Contract.Tests;

public sealed class MigrationSafetyContractTests
{
    [Fact]
    public async Task Repeated_successful_migration_attempts_leave_readiness_enabled()
    {
        var readiness = new MigrationReadiness();
        var runner = new StubMigrationRunner();
        var coordinator = new MigrationStartupCoordinator(runner, readiness);

        await coordinator.ApplyAsync(CancellationToken.None);
        await coordinator.ApplyAsync(CancellationToken.None);

        Assert.Equal(2, runner.Attempts);
        Assert.True(readiness.IsReady);
    }

    [Fact]
    public async Task Migration_failure_blocks_cutover_without_changing_committed_rows()
    {
        var options = new DbContextOptionsBuilder<IoBuildDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new IoBuildDbContext(options);
        dbContext.FoundationRecords.Add(new FoundationRecord { Id = 7, Name = "committed" });
        await dbContext.SaveChangesAsync();

        var readiness = new MigrationReadiness();
        var coordinator = new MigrationStartupCoordinator(new ThrowingMigrationRunner(), readiness);

        await coordinator.ApplyAsync(CancellationToken.None);

        Assert.True(readiness.ShouldBlockRequests);
        Assert.Equal("migration failed", readiness.FailureReason);
        Assert.Equal("committed", (await dbContext.FoundationRecords.SingleAsync()).Name);
    }

    private sealed class StubMigrationRunner : IMigrationRunner
    {
        public int Attempts { get; private set; }
        public Task ApplyAsync(CancellationToken cancellationToken) { Attempts++; return Task.CompletedTask; }
    }

    private sealed class ThrowingMigrationRunner : IMigrationRunner
    {
        public Task ApplyAsync(CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException("simulated migration failure"));
    }
}
