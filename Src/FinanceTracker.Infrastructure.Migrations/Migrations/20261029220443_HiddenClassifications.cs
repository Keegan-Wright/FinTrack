using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class HiddenClassifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HiddenClassifications",
                table: "BankTransactions",
                type: "jsonb",
                nullable: true);

            // Transactions from before hiding existed start with nothing hidden rather than no list at all
            migrationBuilder.Sql("""UPDATE "BankTransactions" SET "HiddenClassifications" = '[]' WHERE "HiddenClassifications" IS NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HiddenClassifications",
                table: "BankTransactions");
        }
    }
}
