using IoBuild.Api.Persistence;
using IoBuild.Api.Subscriptions.Interfaces.REST.Resources;

namespace IoBuild.Api.Subscriptions.Interfaces.REST.Transform;

public static class PlanResourceFromEntityAssembler
{
    public static PlanResource ToResourceFromEntity(Plan entity)
    {
        return new PlanResource(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.Price,
            entity.Interval,
            entity.FeaturesJson);
    }
}
