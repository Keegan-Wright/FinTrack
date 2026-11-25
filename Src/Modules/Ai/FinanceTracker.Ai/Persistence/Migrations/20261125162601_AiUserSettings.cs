using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Ai.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261125162601_AiUserSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Settings",
            schema: "ai");

        migrationBuilder.CreateTable(
            name: "UserSettings",
            schema: "ai",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                OptedIn = table.Column<bool>(type: "boolean", nullable: false),
                IntegrityTag = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserSettings", x => x.UserId);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "UserSettings",
            schema: "ai");

        migrationBuilder.CreateTable(
            name: "Settings",
            schema: "ai",
            columns: table => new
            {
                Name = table.Column<string>(type: "text", nullable: false),
                IntegrityTag = table.Column<string>(type: "text", nullable: true),
                Value = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Settings", x => x.Name);
            });
    }
}
