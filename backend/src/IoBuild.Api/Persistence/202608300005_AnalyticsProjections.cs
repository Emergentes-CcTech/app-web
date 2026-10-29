using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace IoBuild.Api.Persistence;

[DbContext(typeof(IoBuildDbContext))]
[Migration("202608300005_AnalyticsProjections")]
public sealed class AnalyticsProjections : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "device_projection", columns: table => new { DeviceId = table.Column<int>(nullable: false), OwnerUserId = table.Column<int>(nullable: false), ProjectId = table.Column<int>(nullable: true), UnitId = table.Column<int>(nullable: true), DeviceType = table.Column<string>(maxLength: 64, nullable: false), Status = table.Column<string>(maxLength: 32, nullable: false), LastEventAt = table.Column<DateTime>(nullable: false), FloorNumber = table.Column<int>(nullable: true), DeviceName = table.Column<string>(nullable: true) }, constraints: table => table.PrimaryKey("PK_device_projection", row => row.DeviceId));
        migrationBuilder.CreateIndex("IX_device_projection_OwnerUserId", "device_projection", "OwnerUserId");
        migrationBuilder.CreateIndex("IX_device_projection_ProjectId", "device_projection", "ProjectId");
        migrationBuilder.CreateTable(name: "project_projection", columns: table => new { ProjectId = table.Column<int>(nullable: false), BuilderUserId = table.Column<int>(nullable: false), Name = table.Column<string>(maxLength: 160, nullable: false), Status = table.Column<string>(maxLength: 32, nullable: false), LastEventAt = table.Column<DateTime>(nullable: false) }, constraints: table => table.PrimaryKey("PK_project_projection", row => row.ProjectId));
        migrationBuilder.CreateIndex("IX_project_projection_BuilderUserId", "project_projection", "BuilderUserId");
        migrationBuilder.CreateTable(name: "unit_projection", columns: table => new { UnitId = table.Column<int>(nullable: false), ProjectId = table.Column<int>(nullable: false), BuilderUserId = table.Column<int>(nullable: false), OwnerUserId = table.Column<int>(nullable: true), Status = table.Column<string>(maxLength: 32, nullable: false), LastEventAt = table.Column<DateTime>(nullable: false), Floor = table.Column<int>(nullable: true), RoomNumber = table.Column<string>(nullable: true), OwnerEmail = table.Column<string>(nullable: true) }, constraints: table => table.PrimaryKey("PK_unit_projection", row => row.UnitId));
        migrationBuilder.CreateIndex("IX_unit_projection_BuilderUserId", "unit_projection", "BuilderUserId");
        migrationBuilder.CreateIndex("IX_unit_projection_OwnerUserId", "unit_projection", "OwnerUserId");
        migrationBuilder.CreateIndex("IX_unit_projection_ProjectId", "unit_projection", "ProjectId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("unit_projection");
        migrationBuilder.DropTable("project_projection");
        migrationBuilder.DropTable("device_projection");
    }
}
