namespace IoBuild.Api.Subscriptions.Domain.Model.Aggregates;

public sealed class Plan
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Interval { get; set; } = "monthly";
    public string FeaturesJson { get; set; } = "[]";

    public Plan() { }

    public Plan(string name, string description, decimal price, string interval = "monthly", string featuresJson = "[]")
    {
        Name = name;
        Description = description;
        Price = price;
        Interval = interval;
        FeaturesJson = featuresJson;
    }

    public void Update(string name, string description, decimal price, string interval, string featuresJson)
    {
        Name = name;
        Description = description;
        Price = price;
        Interval = interval;
        FeaturesJson = featuresJson;
    }
}
