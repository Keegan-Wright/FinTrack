using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class TransactionDayIndexAndMatchCheckpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BankTransactions_UserId",
                table: "BankTransactions");

            migrationBuilder.AddColumn<string>(
                name: "OccurredAtDayBlindIndex",
                table: "BankTransactions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "TransferMatchCheckpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchedFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferMatchCheckpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransferMatchCheckpoints_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_UserId_OccurredAtDayBlindIndex",
                table: "BankTransactions",
                columns: new[] { "UserId", "OccurredAtDayBlindIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferMatchCheckpoints_UserId",
                table: "TransferMatchCheckpoints",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransferMatchCheckpoints");

            migrationBuilder.DropIndex(
                name: "IX_BankTransactions_UserId_OccurredAtDayBlindIndex",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "OccurredAtDayBlindIndex",
                table: "BankTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_UserId",
                table: "BankTransactions",
                column: "UserId");
        }
    }
}
