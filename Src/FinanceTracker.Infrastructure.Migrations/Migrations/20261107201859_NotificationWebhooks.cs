using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class NotificationWebhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settings now keep the kinds turned off, so kinds added later reach everyone until turned off. The kinds
            // turned off emailing are worked out from those emailed (0 to 2, all there were) before that goes
            migrationBuilder.AddColumn<int[]>(
                name: "NotWebhooked",
                table: "NotificationSettings",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastWebhookAt",
                table: "NotificationSettings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastWebhookFailure",
                table: "NotificationSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int[]>(
                name: "NotEmailed",
                table: "NotificationSettings",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.Sql("""
                UPDATE "NotificationSettings"
                SET "NotEmailed" = ARRAY(SELECT kind FROM unnest(ARRAY[0, 1, 2]) AS kind
                                         WHERE NOT kind = ANY("Emailed") ORDER BY kind)
                """);

            migrationBuilder.DropColumn(
                name: "Emailed",
                table: "NotificationSettings");

            migrationBuilder.AddColumn<string>(
                name: "WebhookSecret",
                table: "NotificationSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookUrl",
                table: "NotificationSettings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastWebhookAt",
                table: "NotificationSettings");

            migrationBuilder.DropColumn(
                name: "LastWebhookFailure",
                table: "NotificationSettings");

            migrationBuilder.AddColumn<int[]>(
                name: "Emailed",
                table: "NotificationSettings",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.Sql("""
                UPDATE "NotificationSettings"
                SET "Emailed" = ARRAY(SELECT kind FROM unnest(ARRAY[0, 1, 2]) AS kind
                                      WHERE NOT kind = ANY("NotEmailed") ORDER BY kind)
                """);

            migrationBuilder.DropColumn(
                name: "NotEmailed",
                table: "NotificationSettings");

            migrationBuilder.DropColumn(
                name: "NotWebhooked",
                table: "NotificationSettings");

            migrationBuilder.DropColumn(
                name: "WebhookSecret",
                table: "NotificationSettings");

            migrationBuilder.DropColumn(
                name: "WebhookUrl",
                table: "NotificationSettings");
        }
    }
}
