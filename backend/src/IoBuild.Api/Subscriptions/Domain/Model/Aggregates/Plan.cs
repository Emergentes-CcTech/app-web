using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace IoBuild.Api.Subscriptions.Domain.Model.Aggregates;

/// <summary>
/// Agregado Plan del contexto de Subscriptions & Billing.
/// Modela los planes HaaS (Hardware-as-a-Service en comodato) para áreas comunes de condominios.
/// </summary>
public sealed class Plan
{
    // Identificador único del plan
    public int Id { get; set; }

    // Nombre comercial del plan: Boutique, Urbano o GranTorre
    public string Name { get; set; } = string.Empty;

    // Descripción del alcance y equipamiento del servicio en comodato
    public string Description { get; set; } = string.Empty;

    // Precio mensual en Soles peruanos con IGV incluido
    public decimal Price { get; set; }

    // Intervalo de facturación recurrente (mensual)
    public string Interval { get; set; } = "monthly";

    // Características técnicas y comerciales serializadas en formato JSON
    public string FeaturesJson { get; set; } = "[]";

    // Límite superior de departamentos admitidos para el plan
    [NotMapped]
    public int MaxUnits => Name switch
    {
        "Boutique" => 20,
        "Urbano" => 80,
        "GranTorre" => int.MaxValue,
        _ => 80
    };

    // Límite inferior de departamentos para segmentación
    [NotMapped]
    public int MinUnits => Name switch
    {
        "Boutique" => 1,
        "Urbano" => 21,
        "GranTorre" => 81,
        _ => 1
    };

    // Total de equipos IoT suministrados en comodato según el plan
    [NotMapped]
    public int MaxDevices => Name switch
    {
        "Boutique" => 4,
        "Urbano" => 6,
        "GranTorre" => 10,
        _ => 6
    };

    // Cantidad de GatewayHub (concentradores con buffer offline 72h y 4G)
    [NotMapped]
    public int GatewayHubAllowance => Name switch
    {
        "Boutique" => 1,
        "Urbano" => 1,
        "GranTorre" => 2,
        _ => 1
    };

    // Cantidad de WaterFlowSensor (caudalímetros de pulsos en cisterna)
    [NotMapped]
    public int WaterFlowSensorAllowance => Name switch
    {
        "Boutique" => 1,
        "Urbano" => 2,
        "GranTorre" => 3,
        _ => 2
    };

    // Cantidad de CurrentTransformerCT (pinzas amperimétricas en tableros)
    [NotMapped]
    public int CurrentTransformerAllowance => Name switch
    {
        "Boutique" => 2,
        "Urbano" => 3,
        "GranTorre" => 5,
        _ => 3
    };

    // Periodo contractual estándar: 12 meses de servicio + 2 meses de garantía
    [NotMapped]
    public int ContractMonths => 12;

    [NotMapped]
    public int GuaranteeMonths => 2;

    public Plan() { }

    public Plan(string name, string description, decimal price, string interval = "monthly", string featuresJson = "[]")
    {
        Name = name;
        Description = description;
        Price = price;
        Interval = interval;
        FeaturesJson = featuresJson;
    }

    // Actualiza la configuración comercial del plan
    public void Update(string name, string description, decimal price, string interval, string featuresJson)
    {
        Name = name;
        Description = description;
        Price = price;
        Interval = interval;
        FeaturesJson = featuresJson;
    }

    // Valida si el condominio cumple con las restricciones de departamentos y equipos IoT del plan
    public bool ValidateCapacity(int totalUnits, int totalDevices, out string reason)
    {
        // Valida que el número de departamentos no sobrepase el tope contratado
        if (totalUnits > MaxUnits)
        {
            reason = $"El condominio registra {totalUnits} departamentos, superando el tope máximo de {MaxUnits} departamentos del Plan {Name}.";
            return false;
        }

        // Valida que la cantidad de sensores solicitados no supere la dotación en comodato
        if (totalDevices > MaxDevices)
        {
            reason = $"Se solicitan {totalDevices} equipos IoT, superando la dotación máxima de {MaxDevices} equipos en comodato del Plan {Name}.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
