using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class RenameBudgetCategoriesToSavingsGoals : Migration
    {
        /// <summary>
        /// Renamed in place so existing goals keep their data. Values are encrypted without the table or column name
        /// bound in, so they still decrypt under the new names.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetCategories_AspNetUsers_UserId",
                table: "BudgetCategories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BudgetCategories",
                table: "BudgetCategories");

            migrationBuilder.RenameTable(
                name: "BudgetCategories",
                newName: "SavingsGoals");

            migrationBuilder.RenameColumn(name: "AvailableFunds", table: "SavingsGoals", newName: "Saved");
            migrationBuilder.RenameColumn(name: "MonthlyStart", table: "SavingsGoals", newName: "MonthlyContribution");
            migrationBuilder.RenameColumn(name: "SavingsGoal", table: "SavingsGoals", newName: "Target");
            migrationBuilder.RenameColumn(name: "GoalCompletionDate", table: "SavingsGoals", newName: "TargetDate");

            migrationBuilder.RenameIndex(
                name: "IX_BudgetCategories_UserId",
                table: "SavingsGoals",
                newName: "IX_SavingsGoals_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SavingsGoals",
                table: "SavingsGoals",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SavingsGoals_AspNetUsers_UserId",
                table: "SavingsGoals",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SavingsGoals_AspNetUsers_UserId",
                table: "SavingsGoals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SavingsGoals",
                table: "SavingsGoals");

            migrationBuilder.RenameTable(
                name: "SavingsGoals",
                newName: "BudgetCategories");

            migrationBuilder.RenameColumn(name: "Saved", table: "BudgetCategories", newName: "AvailableFunds");
            migrationBuilder.RenameColumn(name: "MonthlyContribution", table: "BudgetCategories", newName: "MonthlyStart");
            migrationBuilder.RenameColumn(name: "Target", table: "BudgetCategories", newName: "SavingsGoal");
            migrationBuilder.RenameColumn(name: "TargetDate", table: "BudgetCategories", newName: "GoalCompletionDate");

            migrationBuilder.RenameIndex(
                name: "IX_SavingsGoals_UserId",
                table: "BudgetCategories",
                newName: "IX_BudgetCategories_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BudgetCategories",
                table: "BudgetCategories",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetCategories_AspNetUsers_UserId",
                table: "BudgetCategories",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
