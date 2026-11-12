using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class HouseholdMemberLinkedUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LinkedUserId",
                table: "HouseholdMembers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMembers_LinkedUserId",
                table: "HouseholdMembers",
                column: "LinkedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMembers_UserId_LinkedUserId",
                table: "HouseholdMembers",
                columns: new[] { "UserId", "LinkedUserId" },
                unique: true,
                filter: "\"LinkedUserId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_HouseholdMembers_AspNetUsers_LinkedUserId",
                table: "HouseholdMembers",
                column: "LinkedUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HouseholdMembers_AspNetUsers_LinkedUserId",
                table: "HouseholdMembers");

            migrationBuilder.DropIndex(
                name: "IX_HouseholdMembers_LinkedUserId",
                table: "HouseholdMembers");

            migrationBuilder.DropIndex(
                name: "IX_HouseholdMembers_UserId_LinkedUserId",
                table: "HouseholdMembers");

            migrationBuilder.DropColumn(
                name: "LinkedUserId",
                table: "HouseholdMembers");
        }
    }
}
