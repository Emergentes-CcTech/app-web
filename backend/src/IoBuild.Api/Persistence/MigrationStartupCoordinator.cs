using IoBuild.Api.Readiness;

namespace IoBuild.Api.Persistence;

public sealed class MigrationStartupCoordinator(IMigrationRunner migrationRunner, MigrationReadiness readiness)
{
    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await migrationRunner.ApplyAsync(cancellationToken);
            readiness.RecordMigrationSuccess();
        }
        catch (Exception)
        {
            // Migration failures must leave already committed database rows untouched.
            readiness.RecordMigrationFailure("migration failed");
        }
    }
}
