using IoBuild.Api.Shared.Application.Cutover;

namespace IoBuild.Api.Shared.Interfaces.REST;

public static class CutoverEndpoints
{
    public static void MapCutoverEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/cutover").WithTags("System & Cutover");

        group.MapGet("/status", (CutoverReadiness readiness) => readiness.ShouldBlockWrites
            ? Results.Json(new { status = "frozen", reason = readiness.FailureReason }, statusCode: StatusCodes.Status503ServiceUnavailable)
            : Results.Ok(new { status = "ready" }));
        group.MapPost("/freeze", (System.Security.Claims.ClaimsPrincipal user, CutoverReadiness readiness) =>
        {
            var role = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value ?? string.Empty;
            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) && !string.Equals(role, "Administrator", StringComparison.OrdinalIgnoreCase))
                return Results.Json(new { error = "admin_required" }, statusCode: StatusCodes.Status403Forbidden);
            readiness.Freeze();
            return Results.Ok(new { status = "frozen" });
        }).RequireAuthorization();
        group.MapPost("/stabilize", async (System.Security.Claims.ClaimsPrincipal user, ICutoverHarness harness) =>
        {
            var ok = await harness.StabilizeAsync(user);
            return ok ? Results.Ok(new { status = "ready" }) : Results.Json(new { error = "admin_required" }, statusCode: StatusCodes.Status403Forbidden);
        }).RequireAuthorization();
    }
}
