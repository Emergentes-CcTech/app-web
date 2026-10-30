using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using IoBuild.Api.Publishing.Interfaces.REST.Resources;

namespace IoBuild.Api.Publishing.Interfaces.REST.Transform;

/// <summary>
/// Ensamblador de transformación desde la entidad Unit hacia el recurso UnitResource.
/// Mapea los atributos de alícuota legal y residente para consumo de clientes BFF.
/// </summary>
public static class UnitResourceFromEntityAssembler
{
    public static UnitResource ToResourceFromEntity(Unit entity)
    {
        return new UnitResource(
            entity.Id,
            entity.ProjectId,
            entity.UnitNumber,
            entity.Floor,
            entity.RoomNumber,
            entity.OwnerEmail,
            entity.OwnerId,
            entity.Status,
            entity.AreaM2,
            entity.AliquotPercentage,
            entity.ResidentEmail,
            entity.ResidentFullName,
            entity.AccountStatus,
            entity.ProjectId,
            entity.UnitNumber,
            entity.AliquotPercentage,
            entity.OwnerId,
            entity.OwnerId,
            "apartment"
        );
    }
}
