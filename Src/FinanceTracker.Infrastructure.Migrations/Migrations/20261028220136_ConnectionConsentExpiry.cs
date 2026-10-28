using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class ConnectionConsentExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentExpiresAt",
                table: "BankConnections",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Existing connections didn't store the provider's expiry, UK consent lasts 90 days from when it was given
            migrationBuilder.Sql("""UPDATE "BankConnections" SET "ConsentExpiresAt" = "ConsentGrantedAt" + interval '90 days';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsentExpiresAt",
                table: "BankConnections");
        }
    }
}
