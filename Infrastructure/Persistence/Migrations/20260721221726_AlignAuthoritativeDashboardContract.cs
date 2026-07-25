using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignAuthoritativeDashboardContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_manifest_entry_receiver_email",
                table: "operational_manifest_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_manifest_entry_receiver_phone",
                table: "operational_manifest_entries");

            migrationBuilder.DropIndex(
                name: "IX_financial_transactions_BranchCode_OccurredAt",
                table: "financial_transactions");

            migrationBuilder.DropIndex(
                name: "IX_customers_CreatedAt",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_customers_NormalizedEmail",
                table: "customers");

            migrationBuilder.CreateSequence(
                name: "client_number_seq",
                minValue: 1L);

            migrationBuilder.CreateSequence(
                name: "credit_note_number_seq",
                minValue: 1L);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "BookingReference",
                table: "operational_manifests",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "operational_manifests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "CarrierName",
                table: "operational_manifests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarrierReference",
                table: "operational_manifests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContainerOrFlightNumber",
                table: "operational_manifests",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationCity",
                table: "operational_manifests",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OriginCity",
                table: "operational_manifests",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "operational_manifests",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "TrackingProvider",
                table: "operational_manifests",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReceiverPhone",
                table: "operational_manifest_entries",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "ReceiverEmail",
                table: "operational_manifest_entries",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);

            migrationBuilder.AddColumn<string>(
                name: "Contents",
                table: "operational_manifest_entries",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerShipmentId",
                table: "operational_manifest_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "operational_manifest_entries",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "invoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "invoices",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "invoice_lines",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "financial_transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "FreightClassification",
                table: "financial_transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "FreightClassifiedAt",
                table: "financial_transactions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalOfTransactionId",
                table: "financial_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                table: "customers",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "customers",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "customers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ClientNumber",
                table: "customers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            // Establish the three authoritative hubs before assigning required branch foreign keys.
            migrationBuilder.Sql("""
                INSERT INTO branches ("Id", "Code", "Name", "CountryCode", "TimeZone", "Address", "IsActive",
                                      "CreatedAt", "CreatedBy")
                VALUES
                  ('00000000-0000-0000-0000-000000000101', 'UK-MAN', 'Manchester Warehouse', 'GB',
                   'Europe/London', '1 Kay St, Manchester M11 2DX', TRUE, CURRENT_TIMESTAMP, 'migration'),
                  ('00000000-0000-0000-0000-000000000102', 'NG-LOS', 'Lagos Cargo Hub', 'NG',
                   'Africa/Lagos', 'Lagos, Nigeria', TRUE, CURRENT_TIMESTAMP, 'migration'),
                  ('00000000-0000-0000-0000-000000000103', 'CN-GZ', 'Guangzhou Sourcing Hub', 'CN',
                   'Asia/Shanghai', 'Guangzhou, China', TRUE, CURRENT_TIMESTAMP, 'migration')
                ON CONFLICT ("Code") DO NOTHING;

                UPDATE customers
                SET "Email" = NULLIF(btrim("Email"), ''),
                    "NormalizedEmail" = NULLIF(btrim("NormalizedEmail"), '');

                WITH numbered AS (
                    SELECT "Id", row_number() OVER (ORDER BY "CreatedAt", "Id") AS number
                    FROM customers
                )
                UPDATE customers AS customer
                SET "ClientNumber" = 'ID-' || lpad(numbered.number::text, 2, '0'),
                    "BranchId" = COALESCE(
                        (SELECT "Id" FROM branches WHERE "Code" = CASE
                            WHEN upper(customer."CountryCode") IN ('NG','NGA') THEN 'NG-LOS'
                            WHEN upper(customer."CountryCode") IN ('CN','CHN') THEN 'CN-GZ'
                            ELSE 'UK-MAN' END LIMIT 1),
                        (SELECT "Id" FROM branches ORDER BY "CreatedAt", "Id" LIMIT 1))
                FROM numbered
                WHERE customer."Id" = numbered."Id";

                UPDATE invoices AS invoice
                SET "BranchId" = COALESCE(
                        (SELECT "Id" FROM branches WHERE "Code" = invoice."IssuerBranchCode" LIMIT 1),
                        (SELECT customer."BranchId" FROM customers AS customer
                         WHERE customer."Id" = invoice."CustomerId" LIMIT 1),
                        (SELECT "Id" FROM branches ORDER BY "CreatedAt", "Id" LIMIT 1)),
                    "RowVersion" = 1;

                UPDATE payments AS payment
                SET "BranchId" = COALESCE(
                    (SELECT invoice."BranchId" FROM invoices AS invoice
                     WHERE invoice."Id" = payment."InvoiceId" LIMIT 1),
                    (SELECT "Id" FROM branches ORDER BY "CreatedAt", "Id" LIMIT 1));

                UPDATE financial_transactions AS transaction
                SET "BranchId" = COALESCE(
                        (SELECT "Id" FROM branches WHERE "Code" = transaction."BranchCode" LIMIT 1),
                        (SELECT payment."BranchId" FROM payments AS payment
                         WHERE transaction."SourceType" = 'Payment' AND payment."Id" = transaction."SourceId" LIMIT 1),
                        (SELECT "Id" FROM branches ORDER BY "CreatedAt", "Id" LIMIT 1)),
                    "Direction" = CASE transaction."Direction"
                        WHEN 'MoneyIn' THEN 'Credit' WHEN 'MoneyOut' THEN 'Debit'
                        ELSE transaction."Direction" END,
                    "Type" = CASE transaction."Type"
                        WHEN 'InvoicePayment' THEN 'SalesPayment'
                        WHEN 'PurchasePayout' THEN 'Purchase'
                        WHEN 'ExpensePayout' THEN 'Expense'
                        WHEN 'Refund' THEN 'Reversal'
                        ELSE transaction."Type" END,
                    "Status" = CASE WHEN transaction."Status" = 'Voided' THEN 'Reversed'
                                    ELSE transaction."Status" END,
                    "FreightClassification" = 'Other',
                    "FreightClassifiedAt" = transaction."OccurredAt";

                UPDATE payments SET "Status" = 'Reversed' WHERE "Status" = 'Refunded';
                UPDATE invoice_lines SET "Unit" = 'service' WHERE btrim("Unit") = '';

                UPDATE operational_manifests AS manifest
                SET "BranchId" = COALESCE(
                        (SELECT route."OriginBranchId" FROM service_routes AS route
                         WHERE route."Id" = manifest."ServiceRouteId" LIMIT 1),
                        (SELECT "Id" FROM branches WHERE "CountryCode" = CASE
                            WHEN upper(manifest."OriginCountryCode") IN ('UK','GBR') THEN 'GB'
                            WHEN upper(manifest."OriginCountryCode") = 'NGA' THEN 'NG'
                            WHEN upper(manifest."OriginCountryCode") = 'CHN' THEN 'CN'
                            ELSE upper(manifest."OriginCountryCode") END LIMIT 1),
                        (SELECT "Id" FROM branches ORDER BY "CreatedAt", "Id" LIMIT 1)),
                    "OriginCity" = CASE
                        WHEN upper(manifest."OriginCountryCode") IN ('GB','GBR','UK') THEN 'Manchester'
                        WHEN upper(manifest."OriginCountryCode") IN ('NG','NGA') THEN 'Lagos'
                        WHEN upper(manifest."OriginCountryCode") IN ('CN','CHN') THEN 'Guangzhou'
                        ELSE 'Historic origin' END,
                    "DestinationCity" = CASE
                        WHEN upper(manifest."DestinationCountryCode") IN ('GB','GBR','UK') THEN 'Manchester'
                        WHEN upper(manifest."DestinationCountryCode") IN ('NG','NGA') THEN 'Lagos'
                        WHEN upper(manifest."DestinationCountryCode") IN ('CN','CHN') THEN 'Guangzhou'
                        ELSE 'Historic destination' END,
                    "RowVersion" = 1;

                SELECT setval('client_number_seq', GREATEST((SELECT count(*) FROM customers), 1),
                              (SELECT count(*) FROM customers) > 0);
                DO $$
                DECLARE highest bigint;
                BEGIN
                    SELECT max((regexp_match("InvoiceNumber", '^BGL([0-9]{4,})INV$'))[1]::bigint)
                    INTO highest FROM invoices WHERE "InvoiceNumber" ~ '^BGL[0-9]{4,}INV$';
                    PERFORM setval('invoice_number_seq', COALESCE(highest, 0), highest IS NOT NULL);
                END $$;
                """);

            migrationBuilder.CreateTable(
                name: "credit_notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreditNoteNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UsedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    State = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_notes", x => x.Id);
                    table.CheckConstraint("CK_credit_notes_amount", "\"OriginalAmount\" > 0");
                    table.CheckConstraint("CK_credit_notes_currency", "char_length(\"Currency\") = 3");
                    table.CheckConstraint("CK_credit_notes_reason", "length(btrim(\"Reason\")) > 0");
                    table.CheckConstraint("CK_credit_notes_used", "\"UsedAmount\" >= 0 AND \"UsedAmount\" <= \"OriginalAmount\"");
                    table.ForeignKey(
                        name: "FK_credit_notes_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_notes_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_notes_invoices_OriginalInvoiceId",
                        column: x => x.OriginalInvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_notes_user_accounts_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_notes_user_accounts_VoidedByUserId",
                        column: x => x.VoidedByUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InvoiceNumericFragment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReceiverName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    OriginCountryCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    OriginCity = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DestinationCountryCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    DestinationCityOrState = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DeliveryAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    HandoverMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GoodsType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Contents = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PieceCount = table.Column<int>(type: "integer", nullable: false),
                    PricingMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ActualWeightKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    ChargeableWeightKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    NegotiatedReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MovementState = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReferredByClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_shipments", x => x.Id);
                    table.CheckConstraint("CK_customer_shipments_delivery_address", "\"HandoverMethod\" <> 'Delivery' OR length(btrim(coalesce(\"DeliveryAddress\", ''))) > 0");
                    table.CheckConstraint("CK_customer_shipments_goods_type", "\"GoodsType\" IN ('Fragile','NonFragile')");
                    table.CheckConstraint("CK_customer_shipments_handover", "\"HandoverMethod\" IN ('Pickup','Delivery')");
                    table.CheckConstraint("CK_customer_shipments_invoice_number", "\"InvoiceNumber\" ~ '^BGL[0-9]{4,}INV$'");
                    table.CheckConstraint("CK_customer_shipments_method", "\"Method\" IN ('Air','Sea')");
                    table.CheckConstraint("CK_customer_shipments_piece_count", "\"PieceCount\" > 0");
                    table.CheckConstraint("CK_customer_shipments_pricing", "\"PricingMethod\" IN ('Weight','BoxSizesAndItems','NegotiatedPrice')");
                    table.CheckConstraint("CK_customer_shipments_row_version", "\"RowVersion\" > 0");
                    table.CheckConstraint("CK_customer_shipments_weights", "(\"ActualWeightKg\" IS NULL OR \"ActualWeightKg\" > 0) AND (\"ChargeableWeightKg\" IS NULL OR \"ChargeableWeightKg\" >= \"ActualWeightKg\")");
                    table.ForeignKey(
                        name: "FK_customer_shipments_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customer_shipments_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customer_shipments_customers_ReferredByClientId",
                        column: x => x.ReferredByClientId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customer_shipments_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customer_shipments_user_accounts_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_idempotency_records_user_accounts_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipping_price_catalogue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Group = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ServiceArea = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PriceKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LowerPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    UpperPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    HandlingFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MinimumCharge = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DispatchSchedule = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    EstimatedDelivery = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipping_price_catalogue", x => x.Id);
                    table.CheckConstraint("CK_shipping_price_currency", "char_length(\"Currency\") = 3");
                    table.CheckConstraint("CK_shipping_price_kind_values", "(\"PriceKind\" = 'CustomQuote' AND \"LowerPrice\" IS NULL AND \"UpperPrice\" IS NULL) OR (\"PriceKind\" IN ('Fixed','From') AND \"LowerPrice\" IS NOT NULL) OR (\"PriceKind\" = 'Range' AND \"LowerPrice\" IS NOT NULL AND \"UpperPrice\" IS NOT NULL)");
                    table.CheckConstraint("CK_shipping_price_values", "(\"LowerPrice\" IS NULL OR \"LowerPrice\" >= 0) AND (\"UpperPrice\" IS NULL OR \"UpperPrice\" >= \"LowerPrice\") AND (\"HandlingFee\" IS NULL OR \"HandlingFee\" >= 0) AND (\"MinimumCharge\" IS NULL OR \"MinimumCharge\" >= 0)");
                    table.ForeignKey(
                        name: "FK_shipping_price_catalogue_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_branch_memberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_branch_memberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_branch_memberships_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_branch_memberships_user_accounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credit_applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppliedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_applications", x => x.Id);
                    table.CheckConstraint("CK_credit_applications_amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_credit_applications_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_applications_credit_notes_CreditNoteId",
                        column: x => x.CreditNoteId,
                        principalTable: "credit_notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_applications_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_applications_user_accounts_AppliedByUserId",
                        column: x => x.AppliedByUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_shipment_price_components",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CataloguePriceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CodeSnapshot = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DescriptionSnapshot = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    UnitSnapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_shipment_price_components", x => x.Id);
                    table.CheckConstraint("CK_customer_shipment_price_amount", "\"LineAmount\" >= 0");
                    table.CheckConstraint("CK_customer_shipment_price_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_customer_shipment_price_sort", "\"SortOrder\" >= 0");
                    table.CheckConstraint("CK_customer_shipment_price_unit_price", "\"UnitPrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_customer_shipment_price_components_customer_shipments_Custo~",
                        column: x => x.CustomerShipmentId,
                        principalTable: "customer_shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipping_price_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShippingPriceCatalogueItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipping_price_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shipping_price_history_shipping_price_catalogue_ShippingPri~",
                        column: x => x.ShippingPriceCatalogueItemId,
                        principalTable: "shipping_price_catalogue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipping_price_history_user_accounts_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Preserve historic users, invoices and manifest allocations in the new active read model.
            migrationBuilder.Sql("""
                INSERT INTO staff_branch_memberships
                    ("Id", "UserAccountId", "BranchId", "IsPrimary", "CreatedAt", "CreatedBy")
                SELECT md5(profile."UserAccountId"::text || branch."Id"::text)::uuid,
                       profile."UserAccountId", branch."Id", TRUE, CURRENT_TIMESTAMP, 'migration'
                FROM staff_profiles AS profile
                JOIN branches AS branch ON branch."Code" = profile."BranchCode"
                ON CONFLICT DO NOTHING;

                INSERT INTO customer_shipments
                    ("Id", "BranchId", "CustomerId", "InvoiceId", "InvoiceNumber", "InvoiceNumericFragment",
                     "ReceiverName", "Method", "OriginCountryCode", "OriginCity", "DestinationCountryCode",
                     "DestinationCityOrState", "DeliveryAddress", "HandoverMethod", "GoodsType", "Description",
                     "Contents", "PieceCount", "PricingMethod", "ActualWeightKg", "ChargeableWeightKg",
                     "NegotiatedReason", "MovementState", "CustomerNote", "RowVersion", "CreatedAt", "CreatedBy")
                SELECT md5(invoice."Id"::text || ':customer-shipment')::uuid,
                       invoice."BranchId", invoice."CustomerId", invoice."Id", invoice."InvoiceNumber",
                       regexp_replace(invoice."InvoiceNumber", '[^0-9]', '', 'g'),
                       COALESCE(NULLIF(entry."ReceiverName", ''), customer."FullName"),
                       CASE WHEN manifest."LogisticsMethod" = 'Ship' THEN 'Sea' ELSE 'Air' END,
                       branch."CountryCode",
                       CASE branch."Code" WHEN 'UK-MAN' THEN 'Manchester' WHEN 'NG-LOS' THEN 'Lagos'
                            WHEN 'CN-GZ' THEN 'Guangzhou' ELSE branch."Name" END,
                       COALESCE(NULLIF(manifest."DestinationCountryCode", ''),
                           CASE WHEN branch."CountryCode" = 'GB' THEN 'NG' ELSE 'GB' END),
                       COALESCE(NULLIF(entry."HandoverCity", ''), NULLIF(manifest."DestinationCity", ''),
                           CASE WHEN branch."CountryCode" = 'GB' THEN 'Lagos' ELSE 'Manchester' END),
                       NULLIF(entry."HandoverAddress", ''),
                       CASE WHEN entry."HandoverMethod" = 'SentOut' THEN 'Delivery' ELSE 'Pickup' END,
                       'NonFragile',
                       COALESCE(NULLIF(entry."PackageDescription", ''), first_line."Description",
                                'Migrated historic shipment'),
                       entry."Contents", GREATEST(COALESCE(ceil(entry."Quantity")::integer, 1), 1),
                       'NegotiatedPrice', NULLIF(entry."ActualWeightKg", 0),
                       CASE WHEN entry."CostWeightKg" > 0 THEN entry."CostWeightKg" ELSE NULL END,
                       'Historic commercial price migrated without changing the issued invoice.',
                       CASE WHEN entry."Id" IS NULL THEN 'New' ELSE 'AssignedToManifest' END,
                       invoice."Notes", 1, invoice."CreatedAt", COALESCE(invoice."CreatedBy", 'migration')
                FROM invoices AS invoice
                JOIN customers AS customer ON customer."Id" = invoice."CustomerId"
                JOIN branches AS branch ON branch."Id" = invoice."BranchId"
                LEFT JOIN operational_manifest_entries AS entry ON entry."InvoiceId" = invoice."Id"
                LEFT JOIN operational_manifests AS manifest ON manifest."Id" = entry."ManifestId"
                LEFT JOIN LATERAL (
                    SELECT line."Description" FROM invoice_lines AS line
                    WHERE line."InvoiceId" = invoice."Id" ORDER BY line."CreatedAt", line."Id" LIMIT 1
                ) AS first_line ON TRUE
                WHERE invoice."Type" = 'Sales' AND invoice."Status" <> 'Void'
                  AND invoice."InvoiceNumber" ~ '^BGL[0-9]{4,}INV$';

                UPDATE operational_manifest_entries AS entry
                SET "CustomerShipmentId" = shipment."Id", "Contents" = shipment."Contents", "RowVersion" = 1
                FROM customer_shipments AS shipment
                WHERE shipment."InvoiceId" = entry."InvoiceId";

                INSERT INTO customer_shipment_price_components
                    ("Id", "CustomerShipmentId", "Type", "CodeSnapshot", "DescriptionSnapshot", "UnitSnapshot",
                     "Quantity", "UnitPrice", "LineAmount", "SortOrder", "CreatedAt", "CreatedBy")
                SELECT md5(shipment."Id"::text || line."Id"::text)::uuid, shipment."Id", 'Negotiated',
                       'MIGRATED', line."Description", COALESCE(NULLIF(line."Unit", ''), 'service'),
                       line."Quantity", line."UnitPrice", line."LineTotal",
                       (row_number() OVER (PARTITION BY shipment."Id" ORDER BY line."CreatedAt", line."Id") - 1)::integer,
                       line."CreatedAt", 'migration'
                FROM customer_shipments AS shipment
                JOIN invoice_lines AS line ON line."InvoiceId" = shipment."InvoiceId";

                UPDATE financial_transactions AS transaction
                SET "FreightClassification" = CASE shipment."Method"
                    WHEN 'Air' THEN 'Air' WHEN 'Sea' THEN 'Sea' ELSE 'Other' END
                FROM payments AS payment
                JOIN customer_shipments AS shipment ON shipment."InvoiceId" = payment."InvoiceId"
                WHERE transaction."SourceType" = 'Payment' AND transaction."SourceId" = payment."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_payments_BranchId",
                table: "payments",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifests_BranchId_LogisticsMethod_OriginCountr~",
                table: "operational_manifests",
                columns: new[] { "BranchId", "LogisticsMethod", "OriginCountryCode", "OriginCity", "DestinationCountryCode", "DestinationCity", "ManifestDate" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_entries_CustomerShipmentId",
                table: "operational_manifest_entries",
                column: "CustomerShipmentId",
                unique: true,
                filter: "\"CustomerShipmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_BranchId",
                table: "invoices",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_BranchId_Currency_Direction_Type_Occ~",
                table: "financial_transactions",
                columns: new[] { "BranchId", "Currency", "Direction", "Type", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_BranchId_Currency_OccurredAt",
                table: "financial_transactions",
                columns: new[] { "BranchId", "Currency", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_BranchId_FreightClassification_Occur~",
                table: "financial_transactions",
                columns: new[] { "BranchId", "FreightClassification", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_ReversalOfTransactionId",
                table: "financial_transactions",
                column: "ReversalOfTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_customers_BranchId_CreatedAt",
                table: "customers",
                columns: new[] { "BranchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_ClientNumber",
                table: "customers",
                column: "ClientNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_NormalizedEmail",
                table: "customers",
                column: "NormalizedEmail",
                unique: true,
                filter: "\"NormalizedEmail\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_credit_applications_AppliedByUserId_IdempotencyKey_CreditNo~",
                table: "credit_applications",
                columns: new[] { "AppliedByUserId", "IdempotencyKey", "CreditNoteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_applications_BranchId",
                table: "credit_applications",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_applications_CreditNoteId_AppliedAt",
                table: "credit_applications",
                columns: new[] { "CreditNoteId", "AppliedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_applications_InvoiceId_AppliedAt",
                table: "credit_applications",
                columns: new[] { "InvoiceId", "AppliedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_ApprovedByUserId",
                table: "credit_notes",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_BranchId_Currency_State_IssuedAt",
                table: "credit_notes",
                columns: new[] { "BranchId", "Currency", "State", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_CreditNoteNumber",
                table: "credit_notes",
                column: "CreditNoteNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_CustomerId_Currency_State_IssuedAt",
                table: "credit_notes",
                columns: new[] { "CustomerId", "Currency", "State", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_OriginalInvoiceId",
                table: "credit_notes",
                column: "OriginalInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_VoidedByUserId",
                table: "credit_notes",
                column: "VoidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_shipment_price_components_CataloguePriceId",
                table: "customer_shipment_price_components",
                column: "CataloguePriceId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_shipment_price_components_CustomerShipmentId_SortO~",
                table: "customer_shipment_price_components",
                columns: new[] { "CustomerShipmentId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_shipments_BranchId_CreatedAt",
                table: "customer_shipments",
                columns: new[] { "BranchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_shipments_BranchId_CustomerId_CreatedAt",
                table: "customer_shipments",
                columns: new[] { "BranchId", "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_shipments_BranchId_DestinationCountryCode_Destinat~",
                table: "customer_shipments",
                columns: new[] { "BranchId", "DestinationCountryCode", "DestinationCityOrState", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_shipments_BranchId_Method_MovementState_CreatedAt",
                table: "customer_shipments",
                columns: new[] { "BranchId", "Method", "MovementState", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_shipments_CancelledByUserId",
                table: "customer_shipments",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_shipments_CustomerId",
                table: "customer_shipments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_shipments_InvoiceId",
                table: "customer_shipments",
                column: "InvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_shipments_InvoiceNumber",
                table: "customer_shipments",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_shipments_InvoiceNumericFragment",
                table: "customer_shipments",
                column: "InvoiceNumericFragment");

            migrationBuilder.CreateIndex(
                name: "IX_customer_shipments_ReferredByClientId",
                table: "customer_shipments",
                column: "ReferredByClientId");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_ActorUserId_Operation_Key",
                table: "idempotency_records",
                columns: new[] { "ActorUserId", "Operation", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_ExpiresAt",
                table: "idempotency_records",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_shipping_price_catalogue_BranchId_NormalizedName",
                table: "shipping_price_catalogue",
                columns: new[] { "BranchId", "NormalizedName" });

            migrationBuilder.CreateIndex(
                name: "IX_shipping_price_catalogue_BranchId_State_Method_Category",
                table: "shipping_price_catalogue",
                columns: new[] { "BranchId", "State", "Method", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_shipping_price_catalogue_NormalizedCode",
                table: "shipping_price_catalogue",
                column: "NormalizedCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipping_price_history_ChangedByUserId",
                table: "shipping_price_history",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_shipping_price_history_ShippingPriceCatalogueItemId_Version",
                table: "shipping_price_history",
                columns: new[] { "ShippingPriceCatalogueItemId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_branch_memberships_BranchId_UserAccountId",
                table: "staff_branch_memberships",
                columns: new[] { "BranchId", "UserAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_branch_memberships_UserAccountId_BranchId",
                table: "staff_branch_memberships",
                columns: new[] { "UserAccountId", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_branch_memberships_UserAccountId_IsPrimary",
                table: "staff_branch_memberships",
                columns: new[] { "UserAccountId", "IsPrimary" },
                unique: true,
                filter: "\"IsPrimary\" = TRUE");

            migrationBuilder.AddForeignKey(
                name: "FK_customers_branches_BranchId",
                table: "customers",
                column: "BranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_financial_transactions_branches_BranchId",
                table: "financial_transactions",
                column: "BranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_financial_transactions_financial_transactions_ReversalOfTra~",
                table: "financial_transactions",
                column: "ReversalOfTransactionId",
                principalTable: "financial_transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_branches_BranchId",
                table: "invoices",
                column: "BranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_manifest_entries_customer_shipments_CustomerShi~",
                table: "operational_manifest_entries",
                column: "CustomerShipmentId",
                principalTable: "customer_shipments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_manifests_branches_BranchId",
                table: "operational_manifests",
                column: "BranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_branches_BranchId",
                table: "payments",
                column: "BranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_customers_branches_BranchId",
                table: "customers");

            migrationBuilder.DropForeignKey(
                name: "FK_financial_transactions_branches_BranchId",
                table: "financial_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_financial_transactions_financial_transactions_ReversalOfTra~",
                table: "financial_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_branches_BranchId",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_manifest_entries_customer_shipments_CustomerShi~",
                table: "operational_manifest_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_manifests_branches_BranchId",
                table: "operational_manifests");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_branches_BranchId",
                table: "payments");

            migrationBuilder.DropTable(
                name: "credit_applications");

            migrationBuilder.DropTable(
                name: "customer_shipment_price_components");

            migrationBuilder.DropTable(
                name: "idempotency_records");

            migrationBuilder.DropTable(
                name: "shipping_price_history");

            migrationBuilder.DropTable(
                name: "staff_branch_memberships");

            migrationBuilder.DropTable(
                name: "credit_notes");

            migrationBuilder.DropTable(
                name: "customer_shipments");

            migrationBuilder.DropTable(
                name: "shipping_price_catalogue");

            migrationBuilder.DropIndex(
                name: "IX_payments_BranchId",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifests_BranchId_LogisticsMethod_OriginCountr~",
                table: "operational_manifests");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifest_entries_CustomerShipmentId",
                table: "operational_manifest_entries");

            migrationBuilder.DropIndex(
                name: "IX_invoices_BranchId",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_financial_transactions_BranchId_Currency_Direction_Type_Occ~",
                table: "financial_transactions");

            migrationBuilder.DropIndex(
                name: "IX_financial_transactions_BranchId_Currency_OccurredAt",
                table: "financial_transactions");

            migrationBuilder.DropIndex(
                name: "IX_financial_transactions_BranchId_FreightClassification_Occur~",
                table: "financial_transactions");

            migrationBuilder.DropIndex(
                name: "IX_financial_transactions_ReversalOfTransactionId",
                table: "financial_transactions");

            migrationBuilder.DropIndex(
                name: "IX_customers_BranchId_CreatedAt",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_customers_ClientNumber",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_customers_NormalizedEmail",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "BookingReference",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "CarrierName",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "CarrierReference",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "ContainerOrFlightNumber",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "DestinationCity",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "OriginCity",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "TrackingProvider",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "Contents",
                table: "operational_manifest_entries");

            migrationBuilder.DropColumn(
                name: "CustomerShipmentId",
                table: "operational_manifest_entries");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "operational_manifest_entries");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "invoice_lines");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "financial_transactions");

            migrationBuilder.DropColumn(
                name: "FreightClassification",
                table: "financial_transactions");

            migrationBuilder.DropColumn(
                name: "FreightClassifiedAt",
                table: "financial_transactions");

            migrationBuilder.DropColumn(
                name: "ReversalOfTransactionId",
                table: "financial_transactions");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "ClientNumber",
                table: "customers");

            migrationBuilder.DropSequence(
                name: "client_number_seq");

            migrationBuilder.DropSequence(
                name: "credit_note_number_seq");

            migrationBuilder.AlterColumn<string>(
                name: "ReceiverPhone",
                table: "operational_manifest_entries",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReceiverEmail",
                table: "operational_manifest_entries",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                table: "customers",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "customers",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_manifest_entry_receiver_email",
                table: "operational_manifest_entries",
                sql: "length(btrim(\"ReceiverEmail\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_manifest_entry_receiver_phone",
                table: "operational_manifest_entries",
                sql: "length(btrim(\"ReceiverPhone\")) > 0");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_BranchCode_OccurredAt",
                table: "financial_transactions",
                columns: new[] { "BranchCode", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_CreatedAt",
                table: "customers",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_customers_NormalizedEmail",
                table: "customers",
                column: "NormalizedEmail",
                unique: true);
        }
    }
}
