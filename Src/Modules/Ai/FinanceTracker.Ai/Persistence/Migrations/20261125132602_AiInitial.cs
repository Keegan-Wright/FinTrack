using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Ai.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261125132602_AiInitial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "ai");

        migrationBuilder.CreateTable(
            name: "Settings",
            schema: "ai",
            columns: table => new
            {
                Name = table.Column<string>(type: "text", nullable: false),
                Value = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Settings", x => x.Name);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Settings",
            schema: "ai");
    }
}
