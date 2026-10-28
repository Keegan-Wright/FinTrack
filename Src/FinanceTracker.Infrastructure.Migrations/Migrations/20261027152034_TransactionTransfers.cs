using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class TransactionTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LabelledAsTransfer",
                table: "BankTransactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "Transfer_DecidedAt",
                table: "BankTransactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Transfer_Decision",
                table: "BankTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Transfer_PartnerAccountId",
                table: "BankTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Transfer_PartnerTransactionId",
                table: "BankTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Transfer_Reason",
                table: "BankTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Transfer_Source",
                table: "BankTransactions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LabelledAsTransfer",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "Transfer_DecidedAt",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "Transfer_Decision",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "Transfer_PartnerAccountId",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "Transfer_PartnerTransactionId",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "Transfer_Reason",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "Transfer_Source",
                table: "BankTransactions");
        }
    }
}
