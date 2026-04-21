using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Data.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class SeedsBackgroundSyncJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""INSERT INTO ticker."CronTickers"("Id", "Expression", "Request", "Retries", "RetryIntervals", "Function", "Description", "InitIdentifier", "CreatedAt", "UpdatedAt", "IsEnabled")VALUES (gen_random_uuid(), '0 0 */4 * * *', NULL, 1, ARRAY [10,20,30], 'SyncAllOpenBankingDetailsAsync', 'Performs background syncing of your banking data', NULL, timezone('utc', now()), timezone('utc', now()), true);""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
