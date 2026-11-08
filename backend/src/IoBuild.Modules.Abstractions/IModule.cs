using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IoBuild.Modules.Abstractions;

public interface IModule
{
    string Name { get; }
    void Register(IServiceCollection services);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
