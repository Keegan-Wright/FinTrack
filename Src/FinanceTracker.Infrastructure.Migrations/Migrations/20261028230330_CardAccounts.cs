using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CardAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Balance_CreditLimit",
                table: "BankAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Card_CardType",
                table: "BankAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Card_NameOnCard",
                table: "BankAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Card_Network",
                table: "BankAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Card_PartialNumber",
                table: "BankAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Card_ValidTo",
                table: "BankAccounts",
                type: "text",
                nullable: true);

            // Everything synced so far came from the accounts endpoint
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "BankAccounts",
                type: "text",
                nullable: false,
                defaultValue: "Account");

            migrationBuilder.AddColumn<string>(
                name: "Balance_CreditLimit",
                table: "BalanceSnapshots",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Balance_CreditLimit",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "Card_CardType",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "Card_NameOnCard",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "Card_Network",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "Card_PartialNumber",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "Card_ValidTo",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "Balance_CreditLimit",
                table: "BalanceSnapshots");
        }
    }
}
