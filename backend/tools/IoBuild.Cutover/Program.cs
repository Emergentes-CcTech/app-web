using IoBuild.Api.Shared.Application.Cutover;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();
var connectionString = configuration.GetConnectionString("IoBuild") ?? "Server=localhost;Port=3306;Database=iobuild;User=root;Password=iobuild";
var services = new ServiceCollection();
services.AddDbContext<IoBuildDbContext>(options => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
services.AddSingleton<CutoverReadiness>();
services.AddScoped<ICutoverHarness, CutoverHarness>();
var provider = services.BuildServiceProvider();

var checkpointPath = args.Length > 0 ? args[0] : "cutover-checkpoint.json";
var nginxPath = args.Length > 1 ? args[1] : "../../nginx/nginx.conf";

Console.WriteLine($"IoBuild Cutover — checkpoint: {checkpointPath}, nginx: {nginxPath}");

using var scope = provider.CreateScope();
var harness = scope.ServiceProvider.GetRequiredService<ICutoverHarness>();
var readiness = scope.ServiceProvider.GetRequiredService<CutoverReadiness>();

// Freeze phase: block writes via readiness gate returning 503
await harness.FreezeAsync();
Console.WriteLine($"Freeze: ShouldBlockWrites={readiness.ShouldBlockWrites}, FailureReason={readiness.FailureReason}");

// Backup phase: mysqldump concept + checkpoint
var checkpoint = await harness.BackupAsync(checkpointPath);
Console.WriteLine($"Backup: counts iam={checkpoint.IamCount} projects={checkpoint.ProjectCount} profiles={checkpoint.ProfileCount} subscriptions={checkpoint.SubscriptionCount} devices={checkpoint.DeviceCount} hash={checkpoint.Hash}");

// Import phase would be driven by harness.ImportAsync with ordered dump (IAM→Projects/Profiles→Subscriptions→Devices)
// Parity gates are inside harness.VerifyParityAsync

// Nginx switch phase: proxy to monolith instead of gateway:8080
await harness.SwitchAsync(nginxPath);
Console.WriteLine($"Switch: nginx config written to {nginxPath}");

// Restore rollback concept available via harness.RestoreAsync on failure
Console.WriteLine("Cutover harness ready. Use StabilizeAsync with admin role to unfreeze.");
