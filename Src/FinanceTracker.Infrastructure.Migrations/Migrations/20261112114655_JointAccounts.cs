using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class JointAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JointAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShownAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    HiddenAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    MergedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JointAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JointAccounts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JointAccounts_BankAccounts_HiddenAccountId",
                        column: x => x.HiddenAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JointAccounts_BankAccounts_ShownAccountId",
                        column: x => x.ShownAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JointAccounts_UserLinks_LinkId",
                        column: x => x.LinkId,
                        principalTable: "UserLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JointAccounts_HiddenAccountId",
                table: "JointAccounts",
                column: "HiddenAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JointAccounts_LinkId",
                table: "JointAccounts",
                column: "LinkId");

            migrationBuilder.CreateIndex(
                name: "IX_JointAccounts_ShownAccountId",
                table: "JointAccounts",
                column: "ShownAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JointAccounts_UserId",
                table: "JointAccounts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JointAccounts");
        }
    }
}
