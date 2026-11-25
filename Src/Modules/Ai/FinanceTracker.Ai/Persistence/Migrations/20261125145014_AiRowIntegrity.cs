using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Ai.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261125145014_AiRowIntegrity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "IntegrityTag",
            schema: "ai",
            table: "Settings",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IntegrityTag",
            schema: "ai",
            table: "Settings");
    }
}
