using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeManifestShipmentHeaderWithEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_operational_manifests_cargo_items_CargoItemId",
                table: "operational_manifests");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_manifests_customers_CustomerId",
                table: "operational_manifests");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_manifests_invoices_InvoiceId",
                table: "operational_manifests");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_manifests_shipping_batches_ShippingBatchId",
                table: "operational_manifests");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifests_CargoItemId",
                table: "operational_manifests");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifests_CustomerId_ManifestDate",
                table: "operational_manifests");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifests_InvoiceId",
                table: "operational_manifests");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifests_InvoiceNumber_ManifestDate",
                table: "operational_manifests");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifests_LogisticsMethod_ManifestDate_Currency",
                table: "operational_manifests");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifests_PaymentType_ManifestDate",
                table: "operational_manifests");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifests_ShippingBatchId",
                table: "operational_manifests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_manifests_actual_weight",
                table: "operational_manifests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_manifests_amount",
                table: "operational_manifests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_manifests_cost_weight",
                table: "operational_manifests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_manifests_cost_weight_rule",
                table: "operational_manifests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_manifests_quantity",
                table: "operational_manifests");

            migrationBuilder.AddColumn<string>(
                name: "ShipmentBookingId",
                table: "operational_manifests",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ShipmentId",
                table: "operational_manifests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "operational_manifest_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CargoItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShippingBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShipmentMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShipmentContainerId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceNumberSnapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CustomerNameSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CustomerPhoneSnapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CustomerEmailSnapshot = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    TrackingNumberSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SenderName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PackageDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ActualWeightKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    CostWeightKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PaymentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    HandoverMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    HandoverAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    HandoverCity = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    MovementCodeSnapshot = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ContainerNumberSnapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_manifest_entries", x => x.Id);
                    table.CheckConstraint("CK_manifest_entry_actual_weight", "\"ActualWeightKg\" > 0");
                    table.CheckConstraint("CK_manifest_entry_amount", "\"Amount\" >= 0");
                    table.CheckConstraint("CK_manifest_entry_cost_weight", "\"CostWeightKg\" >= \"ActualWeightKg\"");
                    table.CheckConstraint("CK_manifest_entry_currency", "length(\"Currency\") = 3");
                    table.CheckConstraint("CK_manifest_entry_description", "length(btrim(\"PackageDescription\")) > 0");
                    table.CheckConstraint("CK_manifest_entry_handover_method", "\"HandoverMethod\" IN ('HubCollection','SentOut')");
                    table.CheckConstraint("CK_manifest_entry_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_manifest_entry_sender", "length(btrim(\"SenderName\")) > 0");
                    table.ForeignKey(
                        name: "FK_operational_manifest_entries_cargo_items_CargoItemId",
                        column: x => x.CargoItemId,
                        principalTable: "cargo_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_operational_manifest_entries_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_manifest_entries_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_operational_manifest_entries_operational_manifests_Manifest~",
                        column: x => x.ManifestId,
                        principalTable: "operational_manifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_operational_manifest_entries_shipment_containers_ShipmentCo~",
                        column: x => x.ShipmentContainerId,
                        principalTable: "shipment_containers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_operational_manifest_entries_shipment_movements_ShipmentMov~",
                        column: x => x.ShipmentMovementId,
                        principalTable: "shipment_movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_operational_manifest_entries_shipping_batches_ShippingBatch~",
                        column: x => x.ShippingBatchId,
                        principalTable: "shipping_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_operational_manifest_entries_user_accounts_AddedByUserId",
                        column: x => x.AddedByUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                UPDATE operational_manifests AS manifest
                SET "ShipmentId" = manifest."ManifestNumber",
                    "ShipmentBookingId" = COALESCE(
                        (SELECT COALESCE(movement."MasterTransportReference", movement."MovementCode")
                         FROM operational_manifest_shipment_links AS link
                         JOIN shipment_movements AS movement ON movement."Id" = link."ShipmentMovementId"
                         WHERE link."OperationalManifestId" = manifest."Id"
                         ORDER BY link."LinkedAt", link."Id"
                         LIMIT 1) || '-' || right(manifest."ManifestNumber", 8),
                        manifest."ManifestNumber" || '-BOOKING')
                """);

            migrationBuilder.Sql("""
                WITH ranked_links AS (
                    SELECT link.*,
                           row_number() OVER (
                               PARTITION BY link."OperationalManifestId"
                               ORDER BY link."LinkedAt", link."Id") AS entry_rank
                    FROM operational_manifest_shipment_links AS link
                )
                INSERT INTO operational_manifest_entries
                    ("Id", "ManifestId", "CustomerId", "InvoiceId", "CargoItemId",
                     "ShippingBatchId", "ShipmentMovementId", "ShipmentContainerId",
                     "InvoiceNumberSnapshot", "CustomerNameSnapshot", "CustomerPhoneSnapshot",
                     "CustomerEmailSnapshot", "TrackingNumberSnapshot", "SenderName",
                     "PackageDescription", "Quantity", "ActualWeightKg", "CostWeightKg",
                     "Amount", "Currency", "PaymentType", "HandoverMethod", "HandoverAddress",
                     "HandoverCity", "MovementCodeSnapshot", "ContainerNumberSnapshot", "Notes",
                     "AddedAt", "AddedByUserId", "CreatedAt", "CreatedBy",
                     "LastModifiedAt", "LastModifiedBy")
                SELECT link."Id", manifest."Id", manifest."CustomerId", manifest."InvoiceId",
                       link."CargoItemId", link."ShippingBatchId", link."ShipmentMovementId",
                       link."ShipmentContainerId", manifest."InvoiceNumber", manifest."ClientName",
                       manifest."ClientPhone", customer."Email", link."TrackingNumberSnapshot",
                       link."SenderName", link."PackageDescription", link."Quantity",
                       link."ActualWeightKg", link."CostWeightKg",
                       CASE WHEN link.entry_rank = 1 THEN manifest."Amount" ELSE 0 END,
                       manifest."Currency", manifest."PaymentType", link."HandoverMethod",
                       link."HandoverAddress", link."HandoverCity", link."MovementCodeSnapshot",
                       link."ContainerNumberSnapshot", link."Notes", link."LinkedAt",
                       link."LinkedByUserId", link."CreatedAt", link."CreatedBy",
                       link."LastModifiedAt", link."LastModifiedBy"
                FROM ranked_links AS link
                JOIN operational_manifests AS manifest
                    ON manifest."Id" = link."OperationalManifestId"
                JOIN customers AS customer ON customer."Id" = manifest."CustomerId"
                """);

            migrationBuilder.Sql("""
                INSERT INTO operational_manifest_entries
                    ("Id", "ManifestId", "CustomerId", "InvoiceId", "CargoItemId",
                     "ShippingBatchId", "ShipmentMovementId", "ShipmentContainerId",
                     "InvoiceNumberSnapshot", "CustomerNameSnapshot", "CustomerPhoneSnapshot",
                     "CustomerEmailSnapshot", "TrackingNumberSnapshot", "SenderName",
                     "PackageDescription", "Quantity", "ActualWeightKg", "CostWeightKg",
                     "Amount", "Currency", "PaymentType", "HandoverMethod", "HandoverAddress",
                     "HandoverCity", "MovementCodeSnapshot", "ContainerNumberSnapshot", "Notes",
                     "AddedAt", "AddedByUserId", "CreatedAt", "CreatedBy",
                     "LastModifiedAt", "LastModifiedBy")
                SELECT gen_random_uuid(), manifest."Id", manifest."CustomerId", manifest."InvoiceId",
                       manifest."CargoItemId", COALESCE(manifest."ShippingBatchId", cargo."BatchId"),
                       cargo."ShipmentMovementId", cargo."ShipmentContainerId", manifest."InvoiceNumber",
                       manifest."ClientName", manifest."ClientPhone", customer."Email",
                       cargo."TrackingNumber", manifest."SenderName", manifest."Contents",
                       manifest."Quantity", manifest."ActualWeightKg", manifest."CostWeightKg",
                       manifest."Amount", manifest."Currency", manifest."PaymentType",
                       CASE WHEN manifest."DeliveryAddress" ILIKE 'Hub pickup:%'
                            THEN 'HubCollection' ELSE 'SentOut' END,
                       manifest."DeliveryAddress", manifest."DeliveryCity", movement."MovementCode",
                       container."ContainerNumber", 'Migrated from the original invoice manifest.',
                       manifest."CreatedAt", manifest."CreatedByUserId", manifest."CreatedAt",
                       manifest."CreatedBy", manifest."LastModifiedAt", manifest."LastModifiedBy"
                FROM operational_manifests AS manifest
                JOIN customers AS customer ON customer."Id" = manifest."CustomerId"
                LEFT JOIN cargo_items AS cargo ON cargo."Id" = manifest."CargoItemId"
                LEFT JOIN shipment_movements AS movement ON movement."Id" = cargo."ShipmentMovementId"
                LEFT JOIN shipment_containers AS container ON container."Id" = cargo."ShipmentContainerId"
                WHERE NOT EXISTS (
                    SELECT 1 FROM operational_manifest_shipment_links AS link
                    WHERE link."OperationalManifestId" = manifest."Id")
                """);

            migrationBuilder.DropTable(
                name: "operational_manifest_shipment_links");

            migrationBuilder.DropColumn(name: "ActualWeightKg", table: "operational_manifests");
            migrationBuilder.DropColumn(name: "Amount", table: "operational_manifests");
            migrationBuilder.DropColumn(name: "CargoItemId", table: "operational_manifests");
            migrationBuilder.DropColumn(name: "ClientName", table: "operational_manifests");
            migrationBuilder.DropColumn(name: "ClientPhone", table: "operational_manifests");
            migrationBuilder.DropColumn(name: "Contents", table: "operational_manifests");
            migrationBuilder.DropColumn(name: "CostWeightKg", table: "operational_manifests");
            migrationBuilder.DropColumn(name: "Currency", table: "operational_manifests");
            migrationBuilder.DropColumn(name: "CustomerId", table: "operational_manifests");
            migrationBuilder.DropColumn(name: "DeliveryAddress", table: "operational_manifests");
            migrationBuilder.DropColumn(name: "DeliveryCity", table: "operational_manifests");
            migrationBuilder.DropColumn(name: "InvoiceId", table: "operational_manifests");
            migrationBuilder.DropColumn(name: "InvoiceNumber", table: "operational_manifests");
            migrationBuilder.DropColumn(name: "PaymentType", table: "operational_manifests");
            migrationBuilder.DropColumn(name: "Quantity", table: "operational_manifests");
            migrationBuilder.DropColumn(name: "SenderName", table: "operational_manifests");
            migrationBuilder.DropColumn(name: "ShippingBatchId", table: "operational_manifests");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifests_ShipmentBookingId",
                table: "operational_manifests",
                column: "ShipmentBookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifests_ShipmentId",
                table: "operational_manifests",
                column: "ShipmentId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_manifests_booking_id",
                table: "operational_manifests",
                sql: "length(btrim(\"ShipmentBookingId\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_manifests_shipment_id",
                table: "operational_manifests",
                sql: "length(btrim(\"ShipmentId\")) > 0");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_entries_AddedByUserId",
                table: "operational_manifest_entries",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_entries_CargoItemId",
                table: "operational_manifest_entries",
                column: "CargoItemId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_entries_CustomerId",
                table: "operational_manifest_entries",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_entries_InvoiceId",
                table: "operational_manifest_entries",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_entries_ManifestId_AddedAt",
                table: "operational_manifest_entries",
                columns: new[] { "ManifestId", "AddedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_entries_ManifestId_CargoItemId",
                table: "operational_manifest_entries",
                columns: new[] { "ManifestId", "CargoItemId" },
                unique: true,
                filter: "\"CargoItemId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_entries_ManifestId_CustomerId_AddedAt",
                table: "operational_manifest_entries",
                columns: new[] { "ManifestId", "CustomerId", "AddedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_entries_ManifestId_InvoiceNumberSnapsh~",
                table: "operational_manifest_entries",
                columns: new[] { "ManifestId", "InvoiceNumberSnapshot" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_entries_ShipmentContainerId",
                table: "operational_manifest_entries",
                column: "ShipmentContainerId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_entries_ShipmentMovementId_ShipmentCon~",
                table: "operational_manifest_entries",
                columns: new[] { "ShipmentMovementId", "ShipmentContainerId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_entries_ShippingBatchId",
                table: "operational_manifest_entries",
                column: "ShippingBatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
#pragma warning disable CS0162
            throw new NotSupportedException(
                "This data-preserving manifest normalization cannot be safely reversed automatically.");

            migrationBuilder.DropTable(
                name: "operational_manifest_entries");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifests_ShipmentBookingId",
                table: "operational_manifests");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifests_ShipmentId",
                table: "operational_manifests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_manifests_booking_id",
                table: "operational_manifests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_manifests_shipment_id",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "ShipmentBookingId",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "ShipmentId",
                table: "operational_manifests");

            migrationBuilder.AddColumn<decimal>(
                name: "ActualWeightKg",
                table: "operational_manifests",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "operational_manifests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "CargoItemId",
                table: "operational_manifests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientName",
                table: "operational_manifests",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClientPhone",
                table: "operational_manifests",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Contents",
                table: "operational_manifests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "CostWeightKg",
                table: "operational_manifests",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "operational_manifests",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "operational_manifests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddress",
                table: "operational_manifests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryCity",
                table: "operational_manifests",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId",
                table: "operational_manifests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "operational_manifests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaymentType",
                table: "operational_manifests",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "operational_manifests",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                table: "operational_manifests",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ShippingBatchId",
                table: "operational_manifests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "operational_manifest_shipment_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualWeightKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    CargoItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContainerNumberSnapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CostWeightKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    HandoverAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    HandoverCity = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    HandoverMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LinkedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementCodeSnapshot = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OperationalManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    SenderName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ShipmentContainerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShipmentMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShippingBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrackingNumberSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_manifest_shipment_links", x => x.Id);
                    table.CheckConstraint("CK_manifest_package_actual_weight", "\"ActualWeightKg\" > 0");
                    table.CheckConstraint("CK_manifest_package_cost_weight", "\"CostWeightKg\" >= \"ActualWeightKg\"");
                    table.CheckConstraint("CK_manifest_package_description", "length(btrim(\"PackageDescription\")) > 0");
                    table.CheckConstraint("CK_manifest_package_handover_method", "\"HandoverMethod\" IN ('HubCollection','SentOut')");
                    table.CheckConstraint("CK_manifest_package_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_manifest_package_sender", "length(btrim(\"SenderName\")) > 0");
                    table.ForeignKey(
                        name: "FK_operational_manifest_shipment_links_cargo_items_CargoItemId",
                        column: x => x.CargoItemId,
                        principalTable: "cargo_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_manifest_shipment_links_operational_manifests_O~",
                        column: x => x.OperationalManifestId,
                        principalTable: "operational_manifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_operational_manifest_shipment_links_shipment_containers_Shi~",
                        column: x => x.ShipmentContainerId,
                        principalTable: "shipment_containers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_operational_manifest_shipment_links_shipment_movements_Ship~",
                        column: x => x.ShipmentMovementId,
                        principalTable: "shipment_movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_operational_manifest_shipment_links_shipping_batches_Shippi~",
                        column: x => x.ShippingBatchId,
                        principalTable: "shipping_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_operational_manifest_shipment_links_user_accounts_LinkedByU~",
                        column: x => x.LinkedByUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifests_CargoItemId",
                table: "operational_manifests",
                column: "CargoItemId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifests_CustomerId_ManifestDate",
                table: "operational_manifests",
                columns: new[] { "CustomerId", "ManifestDate" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifests_InvoiceId",
                table: "operational_manifests",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifests_InvoiceNumber_ManifestDate",
                table: "operational_manifests",
                columns: new[] { "InvoiceNumber", "ManifestDate" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifests_LogisticsMethod_ManifestDate_Currency",
                table: "operational_manifests",
                columns: new[] { "LogisticsMethod", "ManifestDate", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifests_PaymentType_ManifestDate",
                table: "operational_manifests",
                columns: new[] { "PaymentType", "ManifestDate" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifests_ShippingBatchId",
                table: "operational_manifests",
                column: "ShippingBatchId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_manifests_actual_weight",
                table: "operational_manifests",
                sql: "\"ActualWeightKg\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_manifests_amount",
                table: "operational_manifests",
                sql: "\"Amount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_manifests_cost_weight",
                table: "operational_manifests",
                sql: "\"CostWeightKg\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_manifests_cost_weight_rule",
                table: "operational_manifests",
                sql: "\"CostWeightKg\" >= \"ActualWeightKg\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_manifests_quantity",
                table: "operational_manifests",
                sql: "\"Quantity\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_shipment_links_CargoItemId",
                table: "operational_manifest_shipment_links",
                column: "CargoItemId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_shipment_links_LinkedByUserId",
                table: "operational_manifest_shipment_links",
                column: "LinkedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_shipment_links_OperationalManifestId_C~",
                table: "operational_manifest_shipment_links",
                columns: new[] { "OperationalManifestId", "CargoItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_shipment_links_OperationalManifestId_H~",
                table: "operational_manifest_shipment_links",
                columns: new[] { "OperationalManifestId", "HandoverMethod", "LinkedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_shipment_links_OperationalManifestId_L~",
                table: "operational_manifest_shipment_links",
                columns: new[] { "OperationalManifestId", "LinkedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_shipment_links_ShipmentContainerId",
                table: "operational_manifest_shipment_links",
                column: "ShipmentContainerId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_shipment_links_ShipmentMovementId_Ship~",
                table: "operational_manifest_shipment_links",
                columns: new[] { "ShipmentMovementId", "ShipmentContainerId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_shipment_links_ShippingBatchId",
                table: "operational_manifest_shipment_links",
                column: "ShippingBatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_operational_manifests_cargo_items_CargoItemId",
                table: "operational_manifests",
                column: "CargoItemId",
                principalTable: "cargo_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_manifests_customers_CustomerId",
                table: "operational_manifests",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_manifests_invoices_InvoiceId",
                table: "operational_manifests",
                column: "InvoiceId",
                principalTable: "invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_manifests_shipping_batches_ShippingBatchId",
                table: "operational_manifests",
                column: "ShippingBatchId",
                principalTable: "shipping_batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
#pragma warning restore CS0162
        }
    }
}
