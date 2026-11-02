using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CalendarEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<string>(type: "text", nullable: false),
                    Recurrence = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    SavingsTarget = table.Column<string>(type: "text", nullable: true),
                    SavingsGoalId = table.Column<Guid>(type: "uuid", nullable: true),
                    RollsGoalOn = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_SavingsGoals_SavingsGoalId",
                        column: x => x.SavingsGoalId,
                        principalTable: "SavingsGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_SavingsGoalId",
                table: "CalendarEvents",
                column: "SavingsGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_UserId",
                table: "CalendarEvents",
                column: "UserId");

            // Daily, after midnight, so a yearly event's goal moves on the day after it
            migrationBuilder.Sql("""INSERT INTO ticker."CronTickers"("Id", "Expression", "Request", "Retries", "RetryIntervals", "Function", "Description", "InitIdentifier", "CreatedAt", "UpdatedAt", "IsEnabled")VALUES (gen_random_uuid(), '0 0 2 * * *', NULL, 1, ARRAY [10,20,30], 'MoveEventSavingsGoalsOnAsync', 'Moves the savings goals of your yearly events on to their next date once each passes', NULL, timezone('utc', now()), timezone('utc', now()), true);""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM ticker."CronTickers" WHERE "Function" = 'MoveEventSavingsGoalsOnAsync';""");

            migrationBuilder.DropTable(
                name: "CalendarEvents");
        }
    }
}
