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
            entity.FeaturesJson)
        {
            PriceWithIgv = entity.PriceWithIgv,
            ActivationFee = entity.ActivationFee,
            ActivationFeeWithIgv = entity.ActivationFeeWithIgv,
            Guarantee = entity.Guarantee,
            ActivationTotal = entity.ActivationTotal,
            ContractMonths = entity.ContractMonths,
            MinUnits = entity.MinUnits,
            // GranTorre no tiene tope comercial; se expone el límite técnico de registro (500)
            MaxUnits = Math.Min(entity.MaxUnits, 500)
        };
    }
}
