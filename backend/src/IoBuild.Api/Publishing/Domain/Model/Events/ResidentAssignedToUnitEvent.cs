namespace IoBuild.Api.Publishing.Domain.Model.Events;

/// <summary>
/// Evento de dominio emitido cuando se registra o asigna un residente a una unidad privativa en Catastro.
/// Notifica al contexto IAM para ejecutar la auto-vinculación de credenciales, proyecto y alícuota legal.
/// </summary>
public sealed record ResidentAssignedToUnitEvent(
    int ProjectId,
    int UnitId,
    string UnitNumber,
    string ResidentEmail,
    decimal AliquotPercentage
);
