namespace IoBuild.Api.Subscriptions.Interfaces.REST.Resources;

public record PlanResource(int Id, string Name, string Description, decimal Price, string Interval, string FeaturesJson)
{
    // Montos del contrato HaaS que la pantalla de activación muestra antes de pagar
    public decimal PriceWithIgv { get; init; }
    public decimal PrepaidLastMonthWithIgv { get; init; }
    public decimal ActivationTotal { get; init; }
    public int ContractMonths { get; init; }
    public int MinUnits { get; init; }
    public int MaxUnits { get; init; }

    public IReadOnlyList<string> Features
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FeaturesJson)) return [];
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(FeaturesJson) ?? [];
            }
            catch (System.Text.Json.JsonException)
            {
                return [];
            }
        }
    }
}
public record CreatePlanResource(string Name, string Description, decimal Price, string Interval = "monthly", string FeaturesJson = "[]");
public record UpdatePlanResource(string Name, string Description, decimal Price, string Interval, string FeaturesJson);
