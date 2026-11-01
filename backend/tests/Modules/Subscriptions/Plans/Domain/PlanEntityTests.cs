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

    [Fact]
    [Trait("Category", "BDD")]
    public void Given_the_esencial_plan_when_activating_then_first_fee_activation_and_guarantee_are_charged()
    {
        // Primera cuota S/ 413.00 + activación S/ 295.00 (S/ 250 + IGV) + garantía S/ 700.00 (2 cuotas sin IGV)
        var esencial = new Plan("Esencial", "", 350.00m);
        Assert.Equal(295.00m, esencial.ActivationFeeWithIgv);
        Assert.Equal(700.00m, esencial.Guarantee);
        Assert.Equal(1408.00m, esencial.ActivationTotal);

        var start = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var subscription = Subscription.Activate(10, esencial, start);
        Assert.Equal(start.AddMonths(24), subscription.EndDate);
        Assert.Equal(700.00m, subscription.GuaranteeSoles);
        Assert.Equal(295.00m, subscription.ActivationFeeSoles);
    }

    [Theory]
    [Trait("Category", "BDD")]
    [InlineData(2, 22, 953.33, 113.33, 0)]   // Urbano cancelado en el mes 2: debe el saldo de equipos menos la garantía
    [InlineData(20, 4, 173.33, 0, 666.67)]   // Cerca del final: la garantía cubre el saldo y se devuelve la diferencia
    [InlineData(24, 0, 0, 0, 840)]           // Contrato cumplido: no hay saldo y la garantía se devuelve completa
    public void Given_an_urbano_contract_when_cancelling_then_the_equipment_balance_is_settled_against_the_guarantee(
        int monthsElapsed, int expectedRemaining, double expectedBalance, double expectedDue, double expectedRefund)
    {
        var urbano = new Plan("Urbano", "", 420.00m);
        var start = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var quote = urbano.QuoteEarlyTermination(start.AddMonths(24), urbano.Guarantee, start.AddMonths(monthsElapsed));

        Assert.Equal(expectedRemaining, quote.MonthsRemaining);
        Assert.Equal((decimal)expectedBalance, quote.EquipmentBalance);
        Assert.Equal((decimal)expectedDue, quote.AmountDue);
        Assert.Equal((decimal)expectedRefund, quote.GuaranteeRefund);
    }

    [Theory]
    [Trait("Category", "BDD")]
    [InlineData(21, "Esencial")]
    [InlineData(40, "Esencial")]
    [InlineData(41, "Urbano")]
    [InlineData(80, "Urbano")]
    [InlineData(81, "GranTorre")]
    public void Given_the_number_of_units_when_assigning_the_plan_then_it_follows_the_catalog_ranges(int units, string expected)
    {
        Assert.Equal(expected, Plan.NameForUnits(units));
    }
}
