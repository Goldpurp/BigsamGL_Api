using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyAndMscContainerTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_shipment_milestones_CarrierProvider_ExternalEventId",
                table: "shipment_milestones");

            migrationBuilder.AddColumn<Guid>(
                name: "ShipmentContainerId",
                table: "cargo_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "exchange_rate_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    QuoteCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(24,10)", precision: 24, scale: 10, nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsFallback = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exchange_rate_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shipment_containers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShippingBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ContainerNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CarrierBookingReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BillOfLadingNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CurrentLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EstimatedArrivalAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastEventAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_containers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shipment_containers_shipping_batches_ShippingBatchId",
                        column: x => x.ShippingBatchId,
                        principalTable: "shipping_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "carrier_tracking_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentContainerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExternalEventId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    EventClassifier = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EventTypeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EventStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstimatedArrivalAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RawPayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PropagatedCargoCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_carrier_tracking_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_carrier_tracking_events_shipment_containers_ShipmentContain~",
                        column: x => x.ShipmentContainerId,
                        principalTable: "shipment_containers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_milestones_CargoItemId_CarrierProvider_ExternalEve~",
                table: "shipment_milestones",
                columns: new[] { "CargoItemId", "CarrierProvider", "ExternalEventId" },
                unique: true,
                filter: "\"ExternalEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_items_ShipmentContainerId",
                table: "cargo_items",
                column: "ShipmentContainerId");

            migrationBuilder.CreateIndex(
                name: "IX_carrier_tracking_events_Provider_ExternalEventId",
                table: "carrier_tracking_events",
                columns: new[] { "Provider", "ExternalEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_carrier_tracking_events_ShipmentContainerId_OccurredAt",
                table: "carrier_tracking_events",
                columns: new[] { "ShipmentContainerId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_exchange_rate_snapshots_BaseCurrency_QuoteCurrency_FetchedAt",
                table: "exchange_rate_snapshots",
                columns: new[] { "BaseCurrency", "QuoteCurrency", "FetchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_containers_Provider_ContainerNumber",
                table: "shipment_containers",
                columns: new[] { "Provider", "ContainerNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipment_containers_ShippingBatchId",
                table: "shipment_containers",
                column: "ShippingBatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_cargo_items_shipment_containers_ShipmentContainerId",
                table: "cargo_items",
                column: "ShipmentContainerId",
                principalTable: "shipment_containers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cargo_items_shipment_containers_ShipmentContainerId",
                table: "cargo_items");

            migrationBuilder.DropTable(
                name: "carrier_tracking_events");

            migrationBuilder.DropTable(
                name: "exchange_rate_snapshots");

            migrationBuilder.DropTable(
                name: "shipment_containers");

            migrationBuilder.DropIndex(
                name: "IX_shipment_milestones_CargoItemId_CarrierProvider_ExternalEve~",
                table: "shipment_milestones");

            migrationBuilder.DropIndex(
                name: "IX_cargo_items_ShipmentContainerId",
                table: "cargo_items");

            migrationBuilder.DropColumn(
                name: "ShipmentContainerId",
                table: "cargo_items");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_milestones_CarrierProvider_ExternalEventId",
                table: "shipment_milestones",
                columns: new[] { "CarrierProvider", "ExternalEventId" },
                unique: true,
                filter: "\"ExternalEventId\" IS NOT NULL");
        }
    }
}
