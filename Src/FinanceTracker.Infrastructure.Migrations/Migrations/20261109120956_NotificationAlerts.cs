using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class NotificationAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing settings start where new ones do, NotificationSettings.DefaultBillReminderDays and
            // DefaultBudgetWarningPercent, and every notification so far stays in the app
            migrationBuilder.AddColumn<int>(
                name: "BillReminderDays",
                table: "NotificationSettings",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "BudgetWarningPercent",
                table: "NotificationSettings",
                type: "integer",
                nullable: false,
                defaultValue: 80);

            migrationBuilder.AddColumn<string>(
                name: "LargeTransactionOver",
                table: "NotificationSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LowBalanceAlerts",
                table: "NotificationSettings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int[]>(
                name: "NotInApp",
                table: "NotificationSettings",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AddColumn<bool>(
                name: "InApp",
                table: "Notifications",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillReminderDays",
                table: "NotificationSettings");

            migrationBuilder.DropColumn(
                name: "BudgetWarningPercent",
                table: "NotificationSettings");

            migrationBuilder.DropColumn(
                name: "LargeTransactionOver",
                table: "NotificationSettings");

            migrationBuilder.DropColumn(
                name: "LowBalanceAlerts",
                table: "NotificationSettings");

            migrationBuilder.DropColumn(
                name: "NotInApp",
                table: "NotificationSettings");

            migrationBuilder.DropColumn(
                name: "InApp",
                table: "Notifications");
        }
    }
}
