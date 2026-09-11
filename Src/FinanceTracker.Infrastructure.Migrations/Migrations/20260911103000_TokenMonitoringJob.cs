using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class TokenMonitoringJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""INSERT INTO ticker."CronTickers"("Id", "Expression", "Request", "Retries", "RetryIntervals", "Function", "Description", "InitIdentifier", "CreatedAt", "UpdatedAt", "IsEnabled")VALUES (gen_random_uuid(), '0 0 */12 * * *', NULL, 1, ARRAY [10,20,30], 'MonitorOpenBankingTokenExpirationAsync', 'Performs background monitoring of your provider access tokens for expiration', NULL, timezone('utc', now()), timezone('utc', now()), true);""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
