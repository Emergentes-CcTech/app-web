namespace IoBuild.Api.Proration.Domain.Model.Aggregates;

/// <summary>
/// Estado de cuenta o recibo individual de mantenimiento liquidado a un departamento según su alícuota legal.
/// </summary>
public sealed class StatementOfAccount
{
    // Identificador único del recibo individual
    public int Id { get; set; }

    // Identificador del periodo de facturación vinculado
    public int BillingPeriodId { get; set; }

    // Identificador del condominio
    public int ProjectId { get; set; }

    // Identificador de la unidad departamental
    public int UnitId { get; set; }

    // Número de departamento (ej. "101", "402")
    public string UnitNumber { get; set; } = string.Empty;

    // Nombre completo del residente o titular
    public string ResidentFullName { get; set; } = string.Empty;

    // Correo electrónico del residente registrado en el catastro
    public string ResidentEmail { get; set; } = string.Empty;

    // Porcentaje legal de participación en áreas comunes (Ley N.° 27157)
    public decimal AliquotPercentage { get; set; }

    // Desglose proporcional de gastos comunes asignados a esta unidad
    public decimal IndividualWaterSoles { get; set; }
    public decimal IndividualEnergySoles { get; set; }
    public decimal IndividualOtherExpensesSoles { get; set; }

    // Importe total liquidado en Soles para este departamento
    public decimal IndividualAmountSoles { get; set; }

    // Monto acumulado de abonos recibidos en Soles
    public decimal PaidAmountSoles { get; set; }

    // Saldo pendiente de pago en Soles
    public decimal BalanceSoles { get; set; }

    // Estado del cobro: Pending, PartiallyPaid, Paid, Overdue
    public string Status { get; set; } = "Pending";

    // Momento en que se completó el pago total
    public DateTime? PaidAtUtc { get; set; }

    // Código de operación bancaria o comprobante de pago
    public string? PaymentReference { get; set; }

    // Canal o medio de abono (Transferencia, BCP, BBVA, POS)
    public string? PaymentMethod { get; set; }
}
