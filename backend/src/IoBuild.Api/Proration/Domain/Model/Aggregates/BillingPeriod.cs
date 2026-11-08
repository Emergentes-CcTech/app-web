namespace IoBuild.Api.Proration.Domain.Model.Aggregates;

/// <summary>
/// Periodo mensual de facturación, conciliación de recibos matrices y cierre comunal del condominio.
/// </summary>
public sealed class BillingPeriod
{
    // Identificador único del periodo de facturación
    public int Id { get; set; }

    // Identificador del condominio
    public int ProjectId { get; set; }

    // Código representativo del periodo (ej. "2026-10")
    public string PeriodCode { get; set; } = string.Empty;

    // Fecha de inicio de la facturación
    public DateTime StartDate { get; set; }

    // Fecha de fin de la facturación
    public DateTime EndDate { get; set; }

    // Fecha de vencimiento límite para el pago sin mora
    public DateTime DueDate { get; set; }

    // Datos del Recibo Matriz Oficial de Agua (Sedapal)
    public decimal MacroWaterM3 { get; set; }
    public decimal MacroWaterAmountSoles { get; set; }
    public string? WaterReceiptImageUrl { get; set; }

    // Datos del Recibo Matriz Oficial de Energía (Luz del Sur)
    public decimal MacroEnergyKwh { get; set; }
    public decimal MacroEnergyAmountSoles { get; set; }
    public string? EnergyReceiptImageUrl { get; set; }

    // Consumos medidos acumulados por los sensores IoT en áreas comunes
    public decimal MeasuredCommonWaterM3 { get; set; }
    public decimal MeasuredCommonEnergyKwh { get; set; }

    // Brecha o pérdida no justificada entre el recibo matriz y la medición comunal
    public decimal WaterLossM3 { get; set; }
    public decimal EnergyLossKwh { get; set; }

    // Otros gastos comunes autorizados (vigilancia, limpieza, consumibles)
    public decimal OtherCommonExpensesSoles { get; set; }

    // Gasto total común a liquidar entre los condóminos según alícuotas
    public decimal TotalCommonExpenseSoles { get; set; }

    // Estado del periodo: Draft, Reconciled, Settled, Closed
    public string Status { get; set; } = "Draft";

    // Momento en que se completó la conciliación de recibos matrices con IoT
    public DateTime? ReconciledAtUtc { get; set; }

    // Momento en que se emitió la liquidación formal por departamentos
    public DateTime? SettledAtUtc { get; set; }

    // Indicadores financieros de cobranza y morosidad
    public decimal TotalBilledSoles { get; set; }
    public decimal TotalCollectedSoles { get; set; }
    public decimal DelinquencyRate { get; set; }
}
