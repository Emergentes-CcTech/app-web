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

    // Identificador del plan contratado (Boutique, Urbano o GranTorre)
    public int PlanId { get; set; }

    // Estado del ciclo de vida de la suscripción (active, cancelled, pending)
    public string Status { get; set; } = "active";

    // Fecha de inicio del contrato de arrendamiento tecnológico
    public DateTimeOffset StartDate { get; set; } = DateTimeOffset.UtcNow;

    // Fecha de finalización del periodo de servicio
    public DateTimeOffset? EndDate { get; set; }

    // Constructor por defecto requerido por el ORM
    public Subscription() { }

    // Constructor parametrizado para registrar una nueva suscripción
    public Subscription(int projectId, int planId, DateTimeOffset startDate, DateTimeOffset? endDate = null)
    {
        ProjectId = projectId;
        PlanId = planId;
        StartDate = startDate;
        EndDate = endDate ?? startDate.AddMonths(12);
        Status = "active";
    }

    // Cancela la suscripción activa
    public void Cancel()
    {
        Status = "cancelled";
        EndDate = DateTimeOffset.UtcNow;
    }
}
