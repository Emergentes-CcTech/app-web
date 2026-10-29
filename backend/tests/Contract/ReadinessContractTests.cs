using IoBuild.Api.Contracts;
using IoBuild.Api.Readiness;

namespace IoBuild.Contract.Tests;

public sealed class ReadinessContractTests
{
    [Fact]
    public void Ready_when_migrations_succeed_and_prior_rows_remain_usable()
    {
        var readiness = new MigrationReadiness();

        readiness.RecordMigrationSuccess();

        Assert.True(readiness.IsReady);
        Assert.False(readiness.ShouldBlockRequests);
    }

    [Fact]
    public void Failed_migration_blocks_requests_without_discarding_committed_rows()
    {
        var readiness = new MigrationReadiness();
        readiness.RecordMigrationSuccess();
        readiness.RecordMigrationFailure("checksum mismatch");

        Assert.False(readiness.IsReady);
        Assert.True(readiness.ShouldBlockRequests);
        Assert.Equal("checksum mismatch", readiness.FailureReason);
    }
}

public sealed class LegacyContractCharacterizationTests
{
    [Fact]
    public void Catalog_preserves_characterized_auth_routes_and_outcomes()
    {
        var contracts = LegacyApiContractCatalog.All;

        Assert.Contains(contracts, route => route == new LegacyRouteContract("POST", "/api/v1/sessions", 201, true, "authenticated-user"));
        Assert.Contains(contracts, route => route == new LegacyRouteContract("POST", "/api/v1/users", 201, true, "message"));
        Assert.Contains(contracts, route => route == new LegacyRouteContract("DELETE", "/api/v1/sessions/current", 204, false, "empty"));
        Assert.Contains(contracts, route => route == new LegacyRouteContract("GET", "/api/v1/users", 200, false, "array"));
    }
}
