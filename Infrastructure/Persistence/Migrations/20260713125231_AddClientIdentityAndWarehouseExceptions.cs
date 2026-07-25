using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientIdentityAndWarehouseExceptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_customers_Email",
                table: "customers");

            migrationBuilder.CreateSequence(
                name: "invoice_number_seq",
                startValue: 0L,
                minValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedFullName",
                table: "user_accounts",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "customers",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedFullName",
                table: "customers",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPhone",
                table: "customers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE user_accounts
                SET "NormalizedFullName" = upper(regexp_replace(trim("FullName"), '\s+', ' ', 'g'));

                UPDATE customers
                SET "NormalizedFullName" = upper(regexp_replace(trim("FullName"), '\s+', ' ', 'g')),
                    "NormalizedEmail" = upper(trim("Email")),
                    "NormalizedPhone" = nullif(regexp_replace("Phone", '[^0-9]', '', 'g'), '');

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM user_accounts GROUP BY "NormalizedFullName" HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Duplicate normalized user names must be resolved before this migration can continue.';
                    END IF;
                    IF EXISTS (SELECT 1 FROM customers GROUP BY "NormalizedFullName" HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Duplicate normalized client names must be resolved before this migration can continue.';
                    END IF;
                    IF EXISTS (SELECT 1 FROM customers GROUP BY "NormalizedEmail" HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Duplicate normalized client emails must be resolved before this migration can continue.';
                    END IF;
                    IF EXISTS (SELECT 1 FROM customers WHERE "NormalizedPhone" IS NOT NULL
                               GROUP BY "NormalizedPhone" HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Duplicate normalized client phone numbers must be resolved before this migration can continue.';
                    END IF;
                END $$;

                SELECT setval('invoice_number_seq',
                    COALESCE((SELECT max(substring("InvoiceNumber" from '^BGL([0-9]+)INV$')::bigint) + 1
                              FROM invoices WHERE "InvoiceNumber" ~ '^BGL[0-9]+INV$'), 0), false);
                """);

            migrationBuilder.CreateTable(
                name: "client_duplicate_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InputFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SubmittedFullName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SubmittedEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SubmittedPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubmittedInvoiceNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    MatchSummaryJson = table.Column<string>(type: "jsonb", nullable: false),
                    HasExactConflict = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SelectedCustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_duplicate_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_duplicate_reviews_customers_SelectedCustomerId",
                        column: x => x.SelectedCustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_exception_cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CargoItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpectedCustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ObservedCustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpectedTrackingNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ObservedTrackingNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExpectedMarkerCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ObservedMarkerCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExpectedLocation = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ObservedLocation = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EvidenceDocumentIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsClientVisible = table.Column<bool>(type: "boolean", nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClientNotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_exception_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warehouse_exception_cases_cargo_items_CargoItemId",
                        column: x => x.CargoItemId,
                        principalTable: "cargo_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_warehouse_exception_cases_customers_ExpectedCustomerId",
                        column: x => x.ExpectedCustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_warehouse_exception_cases_customers_ObservedCustomerId",
                        column: x => x.ObservedCustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_warehouse_exception_cases_user_accounts_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_NormalizedFullName",
                table: "user_accounts",
                column: "NormalizedFullName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_NormalizedEmail",
                table: "customers",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_NormalizedFullName",
                table: "customers",
                column: "NormalizedFullName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_NormalizedPhone",
                table: "customers",
                column: "NormalizedPhone",
                unique: true,
                filter: "\"NormalizedPhone\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_client_duplicate_reviews_InputFingerprint_Status_ExpiresAt",
                table: "client_duplicate_reviews",
                columns: new[] { "InputFingerprint", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_client_duplicate_reviews_SelectedCustomerId",
                table: "client_duplicate_reviews",
                column: "SelectedCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_exception_cases_AssignedToUserId",
                table: "warehouse_exception_cases",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_exception_cases_CargoItemId",
                table: "warehouse_exception_cases",
                column: "CargoItemId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_exception_cases_CaseNumber",
                table: "warehouse_exception_cases",
                column: "CaseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_exception_cases_ExpectedCustomerId",
                table: "warehouse_exception_cases",
                column: "ExpectedCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_exception_cases_ObservedCustomerId",
                table: "warehouse_exception_cases",
                column: "ObservedCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_exception_cases_Status_Severity_ReportedAt",
                table: "warehouse_exception_cases",
                columns: new[] { "Status", "Severity", "ReportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_exception_cases_Type_Status",
                table: "warehouse_exception_cases",
                columns: new[] { "Type", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_duplicate_reviews");

            migrationBuilder.DropTable(
                name: "warehouse_exception_cases");

            migrationBuilder.DropIndex(
                name: "IX_user_accounts_NormalizedFullName",
                table: "user_accounts");

            migrationBuilder.DropIndex(
                name: "IX_customers_NormalizedEmail",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_customers_NormalizedFullName",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_customers_NormalizedPhone",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "NormalizedFullName",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "NormalizedFullName",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "NormalizedPhone",
                table: "customers");

            migrationBuilder.DropSequence(
                name: "invoice_number_seq");

            migrationBuilder.CreateIndex(
                name: "IX_customers_Email",
                table: "customers",
                column: "Email",
                unique: true);
        }
    }
}
