using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class GcmEncryptionWithBlindIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Values encrypted under the old scheme (AES-CBC with a fixed IV) can't be read by the new one, so financial
            // data is cleared and synced again from the banks. User accounts are kept, children before parents
            foreach (string table in new[]
                     {
                         "SyncLog", "BankTransactions", "StandingOrders", "DirectDebits", "BankAccounts", "AccessTokens",
                         "BankConnections", "CustomClassifications", "BudgetCategories", "Debts", "HouseholdMembers"
                     })
            {
                migrationBuilder.Sql($"DELETE FROM \"{table}\";");
            }

            migrationBuilder.DropIndex(
                name: "IX_DirectDebits_AccountId_ExternalId",
                table: "DirectDebits");

            migrationBuilder.DropIndex(
                name: "IX_CustomClassifications_UserId_Tag",
                table: "CustomClassifications");

            migrationBuilder.DropIndex(
                name: "IX_BankTransactions_AccountId_ExternalId",
                table: "BankTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BankConnections_UserId_ExternalProviderId",
                table: "BankConnections");

            migrationBuilder.AddColumn<string>(
                name: "ExternalIdBlindIndex",
                table: "DirectDebits",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TagBlindIndex",
                table: "CustomClassifications",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalIdBlindIndex",
                table: "BankTransactions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalProviderIdBlindIndex",
                table: "BankConnections",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_DirectDebits_AccountId_ExternalIdBlindIndex",
                table: "DirectDebits",
                columns: new[] { "AccountId", "ExternalIdBlindIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomClassifications_UserId_TagBlindIndex",
                table: "CustomClassifications",
                columns: new[] { "UserId", "TagBlindIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_AccountId_ExternalIdBlindIndex",
                table: "BankTransactions",
                columns: new[] { "AccountId", "ExternalIdBlindIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_UserId_ExternalProviderIdBlindIndex",
                table: "BankConnections",
                columns: new[] { "UserId", "ExternalProviderIdBlindIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the schema is restored, the data cleared in Up is gone
            migrationBuilder.DropIndex(
                name: "IX_DirectDebits_AccountId_ExternalIdBlindIndex",
                table: "DirectDebits");

            migrationBuilder.DropIndex(
                name: "IX_CustomClassifications_UserId_TagBlindIndex",
                table: "CustomClassifications");

            migrationBuilder.DropIndex(
                name: "IX_BankTransactions_AccountId_ExternalIdBlindIndex",
                table: "BankTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BankConnections_UserId_ExternalProviderIdBlindIndex",
                table: "BankConnections");

            migrationBuilder.DropColumn(
                name: "ExternalIdBlindIndex",
                table: "DirectDebits");

            migrationBuilder.DropColumn(
                name: "TagBlindIndex",
                table: "CustomClassifications");

            migrationBuilder.DropColumn(
                name: "ExternalIdBlindIndex",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "ExternalProviderIdBlindIndex",
                table: "BankConnections");

            migrationBuilder.CreateIndex(
                name: "IX_DirectDebits_AccountId_ExternalId",
                table: "DirectDebits",
                columns: new[] { "AccountId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomClassifications_UserId_Tag",
                table: "CustomClassifications",
                columns: new[] { "UserId", "Tag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_AccountId_ExternalId",
                table: "BankTransactions",
                columns: new[] { "AccountId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_UserId_ExternalProviderId",
                table: "BankConnections",
                columns: new[] { "UserId", "ExternalProviderId" },
                unique: true);
        }
    }
}
