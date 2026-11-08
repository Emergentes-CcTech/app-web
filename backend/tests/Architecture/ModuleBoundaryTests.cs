using IoBuild.Api.Persistence;
using IoBuild.Modules.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Architecture.Tests;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void Host_uses_one_unified_db_context_and_module_registration_boundary()
    {
        var contextTypes = typeof(IoBuildDbContext).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(DbContext)))
            .ToArray();

        Assert.Single(contextTypes);
        Assert.Equal("IoBuildDbContext", contextTypes[0].Name);
        Assert.Equal("IoBuild.Modules.Abstractions", typeof(IModule).Assembly.GetName().Name);
    }
}
