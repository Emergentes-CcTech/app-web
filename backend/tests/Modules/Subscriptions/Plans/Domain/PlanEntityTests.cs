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
    public void Given_esencial_plan_when_validating_capacity_then_accepts_up_to_40_units_and_3_devices()
    {
        // Dado el plan Esencial con tope de 40 departamentos y 3 equipos en comodato
        var esencial = new Plan("Esencial", "Plan HaaS Esencial", 350.00m);

        var valid = esencial.ValidateCapacity(totalUnits: 40, totalDevices: 3, out var error);
        Assert.True(valid);
        Assert.Empty(error);

        var invalidUnits = esencial.ValidateCapacity(totalUnits: 41, totalDevices: 3, out var unitError);
        Assert.False(invalidUnits);
        Assert.Contains("superando el tope máximo de 40", unitError);

        var invalidDevices = esencial.ValidateCapacity(totalUnits: 21, totalDevices: 4, out var deviceError);
        Assert.False(invalidDevices);
        Assert.Contains("superando la dotación máxima de 3 equipos", deviceError);
    }

    [Fact]
    [Trait("Category", "BDD")]
    public void Given_urbano_plan_when_validating_capacity_then_accepts_up_to_80_units_and_5_devices()
    {
        // Dado el plan Urbano con tope de 80 departamentos y 5 equipos en comodato
        var urbano = new Plan("Urbano", "Plan HaaS Urbano", 420.00m);

        Assert.True(urbano.ValidateCapacity(60, 5, out _));
        Assert.False(urbano.ValidateCapacity(85, 5, out var unitErr));
        Assert.Contains("superando el tope máximo de 80", unitErr);
        Assert.False(urbano.ValidateCapacity(60, 6, out _));
    }

    [Fact]
    [Trait("Category", "BDD")]
    public void Given_catalog_prices_without_igv_when_computing_what_the_board_pays_then_igv_is_added()
    {
        // La junta de propietarios paga el precio del plan más 18% de IGV
        Assert.Equal(413.00m, new Plan("Esencial", "", 350.00m).PriceWithIgv);
        Assert.Equal(495.60m, new Plan("Urbano", "", 420.00m).PriceWithIgv);
        Assert.Equal(767.00m, new Plan("GranTorre", "", 650.00m).PriceWithIgv);
        Assert.Equal(24, new Plan("Urbano", "", 420.00m).ContractMonths);
    }
}
