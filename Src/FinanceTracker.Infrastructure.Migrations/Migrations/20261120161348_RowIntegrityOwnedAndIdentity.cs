using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class RowIntegrityOwnedAndIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "HouseholdMemberLinkedAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metadata_IntegrityTag",
                table: "BankTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Balance_IntegrityTag",
                table: "BankAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Card_IntegrityTag",
                table: "BankAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identifier_IntegrityTag",
                table: "BankAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Balance_IntegrityTag",
                table: "BalanceSnapshots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "AspNetUserTokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "AspNetUserPasskeys",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "HouseholdMemberLinkedAccounts");

            migrationBuilder.DropColumn(
                name: "Metadata_IntegrityTag",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "Balance_IntegrityTag",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "Card_IntegrityTag",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "Identifier_IntegrityTag",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "Balance_IntegrityTag",
                table: "BalanceSnapshots");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "AspNetUserTokens");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "AspNetUserPasskeys");
        }
    }
}
