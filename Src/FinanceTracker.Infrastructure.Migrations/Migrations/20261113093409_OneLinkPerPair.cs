using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class OneLinkPerPair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FirstOfPair",
                table: "UserLinks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "InvitedBy",
                table: "UserLinks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SecondOfPair",
                table: "UserLinks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // One-off for the links already there. The pair in the order UserLink.PairOf gives, which matches
            // Postgres's uuid order, and the first inviter as who invited. Re-invites used to add a link each time, so
            // each pair keeps its live link, or else its latest, and the older finished ones go
            migrationBuilder.Sql(
                """
                UPDATE "UserLinks"
                SET "InvitedBy" = "UserId",
                    "FirstOfPair" = LEAST("UserId", "InviteeId"),
                    "SecondOfPair" = GREATEST("UserId", "InviteeId");

                DELETE FROM "UserLinks" AS link
                USING (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "FirstOfPair", "SecondOfPair"
                               ORDER BY ("Status" IN ('Pending', 'Accepted')) DESC, "InvitedAt" DESC) AS keep
                    FROM "UserLinks") AS ranked
                WHERE link."Id" = ranked."Id" AND ranked.keep > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_UserLinks_OneLinkPerPair",
                table: "UserLinks",
                columns: new[] { "FirstOfPair", "SecondOfPair" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLinks_OneLinkPerPair",
                table: "UserLinks");

            migrationBuilder.DropColumn(
                name: "FirstOfPair",
                table: "UserLinks");

            migrationBuilder.DropColumn(
                name: "InvitedBy",
                table: "UserLinks");

            migrationBuilder.DropColumn(
                name: "SecondOfPair",
                table: "UserLinks");
        }
    }
}
