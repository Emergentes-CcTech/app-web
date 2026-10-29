using IoBuild.Api.Subscriptions.Domain.Model.Aggregates;

namespace IoBuild.Modules.Tests.Subscriptions.Plans.Domain;

[Trait("Context", "Subscriptions")]
[Trait("Capability", "Plans")]
[Trait("Layer", "Domain")]
[Trait("Dependency", "None")]
public sealed class PlanEntityTests
{
    [Fact]
    [Trait("Category", "BDD")]
    public void Given_a_plan_name_description_and_price_when_created_then_monthly_interval_and_empty_features_are_defaulted()
    {
        var plan = new Plan("Starter", "For small projects", 29.99m);

        Assert.Equal("Starter", plan.Name);
        Assert.Equal("For small projects", plan.Description);
        Assert.Equal(29.99m, plan.Price);
        Assert.Equal("monthly", plan.Interval);
        Assert.Equal("[]", plan.FeaturesJson);
    }

    [Fact]
    [Trait("Category", "BDD")]
    public void Given_an_existing_plan_when_plan_details_are_updated_then_new_values_are_kept()
    {
        var plan = new Plan("Starter", "For small projects", 29.99m);

        plan.Update("Professional", "For growing projects", 89.99m, "yearly", "[\"Analytics\"]");

        Assert.Equal("Professional", plan.Name);
        Assert.Equal("For growing projects", plan.Description);
        Assert.Equal(89.99m, plan.Price);
        Assert.Equal("yearly", plan.Interval);
        Assert.Equal("[\"Analytics\"]", plan.FeaturesJson);
    }

    [Fact]
    [Trait("Category", "BDD")]
    public void Given_boutique_plan_when_validating_capacity_then_accepts_up_to_20_units_and_4_devices()
    {
        // Dado el plan Boutique con tope de 20 departamentos y 4 equipos en comodato
        var boutique = new Plan("Boutique", "Plan HaaS Boutique", 500.00m);

        var valid = boutique.ValidateCapacity(totalUnits: 20, totalDevices: 4, out var error);
        Assert.True(valid);
        Assert.Empty(error);

        var invalidUnits = boutique.ValidateCapacity(totalUnits: 25, totalDevices: 4, out var unitError);
        Assert.False(invalidUnits);
        Assert.Contains("superando el tope máximo de 20", unitError);

        var invalidDevices = boutique.ValidateCapacity(totalUnits: 15, totalDevices: 5, out var deviceError);
        Assert.False(invalidDevices);
        Assert.Contains("superando la dotación máxima de 4 equipos", deviceError);
    }

    [Fact]
    [Trait("Category", "BDD")]
    public void Given_urbano_plan_when_validating_capacity_then_accepts_up_to_80_units_and_6_devices()
    {
        // Dado el plan Urbano con tope de 80 departamentos y 6 equipos en comodato
        var urbano = new Plan("Urbano", "Plan HaaS Urbano", 700.00m);

        Assert.True(urbano.ValidateCapacity(25, 6, out _));
        Assert.False(urbano.ValidateCapacity(85, 6, out var unitErr));
        Assert.Contains("superando el tope máximo de 80", unitErr);
    }
}
