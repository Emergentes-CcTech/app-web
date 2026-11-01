namespace IoBuild.Api.Subscriptions.Domain.Model.Aggregates;

/// <summary>
/// Agregado Subscription del contexto Subscriptions & Billing.
/// Modela el contrato anual HaaS vinculado a un condominio residencial.
/// </summary>
public sealed class Subscription
{
    // Identificador único de la suscripción
    public int Id { get; set; }

    // Identificador del condominio / proyecto suscrito al servicio
    public int ProjectId { get; set; }

    // Propiedad puente para compatibilidad con la columna física 'BuilderId' del esquema de base de datos
    public int BuilderId
    {
        get => ProjectId;
        set => ProjectId = value;
    }

    // Identificador del plan contratado (Esencial, Urbano o GranTorre)
    public int PlanId { get; set; }

    // Estado del ciclo de vida de la suscripción (active, cancelled, pending)
    public string Status { get; set; } = "active";

    // Fecha de inicio del contrato de arrendamiento tecnológico
    public DateTimeOffset StartDate { get; set; } = DateTimeOffset.UtcNow;

    // Fecha de finalización del periodo de servicio
    public DateTimeOffset? EndDate { get; set; }

    // Garantía reembolsable cobrada al activar (dos cuotas sin IGV)
    public decimal GuaranteeSoles { get; set; }

    // Cuota de activación cobrada al activar (instalación, con IGV)
    public decimal ActivationFeeSoles { get; set; }

    // Liquidación registrada al cancelar antes de terminar el contrato
    public decimal? EarlyTerminationFeeSoles { get; set; }
    public decimal? GuaranteeRefundSoles { get; set; }

    // Constructor por defecto requerido por el ORM
    public Subscription() { }

    // Constructor parametrizado para registrar una nueva suscripción
    public Subscription(int projectId, int planId, DateTimeOffset startDate, DateTimeOffset? endDate = null)
    {
        ProjectId = projectId;
        PlanId = planId;
        StartDate = startDate;
        EndDate = endDate ?? startDate.AddMonths(24);
        Status = "active";
    }

    // Activa el contrato HaaS de 24 meses con los montos cobrados en el pago de activación
    public static Subscription Activate(int projectId, Plan plan, DateTimeOffset startDate) => new()
    {
        ProjectId = projectId,
        PlanId = plan.Id,
        Status = "active",
        StartDate = startDate,
        EndDate = startDate.AddMonths(plan.ContractMonths),
        GuaranteeSoles = plan.Guarantee,
        ActivationFeeSoles = plan.ActivationFeeWithIgv
    };

    // Cancela la suscripción activa
    public void Cancel()
    {
        Status = "cancelled";
        EndDate = DateTimeOffset.UtcNow;
    }

    // Cancela registrando el saldo de equipos y la devolución de la garantía; repetir no cambia la liquidación
    public void Cancel(EarlyTerminationQuote quote)
    {
        if (Status == "cancelled") return;
        EarlyTerminationFeeSoles = quote.AmountDue;
        GuaranteeRefundSoles = quote.GuaranteeRefund;
        Cancel();
    }
}
