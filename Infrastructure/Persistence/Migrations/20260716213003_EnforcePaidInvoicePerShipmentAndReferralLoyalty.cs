using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforcePaidInvoicePerShipmentAndReferralLoyalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_operational_manifest_entries_invoices_InvoiceId",
                table: "operational_manifest_entries");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifest_entries_InvoiceId",
                table: "operational_manifest_entries");

            migrationBuilder.Sql("""
                CREATE TEMP TABLE manifest_missing_invoices ON COMMIT DROP AS
                SELECT entry."Id" AS "EntryId",
                       md5('manifest-invoice-' || entry."Id"::text)::uuid AS "InvoiceId",
                       nextval('invoice_number_seq') AS "SequenceNumber"
                FROM operational_manifest_entries AS entry
                WHERE entry."InvoiceId" IS NULL;

                INSERT INTO invoices
                    ("Id", "CustomerId", "CustomerName", "InvoiceNumber", "Type", "OriginalInvoiceId",
                     "IssuerBranchCode", "IssueDate", "DueDate", "Status", "PaymentStatus",
                     "PreferredPaymentMethod", "Currency", "Subtotal", "TaxAmount", "DiscountAmount",
                     "TotalAmount", "AmountPaid", "AppliedCreditAmount", "Notes", "VerificationCode",
                     "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy")
                SELECT missing."InvoiceId", entry."CustomerId", entry."CustomerNameSnapshot",
                       'BGL' || lpad(missing."SequenceNumber"::text,
                           greatest(4, length(missing."SequenceNumber"::text)), '0') || 'INV',
                       'Sales', NULL, coalesce(origin."Code", 'GLOBAL'), entry."AddedAt", entry."AddedAt",
                       'Draft', 'Unpaid', entry."PaymentType", entry."Currency", entry."Amount", 0, 0,
                       entry."Amount", 0, 0, 'Backfilled for an existing shipment entry.',
                       upper(md5('manifest-verification-' || entry."Id"::text || random()::text)),
                       entry."CreatedAt", coalesce(entry."CreatedBy", 'migration'), NULL, NULL
                FROM manifest_missing_invoices AS missing
                JOIN operational_manifest_entries AS entry ON entry."Id" = missing."EntryId"
                JOIN operational_manifests AS manifest ON manifest."Id" = entry."ManifestId"
                LEFT JOIN service_routes AS route ON route."Id" = manifest."ServiceRouteId"
                LEFT JOIN branches AS origin ON origin."Id" = route."OriginBranchId";

                INSERT INTO invoice_lines
                    ("Id", "InvoiceId", "Description", "Quantity", "UnitPrice", "TaxRate", "LineTotal",
                     "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy")
                SELECT md5('manifest-invoice-line-' || entry."Id"::text)::uuid, missing."InvoiceId",
                       entry."PackageDescription", 1, entry."Amount", 0, entry."Amount",
                       entry."CreatedAt", coalesce(entry."CreatedBy", 'migration'), NULL, NULL
                FROM manifest_missing_invoices AS missing
                JOIN operational_manifest_entries AS entry ON entry."Id" = missing."EntryId";

                UPDATE operational_manifest_entries AS entry
                SET "InvoiceId" = missing."InvoiceId",
                    "InvoiceNumberSnapshot" = invoice."InvoiceNumber"
                FROM manifest_missing_invoices AS missing
                JOIN invoices AS invoice ON invoice."Id" = missing."InvoiceId"
                WHERE entry."Id" = missing."EntryId";

                CREATE TEMP TABLE manifest_duplicate_invoices ON COMMIT DROP AS
                SELECT ranked."EntryId", ranked."InvoiceId" AS "SourceInvoiceId",
                       md5('manifest-invoice-clone-' || ranked."EntryId"::text)::uuid AS "CloneInvoiceId",
                       nextval('invoice_number_seq') AS "SequenceNumber"
                FROM (
                    SELECT entry."Id" AS "EntryId", entry."InvoiceId",
                           row_number() OVER (
                               PARTITION BY entry."InvoiceId"
                               ORDER BY entry."AddedAt", entry."Id") AS "InvoiceUseNumber"
                    FROM operational_manifest_entries AS entry
                ) AS ranked
                WHERE ranked."InvoiceUseNumber" > 1;

                INSERT INTO invoices
                    ("Id", "CustomerId", "CustomerName", "InvoiceNumber", "Type", "OriginalInvoiceId",
                     "IssuerBranchCode", "IssueDate", "DueDate", "Status", "PaymentStatus",
                     "PreferredPaymentMethod", "Currency", "Subtotal", "TaxAmount", "DiscountAmount",
                     "TotalAmount", "AmountPaid", "AppliedCreditAmount", "Notes", "VerificationCode",
                     "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy")
                SELECT duplicate."CloneInvoiceId", entry."CustomerId", entry."CustomerNameSnapshot",
                       'BGL' || lpad(duplicate."SequenceNumber"::text,
                           greatest(4, length(duplicate."SequenceNumber"::text)), '0') || 'INV',
                       'Sales', NULL, source."IssuerBranchCode", entry."AddedAt", entry."AddedAt",
                       'Draft', 'Unpaid', entry."PaymentType", entry."Currency", entry."Amount", 0, 0,
                       entry."Amount", 0, 0,
                       'Separated from a previously shared invoice during one-invoice-per-shipment migration.',
                       upper(md5('manifest-clone-verification-' || entry."Id"::text || random()::text)),
                       entry."CreatedAt", coalesce(entry."CreatedBy", 'migration'), NULL, NULL
                FROM manifest_duplicate_invoices AS duplicate
                JOIN operational_manifest_entries AS entry ON entry."Id" = duplicate."EntryId"
                JOIN invoices AS source ON source."Id" = duplicate."SourceInvoiceId";

                INSERT INTO invoice_lines
                    ("Id", "InvoiceId", "Description", "Quantity", "UnitPrice", "TaxRate", "LineTotal",
                     "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy")
                SELECT md5('manifest-clone-line-' || entry."Id"::text)::uuid, duplicate."CloneInvoiceId",
                       entry."PackageDescription", 1, entry."Amount", 0, entry."Amount",
                       entry."CreatedAt", coalesce(entry."CreatedBy", 'migration'), NULL, NULL
                FROM manifest_duplicate_invoices AS duplicate
                JOIN operational_manifest_entries AS entry ON entry."Id" = duplicate."EntryId";

                UPDATE operational_manifest_entries AS entry
                SET "InvoiceId" = duplicate."CloneInvoiceId",
                    "InvoiceNumberSnapshot" = invoice."InvoiceNumber"
                FROM manifest_duplicate_invoices AS duplicate
                JOIN invoices AS invoice ON invoice."Id" = duplicate."CloneInvoiceId"
                WHERE entry."Id" = duplicate."EntryId";

                UPDATE operational_manifest_entries AS entry
                SET "InvoiceNumberSnapshot" = invoice."InvoiceNumber"
                FROM invoices AS invoice
                WHERE entry."InvoiceId" = invoice."Id"
                  AND (entry."InvoiceNumberSnapshot" IS NULL OR btrim(entry."InvoiceNumberSnapshot") = '');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumberSnapshot",
                table: "operational_manifest_entries",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "InvoiceId",
                table: "operational_manifest_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferralPointsAwarded",
                table: "operational_manifest_entries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferrerCustomerId",
                table: "operational_manifest_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferrerNameSnapshot",
                table: "operational_manifest_entries",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customer_loyalty_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AvailablePoints = table.Column<int>(type: "integer", nullable: false),
                    LifetimeEarnedPoints = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_loyalty_accounts", x => x.Id);
                    table.CheckConstraint("CK_loyalty_account_available_points", "\"AvailablePoints\" >= 0");
                    table.CheckConstraint("CK_loyalty_account_lifetime_points", "\"LifetimeEarnedPoints\" >= 0");
                    table.ForeignKey(
                        name: "FK_customer_loyalty_accounts_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "loyalty_point_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerLoyaltyAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferredCustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loyalty_point_transactions", x => x.Id);
                    table.CheckConstraint("CK_loyalty_transaction_points", "\"Points\" <> 0");
                    table.ForeignKey(
                        name: "FK_loyalty_point_transactions_customer_loyalty_accounts_Custom~",
                        column: x => x.CustomerLoyaltyAccountId,
                        principalTable: "customer_loyalty_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loyalty_point_transactions_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loyalty_point_transactions_customers_ReferredCustomerId",
                        column: x => x.ReferredCustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loyalty_point_transactions_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_entries_InvoiceId",
                table: "operational_manifest_entries",
                column: "InvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_entries_ReferrerCustomerId",
                table: "operational_manifest_entries",
                column: "ReferrerCustomerId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_manifest_entry_referral_points",
                table: "operational_manifest_entries",
                sql: "\"ReferralPointsAwarded\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_customer_loyalty_accounts_CustomerId",
                table: "customer_loyalty_accounts",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_loyalty_accounts_LifetimeEarnedPoints_CustomerId",
                table: "customer_loyalty_accounts",
                columns: new[] { "LifetimeEarnedPoints", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_point_transactions_CustomerId_OccurredAt_Id",
                table: "loyalty_point_transactions",
                columns: new[] { "CustomerId", "OccurredAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_point_transactions_CustomerLoyaltyAccountId",
                table: "loyalty_point_transactions",
                column: "CustomerLoyaltyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_point_transactions_InvoiceId",
                table: "loyalty_point_transactions",
                column: "InvoiceId",
                unique: true,
                filter: "\"InvoiceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_point_transactions_ReferredCustomerId",
                table: "loyalty_point_transactions",
                column: "ReferredCustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_operational_manifest_entries_customers_ReferrerCustomerId",
                table: "operational_manifest_entries",
                column: "ReferrerCustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_manifest_entries_invoices_InvoiceId",
                table: "operational_manifest_entries",
                column: "InvoiceId",
                principalTable: "invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_operational_manifest_entries_customers_ReferrerCustomerId",
                table: "operational_manifest_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_manifest_entries_invoices_InvoiceId",
                table: "operational_manifest_entries");

            migrationBuilder.DropTable(
                name: "loyalty_point_transactions");

            migrationBuilder.DropTable(
                name: "customer_loyalty_accounts");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifest_entries_InvoiceId",
                table: "operational_manifest_entries");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifest_entries_ReferrerCustomerId",
                table: "operational_manifest_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_manifest_entry_referral_points",
                table: "operational_manifest_entries");

            migrationBuilder.DropColumn(
                name: "ReferralPointsAwarded",
                table: "operational_manifest_entries");

            migrationBuilder.DropColumn(
                name: "ReferrerCustomerId",
                table: "operational_manifest_entries");

            migrationBuilder.DropColumn(
                name: "ReferrerNameSnapshot",
                table: "operational_manifest_entries");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumberSnapshot",
                table: "operational_manifest_entries",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<Guid>(
                name: "InvoiceId",
                table: "operational_manifest_entries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_entries_InvoiceId",
                table: "operational_manifest_entries",
                column: "InvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_operational_manifest_entries_invoices_InvoiceId",
                table: "operational_manifest_entries",
                column: "InvoiceId",
                principalTable: "invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
