using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignDashboardProductionContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MappingJson",
                table: "legacy_import_batches",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "journal_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "journal_entries",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "GBP");

            migrationBuilder.AddColumn<DateTime>(
                name: "MergedAt",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MergedIntoCustomerId",
                table: "customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE journal_entries AS journal
                SET "BranchId" = transaction."BranchId",
                    "Currency" = transaction."Currency"
                FROM financial_transactions AS transaction
                WHERE transaction."JournalEntryId" = journal."Id";

                UPDATE journal_entries AS journal
                SET "BranchId" = invoice."BranchId",
                    "Currency" = invoice."Currency"
                FROM invoices AS invoice
                WHERE journal."SourceType" IN ('Invoice', 'CreditNote')
                  AND journal."SourceId" = invoice."Id"
                  AND journal."BranchId" IS NULL;

                UPDATE journal_entries AS journal
                SET "Currency" = line."Currency"
                FROM (
                    SELECT DISTINCT ON ("JournalEntryId") "JournalEntryId", "Currency"
                    FROM journal_lines
                    ORDER BY "JournalEntryId", "CreatedAt"
                ) AS line
                WHERE line."JournalEntryId" = journal."Id";

                UPDATE journal_entries AS journal
                SET "BranchId" = branch."Id"
                FROM branches AS branch
                WHERE journal."BranchId" IS NULL
                  AND (
                    (journal."Currency" = 'GBP' AND branch."Code" = 'UK-MAN') OR
                    (journal."Currency" = 'NGN' AND branch."Code" = 'NG-LOS') OR
                    (journal."Currency" = 'CNY' AND branch."Code" = 'CN-GZ')
                  );
                """);

            migrationBuilder.CreateTable(
                name: "accounting_periods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Period = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReopenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReopenedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_periods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accounting_periods_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "report_export_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    FiltersJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    FileContent = table.Column<byte[]>(type: "bytea", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_export_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_report_export_jobs_user_accounts_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Message = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    Path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsUrgent = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_notifications_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_staff_notifications_user_accounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_notification_preferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    InAppEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EmailEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ShipmentAlerts = table.Column<bool>(type: "boolean", nullable: false),
                    FinanceAlerts = table.Column<bool>(type: "boolean", nullable: false),
                    SecurityAlerts = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_notification_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_notification_preferences_user_accounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_BranchId_Currency_EntryDate",
                table: "journal_entries",
                columns: new[] { "BranchId", "Currency", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_MergedIntoCustomerId",
                table: "customers",
                column: "MergedIntoCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_periods_BranchId_Currency_Period",
                table: "accounting_periods",
                columns: new[] { "BranchId", "Currency", "Period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounting_periods_IsClosed_Period",
                table: "accounting_periods",
                columns: new[] { "IsClosed", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_report_export_jobs_RequestedByUserId_CreatedAt",
                table: "report_export_jobs",
                columns: new[] { "RequestedByUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_report_export_jobs_Status_ExpiresAt",
                table: "report_export_jobs",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_notifications_BranchId_CreatedAt",
                table: "staff_notifications",
                columns: new[] { "BranchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_notifications_UserAccountId_ReadAt_CreatedAt",
                table: "staff_notifications",
                columns: new[] { "UserAccountId", "ReadAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_notification_preferences_UserAccountId",
                table: "user_notification_preferences",
                column: "UserAccountId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_customers_customers_MergedIntoCustomerId",
                table: "customers",
                column: "MergedIntoCustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_journal_entries_branches_BranchId",
                table: "journal_entries",
                column: "BranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_customers_customers_MergedIntoCustomerId",
                table: "customers");

            migrationBuilder.DropForeignKey(
                name: "FK_journal_entries_branches_BranchId",
                table: "journal_entries");

            migrationBuilder.DropTable(
                name: "accounting_periods");

            migrationBuilder.DropTable(
                name: "report_export_jobs");

            migrationBuilder.DropTable(
                name: "staff_notifications");

            migrationBuilder.DropTable(
                name: "user_notification_preferences");

            migrationBuilder.DropIndex(
                name: "IX_journal_entries_BranchId_Currency_EntryDate",
                table: "journal_entries");

            migrationBuilder.DropIndex(
                name: "IX_customers_MergedIntoCustomerId",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "MappingJson",
                table: "legacy_import_batches");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "MergedAt",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "MergedIntoCustomerId",
                table: "customers");
        }
    }
}
