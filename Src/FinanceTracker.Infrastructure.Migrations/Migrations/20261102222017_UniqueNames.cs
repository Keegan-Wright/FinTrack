using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <summary>
    /// Keeps a user's tags, savings goals, debts and household members apart by name in any case, through a unique
    /// index on each table's NameKeyBlindIndex.
    /// </summary>
    /// <remarks>
    /// The app fills NameKeyBlindIndex as it saves, so existing rows start empty. A user with two goals, debts or
    /// people already would fail the new unique indexes, and existing tags keep their old exact-case hash until saved
    /// again. Applied to an empty database.
    /// </remarks>
    public partial class UniqueNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SavingsGoals_UserId",
                table: "SavingsGoals");

            migrationBuilder.DropIndex(
                name: "IX_HouseholdMembers_UserId",
                table: "HouseholdMembers");

            migrationBuilder.DropIndex(
                name: "IX_Debts_UserId",
                table: "Debts");

            migrationBuilder.RenameColumn(
                name: "TagBlindIndex",
                table: "CustomClassifications",
                newName: "NameKeyBlindIndex");

            migrationBuilder.RenameIndex(
                name: "IX_CustomClassifications_UserId_TagBlindIndex",
                table: "CustomClassifications",
                newName: "IX_CustomClassifications_UserId_NameKeyBlindIndex");

            migrationBuilder.AddColumn<string>(
                name: "NameKeyBlindIndex",
                table: "SavingsGoals",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameKeyBlindIndex",
                table: "HouseholdMembers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameKeyBlindIndex",
                table: "Debts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsGoals_UserId_NameKeyBlindIndex",
                table: "SavingsGoals",
                columns: new[] { "UserId", "NameKeyBlindIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMembers_UserId_NameKeyBlindIndex",
                table: "HouseholdMembers",
                columns: new[] { "UserId", "NameKeyBlindIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Debts_UserId_NameKeyBlindIndex",
                table: "Debts",
                columns: new[] { "UserId", "NameKeyBlindIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SavingsGoals_UserId_NameKeyBlindIndex",
                table: "SavingsGoals");

            migrationBuilder.DropIndex(
                name: "IX_HouseholdMembers_UserId_NameKeyBlindIndex",
                table: "HouseholdMembers");

            migrationBuilder.DropIndex(
                name: "IX_Debts_UserId_NameKeyBlindIndex",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "NameKeyBlindIndex",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "NameKeyBlindIndex",
                table: "HouseholdMembers");

            migrationBuilder.DropColumn(
                name: "NameKeyBlindIndex",
                table: "Debts");

            migrationBuilder.RenameColumn(
                name: "NameKeyBlindIndex",
                table: "CustomClassifications",
                newName: "TagBlindIndex");

            migrationBuilder.RenameIndex(
                name: "IX_CustomClassifications_UserId_NameKeyBlindIndex",
                table: "CustomClassifications",
                newName: "IX_CustomClassifications_UserId_TagBlindIndex");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsGoals_UserId",
                table: "SavingsGoals",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMembers_UserId",
                table: "HouseholdMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Debts_UserId",
                table: "Debts",
                column: "UserId");
        }
    }
}
