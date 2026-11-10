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

    // Periodo contractual estándar: 24 meses de servicio; al activar se pagan el primer y el último mes
    [NotMapped]
    public int ContractMonths => 24;

    // Costo del kit ESP32 en comodato (sin IGV): base del saldo que se cobra si el contrato termina antes
    [NotMapped]
    public decimal KitCost => Name switch
    {
        "Esencial" => 700.00m,
        "Urbano" => 1040.00m,
        "GranTorre" => 1380.00m,
        _ => 1040.00m
    };

    // La instalación corre por cuenta de IoBuild. En su lugar se cobra por adelantado el último mes del contrato,
    // que respalda el comodato: si se cancela antes, se aplica al saldo de los equipos
    [NotMapped]
    public decimal PrepaidLastMonthWithIgv => PriceWithIgv;

    // Pago de activación: primer mes y último mes (mes 24), ambos con IGV
    [NotMapped]
    public decimal ActivationTotal => PriceWithIgv + PrepaidLastMonthWithIgv;

    // Cada equipo común por encima del kit del plan suma una cuota mensual propia (sin IGV)
    public const decimal ExtraDeviceMonthlyPrice = 30.00m;
    public static decimal ExtraDeviceMonthlyPriceWithIgv => WithIgv(ExtraDeviceMonthlyPrice);

    public int ExtraDevices(int communalDevices) => Math.Max(0, communalDevices - MaxDevices);

    // Cuota mensual con los equipos adicionales del condominio
    public decimal MonthlyPriceWith(int communalDevices) => Price + ExtraDevices(communalDevices) * ExtraDeviceMonthlyPrice;
    public decimal MonthlyPriceWithIgvWith(int communalDevices) => PriceWithIgv + ExtraDevices(communalDevices) * ExtraDeviceMonthlyPriceWithIgv;

    // Pago de activación o reactivación con los equipos adicionales: primer y último mes, cada uno con sus extras
    public decimal ActivationTotalWith(int communalDevices) => 2 * MonthlyPriceWithIgvWith(communalDevices);

    private static decimal WithIgv(decimal amount) => Math.Round(amount * (1 + IgvRate), 2, MidpointRounding.AwayFromZero);

    // Plan que corresponde al número de departamentos del condominio
    public static string NameForUnits(int totalUnits) => totalUnits switch
    {
        <= 40 => "Esencial",
        <= 80 => "Urbano",
        _ => "GranTorre"
    };

    // Saldo de equipos al terminar antes de tiempo: el kit se amortiza en partes iguales durante el contrato.
    // El mes 24 adelantado se aplica al saldo mientras no se haya llegado a él; una garantía de contratos anteriores también
    public EarlyTerminationQuote QuoteEarlyTermination(DateTimeOffset contractEnd, decimal prepaidLastMonth, decimal guaranteeHeld, DateTimeOffset now)
    {
        var monthsRemaining = 0;
        if (contractEnd > now)
        {
            monthsRemaining = (contractEnd.Year - now.Year) * 12 + contractEnd.Month - now.Month;
            if (contractEnd.Day > now.Day) monthsRemaining++;
            monthsRemaining = Math.Clamp(monthsRemaining, 0, ContractMonths);
        }
        var equipmentBalance = Math.Round(KitCost * monthsRemaining / ContractMonths, 2, MidpointRounding.AwayFromZero);
        // En el último mes el pago adelantado ya está cubriendo el servicio: deja de ser un saldo a favor
        var credit = (monthsRemaining >= 2 ? prepaidLastMonth : 0m) + guaranteeHeld;
        var amountDue = Math.Max(0, equipmentBalance - credit);
        var refund = Math.Max(0, credit - equipmentBalance);
        return new EarlyTerminationQuote(monthsRemaining, equipmentBalance, credit, amountDue, refund);
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

    // Valida que el condominio entre en el plan por su número de departamentos.
    // Los equipos por encima del kit no bloquean: se cobran como equipos adicionales
    public bool ValidateCapacity(int totalUnits, out string reason)
    {
        // Valida que el número de departamentos no sobrepase el tope contratado
        if (totalUnits > MaxUnits)
        {
            reason = $"El condominio registra {totalUnits} departamentos, superando el tope máximo de {MaxUnits} departamentos del Plan {Name}.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}

/// <summary>Liquidación de una salida anticipada: saldo de equipos, saldo a favor aplicado y lo que queda por pagar o devolver.</summary>
public sealed record EarlyTerminationQuote(int MonthsRemaining, decimal EquipmentBalance, decimal CreditApplied, decimal AmountDue, decimal Refund);
