using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class JointAccountGaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Gaps",
                table: "JointAccounts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FillsJointGap",
                table: "BankTransactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gaps",
                table: "JointAccounts");

            migrationBuilder.DropColumn(
                name: "FillsJointGap",
                table: "BankTransactions");
        }
    }
}
