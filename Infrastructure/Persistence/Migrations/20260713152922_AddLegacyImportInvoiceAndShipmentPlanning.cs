using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyImportInvoiceAndShipmentPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShipmentMovementId",
                table: "shipment_containers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "shipment_containers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "NotSpecified");

            migrationBuilder.AddColumn<decimal>(
                name: "AppliedCreditAmount",
                table: "invoices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "invoices",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "Unknown client");

            migrationBuilder.AddColumn<string>(
                name: "IssuerBranchCode",
                table: "invoices",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "GLOBAL");

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalInvoiceId",
                table: "invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "invoices",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Unpaid");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Sales");

            migrationBuilder.AddColumn<string>(
                name: "AssignedTransportMode",
                table: "cargo_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId",
                table: "cargo_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShipmentMovementId",
                table: "cargo_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE invoices AS invoice
                SET "CustomerName" = COALESCE(NULLIF(BTRIM(customer."FullName"), ''), 'Unknown client'),
                    "IssuerBranchCode" = COALESCE(
                        (SELECT branch."Code"
                         FROM branches AS branch
                         WHERE branch."CountryCode" = customer."CountryCode" AND branch."IsActive"
                         ORDER BY branch."Code"
                         LIMIT 1),
                        (SELECT branch."Code"
                         FROM branches AS branch
                         WHERE branch."IsActive"
                         ORDER BY branch."Code"
                         LIMIT 1),
                        'GLOBAL'),
                    "Type" = 'Sales',
                    "PaymentStatus" = CASE
                        WHEN invoice."Status" = 'Paid' OR invoice."AmountPaid" >= invoice."TotalAmount" THEN 'Paid'
                        WHEN invoice."Status" = 'PartiallyPaid' OR invoice."AmountPaid" > 0 THEN 'PartiallyPaid'
                        ELSE 'Unpaid'
                    END
                FROM customers AS customer
                WHERE invoice."CustomerId" = customer."Id";
                """);

            migrationBuilder.CreateTable(
                name: "legacy_import_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    ValidRows = table.Column<int>(type: "integer", nullable: false),
                    DuplicateRows = table.Column<int>(type: "integer", nullable: false),
                    ErrorRows = table.Column<int>(type: "integer", nullable: false),
                    ImportedRows = table.Column<int>(type: "integer", nullable: false),
                    CommittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CommittedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legacy_import_batches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shipment_movements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CarrierName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CarrierCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    FlightNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    VesselName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    VoyageNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    OriginBranchCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DestinationBranchCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ScheduledDepartureAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstimatedArrivalAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualDepartureAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualArrivalAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ShippingBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shipment_movements_shipping_batches_ShippingBatchId",
                        column: x => x.ShippingBatchId,
                        principalTable: "shipping_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "legacy_import_rows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegacyImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SheetName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DataJson = table.Column<string>(type: "jsonb", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ImportedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legacy_import_rows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_legacy_import_rows_legacy_import_batches_LegacyImportBatchId",
                        column: x => x.LegacyImportBatchId,
                        principalTable: "legacy_import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cargo_allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CargoItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentContainerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ContainerType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CarrierName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AssignedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UnassignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UnassignedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cargo_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cargo_allocations_cargo_items_CargoItemId",
                        column: x => x.CargoItemId,
                        principalTable: "cargo_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cargo_allocations_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cargo_allocations_shipment_containers_ShipmentContainerId",
                        column: x => x.ShipmentContainerId,
                        principalTable: "shipment_containers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cargo_allocations_shipment_movements_ShipmentMovementId",
                        column: x => x.ShipmentMovementId,
                        principalTable: "shipment_movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_containers_ShipmentMovementId_Type_IsActive",
                table: "shipment_containers",
                columns: new[] { "ShipmentMovementId", "Type", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_CustomerId_IssueDate",
                table: "invoices",
                columns: new[] { "CustomerId", "IssueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_IssuerBranchCode_IssueDate",
                table: "invoices",
                columns: new[] { "IssuerBranchCode", "IssueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_OriginalInvoiceId",
                table: "invoices",
                column: "OriginalInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_PaymentStatus_IssueDate",
                table: "invoices",
                columns: new[] { "PaymentStatus", "IssueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_Type_IssueDate",
                table: "invoices",
                columns: new[] { "Type", "IssueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_cargo_items_AssignedTransportMode_Status_CreatedAt",
                table: "cargo_items",
                columns: new[] { "AssignedTransportMode", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_cargo_items_InvoiceId",
                table: "cargo_items",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_items_ShipmentMovementId",
                table: "cargo_items",
                column: "ShipmentMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_allocations_CargoItemId",
                table: "cargo_allocations",
                column: "CargoItemId",
                unique: true,
                filter: "\"UnassignedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_allocations_InvoiceId_AssignedAt",
                table: "cargo_allocations",
                columns: new[] { "InvoiceId", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_cargo_allocations_ShipmentContainerId",
                table: "cargo_allocations",
                column: "ShipmentContainerId");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_allocations_ShipmentMovementId_UnassignedAt_AssignedAt",
                table: "cargo_allocations",
                columns: new[] { "ShipmentMovementId", "UnassignedAt", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_legacy_import_batches_FileSha256",
                table: "legacy_import_batches",
                column: "FileSha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_legacy_import_batches_Status_CreatedAt",
                table: "legacy_import_batches",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_legacy_import_rows_LegacyImportBatchId_EntityType_RowNumber",
                table: "legacy_import_rows",
                columns: new[] { "LegacyImportBatchId", "EntityType", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_legacy_import_rows_LegacyImportBatchId_Status",
                table: "legacy_import_rows",
                columns: new[] { "LegacyImportBatchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_movements_CarrierName_ScheduledDepartureAt",
                table: "shipment_movements",
                columns: new[] { "CarrierName", "ScheduledDepartureAt" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_movements_Mode_Status_ScheduledDepartureAt",
                table: "shipment_movements",
                columns: new[] { "Mode", "Status", "ScheduledDepartureAt" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_movements_MovementCode",
                table: "shipment_movements",
                column: "MovementCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipment_movements_OriginBranchCode_DestinationBranchCode_S~",
                table: "shipment_movements",
                columns: new[] { "OriginBranchCode", "DestinationBranchCode", "ScheduledDepartureAt" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_movements_ShippingBatchId",
                table: "shipment_movements",
                column: "ShippingBatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_cargo_items_invoices_InvoiceId",
                table: "cargo_items",
                column: "InvoiceId",
                principalTable: "invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_cargo_items_shipment_movements_ShipmentMovementId",
                table: "cargo_items",
                column: "ShipmentMovementId",
                principalTable: "shipment_movements",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_invoices_OriginalInvoiceId",
                table: "invoices",
                column: "OriginalInvoiceId",
                principalTable: "invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_shipment_containers_shipment_movements_ShipmentMovementId",
                table: "shipment_containers",
                column: "ShipmentMovementId",
                principalTable: "shipment_movements",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cargo_items_invoices_InvoiceId",
                table: "cargo_items");

            migrationBuilder.DropForeignKey(
                name: "FK_cargo_items_shipment_movements_ShipmentMovementId",
                table: "cargo_items");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_invoices_OriginalInvoiceId",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_shipment_containers_shipment_movements_ShipmentMovementId",
                table: "shipment_containers");

            migrationBuilder.DropTable(
                name: "cargo_allocations");

            migrationBuilder.DropTable(
                name: "legacy_import_rows");

            migrationBuilder.DropTable(
                name: "shipment_movements");

            migrationBuilder.DropTable(
                name: "legacy_import_batches");

            migrationBuilder.DropIndex(
                name: "IX_shipment_containers_ShipmentMovementId_Type_IsActive",
                table: "shipment_containers");

            migrationBuilder.DropIndex(
                name: "IX_invoices_CustomerId_IssueDate",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_IssuerBranchCode_IssueDate",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_OriginalInvoiceId",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_PaymentStatus_IssueDate",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_Type_IssueDate",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_cargo_items_AssignedTransportMode_Status_CreatedAt",
                table: "cargo_items");

            migrationBuilder.DropIndex(
                name: "IX_cargo_items_InvoiceId",
                table: "cargo_items");

            migrationBuilder.DropIndex(
                name: "IX_cargo_items_ShipmentMovementId",
                table: "cargo_items");

            migrationBuilder.DropColumn(
                name: "ShipmentMovementId",
                table: "shipment_containers");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "shipment_containers");

            migrationBuilder.DropColumn(
                name: "AppliedCreditAmount",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "IssuerBranchCode",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "OriginalInvoiceId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "AssignedTransportMode",
                table: "cargo_items");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "cargo_items");

            migrationBuilder.DropColumn(
                name: "ShipmentMovementId",
                table: "cargo_items");

        }
    }
}
