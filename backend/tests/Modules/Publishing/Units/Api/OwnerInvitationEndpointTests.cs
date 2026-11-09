using System.Net;
using System.Text;
using System.Text.Json;
using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Publishing.Units.Api;

/// <summary>
/// La administración pre-registra al propietario en su unidad; cuando él crea su cuenta con ese correo,
/// la cuenta queda vinculada al departamento y el perfil arranca con sus datos.
/// </summary>
[Trait("Context", "Publishing")]
public sealed class OwnerInvitationEndpointTests
{
    private const int Project = 930;

    private static async Task<int> SeedAsync(CoreBusinessApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
        db.Projects.Add(new Project { Id = Project, Name = "Torre Sol", BuilderId = 80 });
        var unit = new Unit(Project, "401", 4, 118m, 100m);
        db.Units.Add(unit);
        await db.SaveChangesAsync();
        return unit.Id;
    }

    private static string Admin => Token(80, "admin@example.test", "PropertyManager");
    private const string Invite = """{"firstName":"Carla","lastName":"Flores","email":"Carla.Flores@Example.test","phoneNumber":"987 654 321"}""";

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    [Fact]
    public async Task Invited_owner_signs_up_with_that_email_and_gets_the_unit_and_profile()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        var unitId = await SeedAsync(factory);

        var invite = await SendAuthorizedAsync(client, HttpMethod.Put, $"/api/v1/units/{unitId}/owner-invitation", Admin, Invite);
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);

        var lookup = JsonDocument.Parse(await client.GetStringAsync("/api/v1/authentication/invitation?email=carla.flores@example.test")).RootElement;
        Assert.True(lookup.GetProperty("assigned").GetBoolean());
        var invited = Assert.Single(lookup.GetProperty("units").EnumerateArray());
        Assert.Equal("401", invited.GetProperty("unitNumber").GetString());
        Assert.Equal("Torre Sol", invited.GetProperty("condominiumName").GetString());
        Assert.False(lookup.TryGetProperty("fullName", out _));

        var residents = JsonDocument.Parse(await (await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/residents?condominiumId={Project}", Admin)).Content.ReadAsStringAsync()).RootElement;
        var pending = Assert.Single(residents.EnumerateArray());
        Assert.Equal("pending", pending.GetProperty("accountStatus").GetString());
        Assert.Equal("Carla Flores", pending.GetProperty("fullName").GetString());
        Assert.Equal("987 654 321", pending.GetProperty("phoneNumber").GetString());

        var signUp = await client.PostAsync("/api/v1/authentication/sign-up", Json("""{"email":"carla.flores@example.test","password":"secret123","role":"Resident"}"""));
        Assert.Equal(HttpStatusCode.Created, signUp.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
        var user = await db.IamUsers.SingleAsync(u => u.Email == "carla.flores@example.test");
        Assert.Equal(user.Id, (await db.Units.SingleAsync(u => u.Id == unitId)).OwnerId);
        var profile = await db.Profiles.SingleAsync(p => p.UserId == user.Id);
        Assert.Equal("Carla Flores", profile.Name);
        Assert.Equal("987 654 321", profile.PhoneNumber);

        var again = await SendAuthorizedAsync(client, HttpMethod.Put, $"/api/v1/units/{unitId}/owner-invitation", Admin, Invite);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Rejects_bad_data_existing_accounts_and_strangers_and_can_be_withdrawn()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        var unitId = await SeedAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
            db.IamUsers.Add(new IoBuild.Api.IAM.Domain.Model.Aggregates.IamUser { Email = "taken@example.test", PasswordHash = "x", Role = "Resident" });
            await db.SaveChangesAsync();
        }

        var invalid = await SendAuthorizedAsync(client, HttpMethod.Put, $"/api/v1/units/{unitId}/owner-invitation", Admin,
            """{"firstName":"C","lastName":"Flores","email":"no-es-correo","phoneNumber":"abc"}""");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);

        var taken = await SendAuthorizedAsync(client, HttpMethod.Put, $"/api/v1/units/{unitId}/owner-invitation", Admin,
            """{"firstName":"Ana","lastName":"Ruiz","email":"taken@example.test","phoneNumber":"987654321"}""");
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);

        var stranger = await SendAuthorizedAsync(client, HttpMethod.Put, $"/api/v1/units/{unitId}/owner-invitation", Token(81, "x@example.test", "PropertyManager"), Invite);
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await SendAuthorizedAsync(client, HttpMethod.Put, $"/api/v1/units/{unitId}/owner-invitation", Admin, Invite)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAuthorizedAsync(client, HttpMethod.Delete, $"/api/v1/units/{unitId}/owner-invitation", Admin)).StatusCode);

        var lookup = JsonDocument.Parse(await client.GetStringAsync("/api/v1/authentication/invitation?email=carla.flores@example.test")).RootElement;
        Assert.False(lookup.GetProperty("assigned").GetBoolean());
        using var check = factory.Services.CreateScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<IoBuildDbContext>().Clients.AnyAsync(c => c.UnitId == unitId));
    }
}
