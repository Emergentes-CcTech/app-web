using IoBuild.Api.Publishing.Application.Internal.CommandServices;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;

namespace IoBuild.Modules.Tests.Publishing.Units.Domain;

[Trait("Context", "Publishing")]
[Trait("Capability", "Units")]
[Trait("Layer", "Domain")]
[Trait("Dependency", "None")]
public sealed class UnitEntityTests
{
    [Fact]
    [Trait("Category", "BDD")]
    public void Given_a_unit_without_a_room_number_when_created_then_unit_number_is_used_as_room_number()
    {
        var unit = new Unit(projectId: 7, unitNumber: "A-101");

        Assert.Equal(7, unit.ProjectId);
        Assert.Equal("A-101", unit.UnitNumber);
        Assert.Equal("A-101", unit.RoomNumber);
        Assert.Equal("available", unit.Status);
        Assert.Equal("Al día", unit.AccountStatus);
    }

    [Fact]
    [Trait("Category", "BDD")]
    public void Given_an_available_unit_when_an_owner_is_assigned_then_unit_becomes_occupied()
    {
        var unit = new Unit(projectId: 7, unitNumber: "A-101");

        unit.AssignOwner("owner@example.com", ownerId: 42);

        Assert.Equal("owner@example.com", unit.OwnerEmail);
        Assert.Equal("owner@example.com", unit.ResidentEmail);
        Assert.Equal(42, unit.OwnerId);
        Assert.Equal("occupied", unit.Status);
        Assert.Equal("Al día", unit.AccountStatus);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "BDD")]
    public void Given_an_occupied_unit_when_owner_email_is_cleared_then_unit_becomes_available(string? email)
    {
        var unit = new Unit(projectId: 7, unitNumber: "A-101");
        unit.AssignOwner("owner@example.com", ownerId: 42);

        unit.AssignOwner(email);

        Assert.Null(unit.OwnerEmail);
        Assert.Null(unit.OwnerId);
        Assert.Equal("available", unit.Status);
        Assert.Equal("Sin asignar", unit.AccountStatus);
    }

    [Fact]
    [Trait("Category", "BDD")]
    public void Given_condominium_aliquots_summing_exactly_100_percent_when_validated_then_accepted()
    {
        // Dado un conjunto de 25 departamentos cuyas alícuotas suman exactamente 100.0000%
        var aliquots = new List<decimal>();
        for (int i = 0; i < 24; i++)
        {
            aliquots.Add(4.0000m);
        }
        aliquots.Add(4.0000m); // 25 * 4.0000 = 100.0000%

        bool isValid = ProjectCommandService.ValidateAliquotSum(aliquots, out var error);

        Assert.True(isValid);
        Assert.Empty(error);
    }

    [Fact]
    [Trait("Category", "BDD")]
    public void Given_condominium_aliquots_not_summing_100_percent_when_validated_then_rejected_under_law_27157()
    {
        // Dado un conjunto de departamentos que suma 98.5000% (incompleto)
        var aliquots = new List<decimal> { 30.0000m, 30.0000m, 38.5000m };

        bool isValid = ProjectCommandService.ValidateAliquotSum(aliquots, out var error);

        Assert.False(isValid);
        Assert.Contains("debe ser exactamente 100.0000% (Ley N.° 27157)", error);
        Assert.Contains("98.5000%", error);
    }
}
