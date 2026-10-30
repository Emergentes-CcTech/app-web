using System.Data;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Persistence;

public interface IMigrationRunner
{
    Task ApplyAsync(CancellationToken cancellationToken);
}

public sealed class EfMigrationRunner(IoBuildDbContext dbContext) : IMigrationRunner
{
    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        if (dbContext.Database.IsRelational())
        {
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var connection = dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose) await connection.OpenAsync(cancellationToken);

            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'profiles';";
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    existingColumns.Add(reader.GetString(0));
                }
            }
            finally
            {
                if (shouldClose) await connection.CloseAsync();
            }

            var needsBuilderYearsBackfill = !existingColumns.Contains("YearsInBusiness");
            var columnsToAdd = new (string Name, string Type)[]
            {
                ("PhoneNumber", "VARCHAR(50) NULL"),
                ("Address", "VARCHAR(255) NULL"),
                ("SecondEmail", "VARCHAR(150) NULL"),
                ("PhotoUrl", "LONGTEXT NULL"),
                ("YearsInBusiness", "INT NULL"),
                ("LegalRepresentative", "VARCHAR(150) NULL")
            };

            foreach (var (colName, colType) in columnsToAdd)
            {
                if (!existingColumns.Contains(colName))
                {
                    await dbContext.Database.ExecuteSqlRawAsync($"ALTER TABLE profiles ADD COLUMN {colName} {colType};", cancellationToken);
                }
            }

            if (needsBuilderYearsBackfill)
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    "UPDATE profiles p INNER JOIN iam_users u ON u.Id = p.UserId SET p.YearsInBusiness = p.Age WHERE LOWER(u.`Role`) = 'builder' AND p.YearsInBusiness IS NULL AND p.Age BETWEEN 0 AND 120;",
                    cancellationToken);
            }

            if (existingColumns.Contains("PhotoUrl"))
            {
                await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE profiles MODIFY COLUMN PhotoUrl LONGTEXT NULL;", cancellationToken);
            }
            if (existingColumns.Contains("CloudinaryReference"))
            {
                await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE profiles MODIFY COLUMN CloudinaryReference LONGTEXT NULL;", cancellationToken);
            }

            var existingClientColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (shouldClose) await connection.OpenAsync(cancellationToken);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'clients';";
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    existingClientColumns.Add(reader.GetString(0));
                }
            }
            finally
            {
                if (shouldClose) await connection.CloseAsync();
            }

            var clientColumnsToAdd = new (string Name, string Type)[]
            {
                ("Email", "VARCHAR(150) NULL"),
                ("PhoneNumber", "VARCHAR(50) NULL"),
                ("Address", "VARCHAR(255) NULL"),
                ("UnitId", "INT NULL"),
                ("UnitNumber", "VARCHAR(50) NULL")
            };

            foreach (var (colName, colType) in clientColumnsToAdd)
            {
                if (!existingClientColumns.Contains(colName))
                {
                    await dbContext.Database.ExecuteSqlRawAsync($"ALTER TABLE clients ADD COLUMN {colName} {colType};", cancellationToken);
                }
            }

            // Datos de catastro del condominio (dirección, distrito, suministros de recibos matriz, estado):
            // antes solo vivían en memoria y el alta de condominio los perdía.
            var existingProjectColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (shouldClose) await connection.OpenAsync(cancellationToken);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'projects';";
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    existingProjectColumns.Add(reader.GetString(0));
                }
            }
            finally
            {
                if (shouldClose) await connection.CloseAsync();
            }

            var projectColumnsToAdd = new (string Name, string Type)[]
            {
                ("Address", "VARCHAR(255) NOT NULL DEFAULT ''"),
                ("District", "VARCHAR(100) NOT NULL DEFAULT ''"),
                ("TotalFloors", "INT NOT NULL DEFAULT 1"),
                ("SedapalSupplyCode", "VARCHAR(32) NOT NULL DEFAULT ''"),
                ("LuzDelSurSupplyCode", "VARCHAR(32) NOT NULL DEFAULT ''"),
                ("Status", "VARCHAR(32) NOT NULL DEFAULT 'Activo'")
            };

            foreach (var (colName, colType) in projectColumnsToAdd)
            {
                if (!existingProjectColumns.Contains(colName))
                {
                    await dbContext.Database.ExecuteSqlRawAsync($"ALTER TABLE projects ADD COLUMN {colName} {colType};", cancellationToken);
                    // Los condominios existentes conservan el distrito que la API ya mostraba por defecto
                    if (colName == "District")
                    {
                        await dbContext.Database.ExecuteSqlRawAsync("UPDATE projects SET District = 'Jesús María' WHERE District = '';", cancellationToken);
                    }
                }
            }

            // Single-active-subscription arbiter (migration 202609170006)
            if (shouldClose) await connection.OpenAsync(cancellationToken);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'subscriptions' AND COLUMN_NAME = 'ActiveBuilderId';";
                var hasArbiter = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
                if (!hasArbiter)
                {
                    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE subscriptions ADD COLUMN ActiveBuilderId INT GENERATED ALWAYS AS (CASE WHEN Status = 'active' THEN BuilderId ELSE NULL END) STORED;", cancellationToken);
                    await dbContext.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IX_subscriptions_ActiveBuilderId ON subscriptions (ActiveBuilderId);", cancellationToken);
                }
            }
            finally
            {
                if (shouldClose) await connection.CloseAsync();
            }

            // Asegura la existencia de la tabla de áreas comunes para el contexto Cadastre
            if (shouldClose) await connection.OpenAsync(cancellationToken);
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    "CREATE TABLE IF NOT EXISTS common_areas (Id INT AUTO_INCREMENT PRIMARY KEY, ProjectId INT NOT NULL, Name VARCHAR(150) NOT NULL, Subsystem VARCHAR(50) NOT NULL, Location VARCHAR(255) NULL, Description VARCHAR(500) NULL);", cancellationToken);

                // Asegura la existencia de las tablas de series temporales y alertas para Analytics
                await dbContext.Database.ExecuteSqlRawAsync(
                    "CREATE TABLE IF NOT EXISTS consumption_records (Id BIGINT AUTO_INCREMENT PRIMARY KEY, ProjectId INT NOT NULL, DeviceId INT NOT NULL, Subsystem VARCHAR(64) NOT NULL, MetricType VARCHAR(64) NOT NULL, Value DOUBLE NOT NULL, TimestampUtc DATETIME(6) NOT NULL, INDEX idx_cr_proj_time (ProjectId, TimestampUtc), INDEX idx_cr_dev_time (DeviceId, TimestampUtc));", cancellationToken);

                await dbContext.Database.ExecuteSqlRawAsync(
                    "CREATE TABLE IF NOT EXISTS anomaly_alerts (Id INT AUTO_INCREMENT PRIMARY KEY, ProjectId INT NOT NULL, DeviceId INT NOT NULL, Subsystem VARCHAR(64) NOT NULL, AlertType VARCHAR(64) NOT NULL, Severity VARCHAR(32) NOT NULL, Title VARCHAR(200) NOT NULL, Description VARCHAR(1000) NOT NULL, EstimatedOvercostSoles DECIMAL(10,2) NOT NULL DEFAULT 0.00, DetectedAtUtc DATETIME(6) NOT NULL, ResolvedAtUtc DATETIME(6) NULL, Status VARCHAR(32) NOT NULL, INDEX idx_aa_proj_status (ProjectId, Status), INDEX idx_aa_dev (DeviceId));",
                    cancellationToken);

                // Asegura la existencia de las tablas del contexto de Prorrateo y Liquidación (Proration)
                await dbContext.Database.ExecuteSqlRawAsync(
                    "CREATE TABLE IF NOT EXISTS billing_periods (Id INT AUTO_INCREMENT PRIMARY KEY, ProjectId INT NOT NULL, PeriodCode VARCHAR(32) NOT NULL, StartDate DATETIME(6) NOT NULL, EndDate DATETIME(6) NOT NULL, DueDate DATETIME(6) NOT NULL, MacroWaterM3 DECIMAL(18,4) NOT NULL, MacroWaterAmountSoles DECIMAL(18,2) NOT NULL, WaterReceiptImageUrl VARCHAR(500) NULL, MacroEnergyKwh DECIMAL(18,4) NOT NULL, MacroEnergyAmountSoles DECIMAL(18,2) NOT NULL, EnergyReceiptImageUrl VARCHAR(500) NULL, MeasuredCommonWaterM3 DECIMAL(18,4) NOT NULL, MeasuredCommonEnergyKwh DECIMAL(18,4) NOT NULL, WaterLossM3 DECIMAL(18,4) NOT NULL, EnergyLossKwh DECIMAL(18,4) NOT NULL, OtherCommonExpensesSoles DECIMAL(18,2) NOT NULL, TotalCommonExpenseSoles DECIMAL(18,2) NOT NULL, Status VARCHAR(32) NOT NULL, ReconciledAtUtc DATETIME(6) NULL, SettledAtUtc DATETIME(6) NULL, TotalBilledSoles DECIMAL(18,2) NOT NULL, TotalCollectedSoles DECIMAL(18,2) NOT NULL, DelinquencyRate DECIMAL(5,2) NOT NULL, UNIQUE KEY uq_bp_proj_period (ProjectId, PeriodCode));",
                    cancellationToken);

                await dbContext.Database.ExecuteSqlRawAsync(
                    "CREATE TABLE IF NOT EXISTS statement_of_accounts (Id INT AUTO_INCREMENT PRIMARY KEY, BillingPeriodId INT NOT NULL, ProjectId INT NOT NULL, UnitId INT NOT NULL, UnitNumber VARCHAR(32) NOT NULL, ResidentFullName VARCHAR(150) NULL, ResidentEmail VARCHAR(150) NULL, AliquotPercentage DECIMAL(10,4) NOT NULL, IndividualWaterSoles DECIMAL(18,2) NOT NULL, IndividualEnergySoles DECIMAL(18,2) NOT NULL, IndividualOtherExpensesSoles DECIMAL(18,2) NOT NULL, IndividualAmountSoles DECIMAL(18,2) NOT NULL, PaidAmountSoles DECIMAL(18,2) NOT NULL DEFAULT 0.00, BalanceSoles DECIMAL(18,2) NOT NULL DEFAULT 0.00, Status VARCHAR(32) NOT NULL, PaidAtUtc DATETIME(6) NULL, PaymentReference VARCHAR(100) NULL, PaymentMethod VARCHAR(50) NULL, UNIQUE KEY uq_soa_period_unit (BillingPeriodId, UnitId), INDEX idx_soa_proj (ProjectId));",
                    cancellationToken);

                // Asegura las columnas para alícuotas y catastro en la tabla units
                var existingUnitCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using var unitCmd = connection.CreateCommand();
                unitCmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'units';";
                using var unitReader = await unitCmd.ExecuteReaderAsync(cancellationToken);
                while (await unitReader.ReadAsync(cancellationToken))
                {
                    existingUnitCols.Add(unitReader.GetString(0));
                }

                if (!existingUnitCols.Contains("AliquotPercentage"))
                {
                    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE units ADD COLUMN AliquotPercentage DECIMAL(7,4) NOT NULL DEFAULT 0.0000;", cancellationToken);
                }
                if (!existingUnitCols.Contains("AreaM2"))
                {
                    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE units ADD COLUMN AreaM2 DECIMAL(10,2) NOT NULL DEFAULT 75.00;", cancellationToken);
                }
                if (!existingUnitCols.Contains("ResidentFullName"))
                {
                    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE units ADD COLUMN ResidentFullName VARCHAR(255) NULL;", cancellationToken);
                }
                if (!existingUnitCols.Contains("AccountStatus"))
                {
                    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE units ADD COLUMN AccountStatus VARCHAR(50) NOT NULL DEFAULT 'Al día';", cancellationToken);
                }
            }
            finally
            {
                if (shouldClose) await connection.CloseAsync();
            }
        }
    }
}
