using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace IoBuild.Api.Persistence;

[DbContext(typeof(IoBuildDbContext))]
[Migration("202608290002_IamAndDispatch")]
public sealed class IamAndDispatch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "iam_users", columns: table => new
        {
            Id = table.Column<int>(nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
            Email = table.Column<string>(maxLength: 320, nullable: false),
            PasswordHash = table.Column<string>(maxLength: 255, nullable: false),
            Role = table.Column<string>(maxLength: 80, nullable: false)
        }, constraints: table => table.PrimaryKey("PK_iam_users", row => row.Id));
        migrationBuilder.CreateIndex(name: "IX_iam_users_Email", table: "iam_users", column: "Email", unique: true);
        migrationBuilder.CreateTable(name: "iam_revoked_tokens", columns: table => new
        {
            TokenHash = table.Column<string>(maxLength: 64, nullable: false),
            ExpiresAt = table.Column<DateTimeOffset>(nullable: false),
            RevokedAt = table.Column<DateTimeOffset>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK_iam_revoked_tokens", row => row.TokenHash));
        migrationBuilder.CreateTable(name: "integration_dispatch", columns: table => new
        {
            Id = table.Column<long>(nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
            OwnerModule = table.Column<string>(maxLength: 80, nullable: false),
            Channel = table.Column<string>(maxLength: 80, nullable: false),
            OrderingKey = table.Column<string>(maxLength: 200, nullable: false),
            Sequence = table.Column<long>(nullable: false),
            Payload = table.Column<string>(nullable: false),
            IdempotencyKey = table.Column<string>(maxLength: 200, nullable: false),
            Status = table.Column<string>(maxLength: 20, nullable: false),
            Attempts = table.Column<int>(nullable: false),
            NextAttemptAt = table.Column<DateTimeOffset>(nullable: false),
            LeaseOwner = table.Column<string>(nullable: true),
            LeaseExpiresAt = table.Column<DateTimeOffset>(nullable: true),
            LastError = table.Column<string>(nullable: true),
            CreatedAt = table.Column<DateTimeOffset>(nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK_integration_dispatch", row => row.Id));
        migrationBuilder.CreateIndex(name: "IX_integration_dispatch_IdempotencyKey", table: "integration_dispatch", column: "IdempotencyKey", unique: true);
        migrationBuilder.CreateIndex(name: "IX_integration_dispatch_OrderingKey_Sequence", table: "integration_dispatch", columns: new[] { "OrderingKey", "Sequence" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_integration_dispatch_Status_NextAttemptAt", table: "integration_dispatch", columns: new[] { "Status", "NextAttemptAt" });
    }
    protected override void Down(MigrationBuilder migrationBuilder) { migrationBuilder.DropTable("integration_dispatch"); migrationBuilder.DropTable("iam_revoked_tokens"); migrationBuilder.DropTable("iam_users"); }
}
