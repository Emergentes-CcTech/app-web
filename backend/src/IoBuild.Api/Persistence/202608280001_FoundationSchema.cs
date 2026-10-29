using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace IoBuild.Api.Persistence;

[DbContext(typeof(IoBuildDbContext))]
[Migration("202608280001_FoundationSchema")]
public sealed class FoundationSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "foundation_records",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                Name = table.Column<string>(maxLength: 200, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_foundation_records", row => row.Id));
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "foundation_records");
}
