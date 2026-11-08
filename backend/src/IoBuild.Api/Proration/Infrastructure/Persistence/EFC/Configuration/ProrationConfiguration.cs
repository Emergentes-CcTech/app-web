using IoBuild.Api.Proration.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Proration.Infrastructure.Persistence.EFC.Configuration;

/// <summary>
/// Configuración de Entity Framework Core para las entidades del contexto de Prorrateo y Liquidación (Proration).
/// Mapea los agregados de periodos de facturación y estados de cuenta individuales bajo la Ley N.° 27157.
/// </summary>
public static class ProrationConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BillingPeriod>(entity =>
        {
            entity.ToTable("billing_periods");
            entity.HasKey(period => period.Id);
            entity.Property(period => period.PeriodCode).HasMaxLength(32).IsRequired();
            entity.Property(period => period.Status).HasMaxLength(32).IsRequired();
            entity.Property(period => period.WaterReceiptImageUrl).HasMaxLength(500);
            entity.Property(period => period.EnergyReceiptImageUrl).HasMaxLength(500);
            entity.Property(period => period.MacroWaterM3).HasPrecision(18, 4);
            entity.Property(period => period.MacroWaterAmountSoles).HasPrecision(18, 2);
            entity.Property(period => period.MacroEnergyKwh).HasPrecision(18, 4);
            entity.Property(period => period.MacroEnergyAmountSoles).HasPrecision(18, 2);
            entity.Property(period => period.MeasuredCommonWaterM3).HasPrecision(18, 4);
            entity.Property(period => period.MeasuredCommonEnergyKwh).HasPrecision(18, 4);
            entity.Property(period => period.WaterLossM3).HasPrecision(18, 4);
            entity.Property(period => period.EnergyLossKwh).HasPrecision(18, 4);
            entity.Property(period => period.OtherCommonExpensesSoles).HasPrecision(18, 2);
            entity.Property(period => period.TotalCommonExpenseSoles).HasPrecision(18, 2);
            entity.Property(period => period.TotalBilledSoles).HasPrecision(18, 2);
            entity.Property(period => period.TotalCollectedSoles).HasPrecision(18, 2);
            entity.Property(period => period.DelinquencyRate).HasPrecision(5, 2);

            entity.HasIndex(period => new { period.ProjectId, period.PeriodCode }).IsUnique();
        });

        modelBuilder.Entity<StatementOfAccount>(entity =>
        {
            entity.ToTable("statement_of_accounts");
            entity.HasKey(statement => statement.Id);
            entity.Property(statement => statement.UnitNumber).HasMaxLength(32).IsRequired();
            entity.Property(statement => statement.ResidentFullName).HasMaxLength(150);
            entity.Property(statement => statement.ResidentEmail).HasMaxLength(150);
            entity.Property(statement => statement.Status).HasMaxLength(32).IsRequired();
            entity.Property(statement => statement.PaymentReference).HasMaxLength(100);
            entity.Property(statement => statement.PaymentMethod).HasMaxLength(50);
            entity.Property(statement => statement.AliquotPercentage).HasPrecision(10, 4);
            entity.Property(statement => statement.IndividualWaterSoles).HasPrecision(18, 2);
            entity.Property(statement => statement.IndividualEnergySoles).HasPrecision(18, 2);
            entity.Property(statement => statement.IndividualOtherExpensesSoles).HasPrecision(18, 2);
            entity.Property(statement => statement.IndividualAmountSoles).HasPrecision(18, 2);
            entity.Property(statement => statement.PaidAmountSoles).HasPrecision(18, 2);
            entity.Property(statement => statement.BalanceSoles).HasPrecision(18, 2);

            entity.HasIndex(statement => new { statement.BillingPeriodId, statement.UnitId }).IsUnique();
            entity.HasIndex(statement => statement.ProjectId);
        });
    }
}
