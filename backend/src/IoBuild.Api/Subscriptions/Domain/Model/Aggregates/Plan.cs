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

    // Nombre comercial del plan: Esencial, Urbano o GranTorre
    public string Name { get; set; } = string.Empty;

    // Descripción del alcance y equipamiento del servicio en comodato
    public string Description { get; set; } = string.Empty;

    // Precio mensual en Soles peruanos sin IGV (ingreso de IoBuild)
    public decimal Price { get; set; }

    // Intervalo de facturación recurrente (mensual)
    public string Interval { get; set; } = "monthly";

    // Características técnicas y comerciales serializadas en formato JSON
    public string FeaturesJson { get; set; } = "[]";

    public const decimal IgvRate = 0.18m;

    // Monto mensual que paga la junta de propietarios (el IGV no es crédito fiscal para ella)
    [NotMapped]
    public decimal PriceWithIgv => Math.Round(Price * (1 + IgvRate), 2, MidpointRounding.AwayFromZero);

    // Límite superior de departamentos admitidos para el plan
    [NotMapped]
    public int MaxUnits => Name switch
    {
        "Esencial" => 40,
        "Urbano" => 80,
        "GranTorre" => int.MaxValue,
        _ => 80
    };

    // Límite inferior de departamentos para segmentación
    [NotMapped]
    public int MinUnits => Name switch
    {
        "Esencial" => 1,
        "Urbano" => 41,
        "GranTorre" => 81,
        _ => 1
    };

    // Total de equipos IoT suministrados en comodato según el plan
    [NotMapped]
    public int MaxDevices => GatewayHubAllowance + WaterFlowSensorAllowance + CurrentTransformerAllowance;

    // Cantidad de GatewayHub (gateway 4G con respaldo local de lecturas): uno basta para todo el edificio
    [NotMapped]
    public int GatewayHubAllowance => 1;

    // Cantidad de WaterFlowSensor (puntos de medición de agua no invasivos)
    [NotMapped]
    public int WaterFlowSensorAllowance => Name switch
    {
        "Esencial" => 1,
        "Urbano" => 2,
        "GranTorre" => 3,
        _ => 2
    };

    // Cantidad de CurrentTransformerCT (puntos de medición de energía por tablero trifásico)
    [NotMapped]
    public int CurrentTransformerAllowance => Name switch
    {
        "Esencial" => 1,
        "Urbano" => 2,
        "GranTorre" => 3,
        _ => 2
    };

    // Periodo contractual estándar: 24 meses de servicio con garantía reembolsable de 2 cuotas
    [NotMapped]
    public int ContractMonths => 24;

    [NotMapped]
    public int GuaranteeMonths => 2;

    // Costo del kit ESP32 en comodato (sin IGV): base del saldo que se cobra si el contrato termina antes
    [NotMapped]
    public decimal KitCost => Name switch
    {
        "Esencial" => 700.00m,
        "Urbano" => 1040.00m,
        "GranTorre" => 1380.00m,
        _ => 1040.00m
    };

    // Cuota única de activación: cubre la instalación (técnico + electricista), sin IGV
    [NotMapped]
    public decimal ActivationFee => Name switch
    {
        "Esencial" => 250.00m,
        "Urbano" => 300.00m,
        "GranTorre" => 400.00m,
        _ => 300.00m
    };

    [NotMapped]
    public decimal ActivationFeeWithIgv => WithIgv(ActivationFee);

    // Garantía reembolsable de dos cuotas sin IGV: es un depósito, no una venta
    [NotMapped]
    public decimal Guarantee => Price * GuaranteeMonths;

    // Pago de activación: primera cuota y cuota de activación con IGV, más la garantía
    [NotMapped]
    public decimal ActivationTotal => PriceWithIgv + ActivationFeeWithIgv + Guarantee;

    private static decimal WithIgv(decimal amount) => Math.Round(amount * (1 + IgvRate), 2, MidpointRounding.AwayFromZero);

    // Plan que corresponde al número de departamentos del condominio
    public static string NameForUnits(int totalUnits) => totalUnits switch
    {
        <= 40 => "Esencial",
        <= 80 => "Urbano",
        _ => "GranTorre"
    };

    // Saldo de equipos al terminar antes de tiempo: el kit se amortiza en partes iguales durante el contrato
    public EarlyTerminationQuote QuoteEarlyTermination(DateTimeOffset contractEnd, decimal guaranteeHeld, DateTimeOffset now)
    {
        var monthsRemaining = 0;
        if (contractEnd > now)
        {
            monthsRemaining = (contractEnd.Year - now.Year) * 12 + contractEnd.Month - now.Month;
            if (contractEnd.Day > now.Day) monthsRemaining++;
            monthsRemaining = Math.Clamp(monthsRemaining, 0, ContractMonths);
        }
        var equipmentBalance = Math.Round(KitCost * monthsRemaining / ContractMonths, 2, MidpointRounding.AwayFromZero);
        var amountDue = Math.Max(0, equipmentBalance - guaranteeHeld);
        var refund = Math.Max(0, guaranteeHeld - equipmentBalance);
        return new EarlyTerminationQuote(monthsRemaining, equipmentBalance, guaranteeHeld, amountDue, refund);
    }

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

/// <summary>Liquidación de una salida anticipada: saldo de equipos, garantía aplicada y lo que queda por pagar o devolver.</summary>
public sealed record EarlyTerminationQuote(int MonthsRemaining, decimal EquipmentBalance, decimal GuaranteeHeld, decimal AmountDue, decimal GuaranteeRefund);
