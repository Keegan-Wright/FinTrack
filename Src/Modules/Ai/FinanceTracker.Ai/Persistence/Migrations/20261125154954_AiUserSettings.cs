using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Ai.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261125154954_AiUserSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Value",
            schema: "ai",
            table: "Settings",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text");

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

        migrationBuilder.InsertData(
            schema: "ai",
            table: "Settings",
            columns: new[] { "Name", "IntegrityTag", "Value" },
            values: new object[] { "OllamaEndpoint", null, null });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "UserSettings",
            schema: "ai");

        migrationBuilder.DeleteData(
            schema: "ai",
            table: "Settings",
            keyColumn: "Name",
            keyValue: "OllamaEndpoint");

        migrationBuilder.AlterColumn<string>(
            name: "Value",
            schema: "ai",
            table: "Settings",
            type: "text",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);
    }
}
