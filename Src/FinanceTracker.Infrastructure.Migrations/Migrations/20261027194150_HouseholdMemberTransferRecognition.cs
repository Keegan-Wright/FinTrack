using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class HouseholdMemberTransferRecognition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayeeNames",
                table: "HouseholdMembers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "Transfer_PartnerHouseholdMemberId",
                table: "BankTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HouseholdMemberLinkedAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SortCode = table.Column<string>(type: "text", nullable: true),
                    Number = table.Column<string>(type: "text", nullable: true),
                    Iban = table.Column<string>(type: "text", nullable: true),
                    HouseholdMemberId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdMemberLinkedAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdMemberLinkedAccounts_HouseholdMembers_HouseholdMem~",
                        column: x => x.HouseholdMemberId,
                        principalTable: "HouseholdMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMemberLinkedAccounts_HouseholdMemberId",
                table: "HouseholdMemberLinkedAccounts",
                column: "HouseholdMemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseholdMemberLinkedAccounts");

            migrationBuilder.DropColumn(
                name: "PayeeNames",
                table: "HouseholdMembers");

            migrationBuilder.DropColumn(
                name: "Transfer_PartnerHouseholdMemberId",
                table: "BankTransactions");
        }
    }
}
