using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class RuleDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RuleTags",
                table: "BankTransactions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TagsSetByUser",
                table: "BankTransactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "Transfer_RuleId",
                table: "BankTransactions",
                type: "uuid",
                nullable: true);

            // Every tag so far was added by hand, so rules mustn't change those transactions' tags
            migrationBuilder.Sql("""UPDATE "BankTransactions" SET "TagsSetByUser" = true WHERE cardinality("CustomClassificationIds") > 0;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RuleTags",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "TagsSetByUser",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "Transfer_RuleId",
                table: "BankTransactions");
        }
    }
}
