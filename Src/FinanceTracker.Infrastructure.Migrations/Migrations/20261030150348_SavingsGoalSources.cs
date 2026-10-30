using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class SavingsGoalSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "SavingsGoals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "SavingsGoals",
                type: "text",
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<Guid[]>(
                name: "TagIds",
                table: "SavingsGoals",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<string>(
                name: "TrackedFrom",
                table: "SavingsGoals",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavingsGoals_AccountId",
                table: "SavingsGoals",
                column: "AccountId",
                unique: true,
                filter: "\"AccountId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SavingsGoals_AccountId",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "TagIds",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "TrackedFrom",
                table: "SavingsGoals");
        }
    }
}
