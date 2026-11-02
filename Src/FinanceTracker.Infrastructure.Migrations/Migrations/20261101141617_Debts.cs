using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class Debts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dropped rather than renamed, the forecast takes its place and Lender is a new column
            migrationBuilder.DropColumn(
                name: "FinalPaymentDate",
                table: "Debts");

            migrationBuilder.AddColumn<string>(
                name: "Lender",
                table: "Debts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "Debts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterestRate",
                table: "Debts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StartedOn",
                table: "Debts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StartingBalance",
                table: "Debts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "DebtPayment_DebtId",
                table: "BankTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DebtPayment_DecidedAt",
                table: "BankTransactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DebtPayment_RuleId",
                table: "BankTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DebtPayment_Source",
                table: "BankTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Debts_AccountId",
                table: "Debts",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_DebtPayment_DebtId",
                table: "BankTransactions",
                column: "DebtPayment_DebtId");

            migrationBuilder.AddForeignKey(
                name: "FK_Debts_BankAccounts_AccountId",
                table: "Debts",
                column: "AccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Debts_BankAccounts_AccountId",
                table: "Debts");

            migrationBuilder.DropIndex(
                name: "IX_Debts_AccountId",
                table: "Debts");

            migrationBuilder.DropIndex(
                name: "IX_BankTransactions_DebtPayment_DebtId",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "InterestRate",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "StartedOn",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "StartingBalance",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "DebtPayment_DebtId",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "DebtPayment_DecidedAt",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "DebtPayment_RuleId",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "DebtPayment_Source",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "Lender",
                table: "Debts");

            migrationBuilder.AddColumn<string>(
                name: "FinalPaymentDate",
                table: "Debts",
                type: "text",
                nullable: true);
        }
    }
}
