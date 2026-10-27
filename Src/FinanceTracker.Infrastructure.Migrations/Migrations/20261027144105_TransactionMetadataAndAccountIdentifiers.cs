using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class TransactionMetadataAndAccountIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Meta_CounterPartyPreferredName",
                table: "BankTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Meta_DebtorAccountName",
                table: "BankTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Meta_MerchantName",
                table: "BankTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Meta_ProviderCategory",
                table: "BankTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Meta_ProviderReference",
                table: "BankTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identifier_Iban",
                table: "BankAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identifier_Number",
                table: "BankAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identifier_SortCode",
                table: "BankAccounts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Meta_CounterPartyPreferredName",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "Meta_DebtorAccountName",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "Meta_MerchantName",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "Meta_ProviderCategory",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "Meta_ProviderReference",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "Identifier_Iban",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "Identifier_Number",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "Identifier_SortCode",
                table: "BankAccounts");
        }
    }
}
