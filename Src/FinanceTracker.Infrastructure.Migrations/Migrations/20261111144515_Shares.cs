using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class Shares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Shares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemType = table.Column<string>(type: "text", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    SharedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shares_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Shares_UserLinks_LinkId",
                        column: x => x.LinkId,
                        principalTable: "UserLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Shares_ItemType_ItemId",
                table: "Shares",
                columns: new[] { "ItemType", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Shares_LinkId_ItemType_ItemId",
                table: "Shares",
                columns: new[] { "LinkId", "ItemType", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shares_UserId",
                table: "Shares",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Shares");
        }
    }
}
