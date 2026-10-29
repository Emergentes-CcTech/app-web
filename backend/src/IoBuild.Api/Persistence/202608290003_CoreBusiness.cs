using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoBuild.Api.Persistence;

[DbContext(typeof(IoBuildDbContext))]
[Migration("202608290003_CoreBusiness")]
public sealed class CoreBusiness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "projects", columns: table => new
        {
            Id = table.Column<int>(nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
            Name = table.Column<string>(maxLength: 200, nullable: false),
            Description = table.Column<string>(maxLength: 2000, nullable: false),
            Location = table.Column<string>(maxLength: 500, nullable: false),
            TotalUnits = table.Column<int>(nullable: false),
            BuilderId = table.Column<int>(nullable: false),
            ImageUrl = table.Column<string>(maxLength: 2000, nullable: true),
            StructureDefined = table.Column<bool>(nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK_projects", row => row.Id));
        migrationBuilder.CreateTable(name: "profiles", columns: table => new
        {
            Id = table.Column<int>(nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
            UserId = table.Column<int>(nullable: false),
            Name = table.Column<string>(maxLength: 200, nullable: false),
            Username = table.Column<string>(maxLength: 100, nullable: false),
            PhotoReference = table.Column<string>(maxLength: 128, nullable: true),
            CloudinaryReference = table.Column<string>(maxLength: 2000, nullable: true)
        }, constraints: table => table.PrimaryKey("PK_profiles", row => row.Id));
        migrationBuilder.CreateIndex("IX_profiles_UserId", "profiles", "UserId", unique: true);
        migrationBuilder.CreateTable(name: "subscriptions", columns: table => new
        {
            Id = table.Column<int>(nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
            BuilderId = table.Column<int>(nullable: false),
            PlanId = table.Column<int>(nullable: false),
            Status = table.Column<string>(maxLength: 40, nullable: false),
            StartDate = table.Column<DateTimeOffset>(nullable: false),
            EndDate = table.Column<DateTimeOffset>(nullable: true)
        }, constraints: table => table.PrimaryKey("PK_subscriptions", row => row.Id));
        migrationBuilder.CreateIndex("IX_subscriptions_BuilderId_PlanId_Status", "subscriptions", new[] { "BuilderId", "PlanId", "Status" });
        migrationBuilder.CreateTable(name: "subscription_webhooks", columns: table => new
        {
            EventId = table.Column<string>(maxLength: 255, nullable: false),
            EventType = table.Column<string>(maxLength: 120, nullable: false),
            ReceivedAt = table.Column<DateTimeOffset>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK_subscription_webhooks", row => row.EventId));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("subscription_webhooks");
        migrationBuilder.DropTable("subscriptions");
        migrationBuilder.DropTable("profiles");
        migrationBuilder.DropTable("projects");
    }
}
