using IoBuild.Api.Proration.Domain.Model.Aggregates;

namespace IoBuild.Api.Proration.Domain.Services;

/// <summary>
/// DTO con indicadores financieros y tasa de morosidad del condominio para un periodo.
/// </summary>
public sealed record BillingPeriodMetricsDto(
    int BillingPeriodId,
    string PeriodCode,
    decimal TotalBilledSoles,
    decimal TotalCollectedSoles,
    decimal TotalOverdueSoles,
    decimal DelinquencyRate,
    int TotalStatements,
    int PaidStatements,
    int PendingStatements,
    int OverdueStatements);

/// <summary>
/// Contrato del servicio de dominio para la conciliación de recibos matrices y el prorrateo financiero legal.
/// </summary>
public interface IProrationSettlementService
{
    // Registra los recibos matrices de Sedapal y Luz del Sur, conciliándolos contra las mediciones de áreas comunes
    Task<BillingPeriod> ReconcileMacroBillsAsync(
        int projectId,
        string periodCode,
        DateTime startDate,
        DateTime endDate,
        DateTime dueDate,
        decimal macroWaterM3,
        decimal macroWaterAmountSoles,
        string? waterReceiptImageUrl,
        decimal macroEnergyKwh,
        decimal macroEnergyAmountSoles,
        string? energyReceiptImageUrl,
        decimal otherExpensesSoles,
        CancellationToken ct = default);

    // Ejecuta la liquidación por departamento aplicando la alícuota legal (Ley N.° 27157) con cuadre de céntimos
    Task<List<StatementOfAccount>> GenerateSettlementAsync(int billingPeriodId, CancellationToken ct = default);

    // Registra el abono de una cuota de mantenimiento y actualiza la tasa de morosidad del periodo
    Task<StatementOfAccount> RegisterPaymentAsync(
        int statementId,
        decimal amount,
        string paymentMethod,
        string paymentReference,
        CancellationToken ct = default);

    // Consulta los indicadores financieros y de cobranza del periodo
    Task<BillingPeriodMetricsDto> GetPeriodMetricsAsync(int billingPeriodId, CancellationToken ct = default);
}
