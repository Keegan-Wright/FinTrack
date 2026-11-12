using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class ChosenShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shares_LinkId_ItemType_ItemId",
                table: "Shares");

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "Shares",
                type: "text",
                nullable: false,
                defaultValue: "Chosen");

            // Tags, bills and debts were only ever shared for being on the other person's transaction until now
            migrationBuilder.Sql(
                """UPDATE "Shares" SET "Reason" = 'OnTransaction' WHERE "ItemType" IN ('Classification', 'Bill', 'Debt');""");

            migrationBuilder.CreateIndex(
                name: "IX_Shares_LinkId_ItemType_ItemId_Reason",
                table: "Shares",
                columns: new[] { "LinkId", "ItemType", "ItemId", "Reason" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Chosen shares of anything but accounts didn't exist before
            migrationBuilder.Sql(
                """DELETE FROM "Shares" WHERE "Reason" = 'Chosen' AND "ItemType" <> 'BankAccount';""");

            migrationBuilder.DropIndex(
                name: "IX_Shares_LinkId_ItemType_ItemId_Reason",
                table: "Shares");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "Shares");

            migrationBuilder.CreateIndex(
                name: "IX_Shares_LinkId_ItemType_ItemId",
                table: "Shares",
                columns: new[] { "LinkId", "ItemType", "ItemId" },
                unique: true);
        }
    }
}
