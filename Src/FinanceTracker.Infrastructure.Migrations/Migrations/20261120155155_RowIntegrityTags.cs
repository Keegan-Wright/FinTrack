using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class RowIntegrityTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "UserLinks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "TransferMatchCheckpoints",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "SyncLog",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "StandingOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "SharingSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "Shares",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "SavingsGoals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "Rules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "RuleCheckpoints",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "PushDevices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "PersonalAccessTokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "PaySchedules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "NotificationSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "Notifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "NetWorthSnapshots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "JointAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "HouseholdMembers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "DirectDebits",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "Debts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "CustomClassifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "CalendarEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "Budgets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "BillSuggestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "Bills",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "BankTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "BankConnections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "BankAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "BalanceSnapshots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityTag",
                table: "AccessTokens",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "UserLinks");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "TransferMatchCheckpoints");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "SyncLog");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "StandingOrders");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "SharingSettings");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "Shares");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "Rules");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "RuleCheckpoints");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "PushDevices");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "PersonalAccessTokens");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "PaySchedules");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "NotificationSettings");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "NetWorthSnapshots");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "JointAccounts");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "HouseholdMembers");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "DirectDebits");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "CustomClassifications");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "BillSuggestions");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "BankConnections");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "BalanceSnapshots");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "AccessTokens");
        }
    }
}
