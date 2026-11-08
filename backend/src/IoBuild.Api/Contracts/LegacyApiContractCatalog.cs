namespace IoBuild.Api.Contracts;

public sealed record LegacyRouteContract(
    string Method,
    string Path,
    int SuccessStatusCode,
    bool AllowsAnonymous,
    string JsonShape);

public static class LegacyApiContractCatalog
{
    public static IReadOnlyList<LegacyRouteContract> All { get; } =
    [
        new("POST", "/api/v1/sessions", 201, true, "authenticated-user"),
        new("POST", "/api/v1/authentication/sign-in", 201, true, "authenticated-user"),
        new("POST", "/api/v1/users", 201, true, "message"),
        new("POST", "/api/v1/authentication/sign-up", 201, true, "message"),
        new("DELETE", "/api/v1/sessions/current", 204, false, "empty"),
        new("POST", "/api/v1/authentication/sign-out", 200, false, "message"),
        new("GET", "/api/v1/users", 200, false, "array"),
        new("GET", "/api/v1/units", 200, false, "array"),
        new("POST", "/api/v1/units", 201, false, "unit"),
        new("GET", "/api/v1/clients", 200, false, "array"),
        new("POST", "/api/v1/clients", 201, false, "client"),
        new("GET", "/api/v1/plans", 200, true, "array"),
        new("POST", "/api/v1/plans", 201, false, "plan")
    ];
}
