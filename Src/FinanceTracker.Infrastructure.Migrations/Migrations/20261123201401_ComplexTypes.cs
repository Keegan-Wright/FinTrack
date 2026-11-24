using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations.Migrations;

/// <inheritdoc />
public partial class _20261123201401_ComplexTypes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Metadata_IntegrityTag",
            table: "BankTransactions");

        migrationBuilder.DropColumn(
            name: "Balance_IntegrityTag",
            table: "BankAccounts");

        migrationBuilder.DropColumn(
            name: "Card_IntegrityTag",
            table: "BankAccounts");

        migrationBuilder.DropColumn(
            name: "Identifier_IntegrityTag",
            table: "BankAccounts");

        migrationBuilder.DropColumn(
            name: "Balance_IntegrityTag",
            table: "BalanceSnapshots");

        migrationBuilder.AlterColumn<string>(
            name: "Conditions",
            table: "Rules",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]",
            oldClrType: typeof(string),
            oldType: "jsonb",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Actions",
            table: "Rules",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]",
            oldClrType: typeof(string),
            oldType: "jsonb",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "LowBalanceAlerts",
            table: "NotificationSettings",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]",
            oldClrType: typeof(string),
            oldType: "jsonb",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Gaps",
            table: "JointAccounts",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]",
            oldClrType: typeof(string),
            oldType: "jsonb",
            oldNullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LinkedAccounts",
            table: "HouseholdMembers",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]");

        // Each member's linked accounts move into their row, still encrypted, before their table goes
        migrationBuilder.Sql("""
            UPDATE "HouseholdMembers" AS member
            SET "LinkedAccounts" = linked."Accounts"
            FROM (
                SELECT "HouseholdMemberId",
                       jsonb_agg(jsonb_build_object('SortCode', "SortCode", 'Number', "Number", 'Iban', "Iban"))
                           AS "Accounts"
                FROM "HouseholdMemberLinkedAccounts"
                GROUP BY "HouseholdMemberId"
            ) AS linked
            WHERE linked."HouseholdMemberId" = member."Id";
            """);

        migrationBuilder.DropTable(
            name: "HouseholdMemberLinkedAccounts");

        migrationBuilder.AlterColumn<string>(
            name: "Limits",
            table: "Budgets",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]",
            oldClrType: typeof(string),
            oldType: "jsonb",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Amounts",
            table: "Bills",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]",
            oldClrType: typeof(string),
            oldType: "jsonb",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Splits",
            table: "BankTransactions",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]",
            oldClrType: typeof(string),
            oldType: "jsonb",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "RuleTags",
            table: "BankTransactions",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]",
            oldClrType: typeof(string),
            oldType: "jsonb",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "HiddenClassifications",
            table: "BankTransactions",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]",
            oldClrType: typeof(string),
            oldType: "jsonb",
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Conditions",
            table: "Rules",
            type: "jsonb",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "jsonb");

        migrationBuilder.AlterColumn<string>(
            name: "Actions",
            table: "Rules",
            type: "jsonb",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "jsonb");

        migrationBuilder.AlterColumn<string>(
            name: "LowBalanceAlerts",
            table: "NotificationSettings",
            type: "jsonb",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "jsonb");

        migrationBuilder.AlterColumn<string>(
            name: "Gaps",
            table: "JointAccounts",
            type: "jsonb",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "jsonb");

        migrationBuilder.AlterColumn<string>(
            name: "Limits",
            table: "Budgets",
            type: "jsonb",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "jsonb");

        migrationBuilder.AlterColumn<string>(
            name: "Amounts",
            table: "Bills",
            type: "jsonb",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "jsonb");

        migrationBuilder.AlterColumn<string>(
            name: "Splits",
            table: "BankTransactions",
            type: "jsonb",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "jsonb");

        migrationBuilder.AlterColumn<string>(
            name: "RuleTags",
            table: "BankTransactions",
            type: "jsonb",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "jsonb");

        migrationBuilder.AlterColumn<string>(
            name: "HiddenClassifications",
            table: "BankTransactions",
            type: "jsonb",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "jsonb");

        migrationBuilder.AddColumn<string>(
            name: "Metadata_IntegrityTag",
            table: "BankTransactions",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Balance_IntegrityTag",
            table: "BankAccounts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Card_IntegrityTag",
            table: "BankAccounts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Identifier_IntegrityTag",
            table: "BankAccounts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Balance_IntegrityTag",
            table: "BalanceSnapshots",
            type: "text",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "HouseholdMemberLinkedAccounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                Iban = table.Column<string>(type: "text", nullable: true),
                IntegrityTag = table.Column<string>(type: "text", nullable: true),
                Number = table.Column<string>(type: "text", nullable: true),
                SortCode = table.Column<string>(type: "text", nullable: true)
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

        // Back into their own table before the column goes, tagged again at startup
        migrationBuilder.Sql("""
            INSERT INTO "HouseholdMemberLinkedAccounts" ("Id", "HouseholdMemberId", "SortCode", "Number", "Iban")
            SELECT gen_random_uuid(), member."Id", account ->> 'SortCode', account ->> 'Number', account ->> 'Iban'
            FROM "HouseholdMembers" AS member, jsonb_array_elements(member."LinkedAccounts") AS account;
            """);

        migrationBuilder.DropColumn(
            name: "LinkedAccounts",
            table: "HouseholdMembers");
    }
}
