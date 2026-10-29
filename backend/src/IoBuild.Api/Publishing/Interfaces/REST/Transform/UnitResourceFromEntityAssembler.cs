using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Interfaces.REST.Resources;

namespace IoBuild.Api.Publishing.Interfaces.REST.Transform;

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
            entity.Status);
    }
}
