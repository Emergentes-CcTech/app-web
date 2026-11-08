namespace IoBuild.Api.Shared.Interfaces.ASP.Configuration;

/// <summary>
/// Shared ASP.NET configuration helpers.
/// - Kebab-case / snake_case route conventions are documented here for future
///   BCs to reuse. Current routes preserve legacy wire contracts
///   (/api/v1/*, kebab-free) to avoid breaking frontend / simulator.
/// - CORS, ForwardedHeaders, and JSON options are configured centrally in
///   Program.cs and remain unchanged.
/// </summary>
public static class RouteConfiguration
{
    public const string ApiPrefix = "/api/v1";
    public const string HealthPath = "/health";
}
