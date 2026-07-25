using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManifestDirectionsAndPackageLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DestinationCountryCode",
                table: "operational_manifests",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "??");

            migrationBuilder.AddColumn<string>(
                name: "OriginCountryCode",
                table: "operational_manifests",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "??");

            migrationBuilder.AddColumn<string>(
                name: "RouteCode",
                table: "operational_manifests",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "UNASSIGNED");

            migrationBuilder.AddColumn<string>(
                name: "RouteName",
                table: "operational_manifests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "Route not assigned");

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceRouteId",
                table: "operational_manifests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "operational_manifest_shipment_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationalManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CargoItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShippingBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShipmentMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShipmentContainerId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrackingNumberSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MovementCodeSnapshot = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ContainerNumberSnapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LinkedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_manifest_shipment_links", x => x.Id);
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

            migrationBuilder.Sql("""
                UPDATE operational_manifests AS manifest
                SET "ServiceRouteId" = route."Id",
                    "RouteCode" = route."Code",
                    "RouteName" = route."Name",
                    "OriginCountryCode" = origin."CountryCode",
                    "DestinationCountryCode" = destination."CountryCode"
                FROM cargo_items AS cargo
                JOIN bookings AS booking ON booking."CargoItemId" = cargo."Id"
                JOIN service_routes AS route ON route."Id" = booking."ServiceRouteId"
                JOIN branches AS origin ON origin."Id" = route."OriginBranchId"
                JOIN branches AS destination ON destination."Id" = route."DestinationBranchId"
                WHERE manifest."CargoItemId" = cargo."Id"
                """);

            migrationBuilder.Sql("""
                INSERT INTO operational_manifest_shipment_links
                    ("Id", "OperationalManifestId", "CargoItemId", "ShippingBatchId",
                     "ShipmentMovementId", "ShipmentContainerId", "TrackingNumberSnapshot",
                     "MovementCodeSnapshot", "ContainerNumberSnapshot", "Notes", "LinkedAt",
                     "LinkedByUserId", "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy")
                SELECT gen_random_uuid(), manifest."Id", cargo."Id", cargo."BatchId",
                       cargo."ShipmentMovementId", cargo."ShipmentContainerId", cargo."TrackingNumber",
                       movement."MovementCode", container."ContainerNumber",
                       'Migrated from the original manifest cargo link.', manifest."CreatedAt",
                       manifest."CreatedByUserId", manifest."CreatedAt", manifest."CreatedBy", NULL, NULL
                FROM operational_manifests AS manifest
                JOIN cargo_items AS cargo ON cargo."Id" = manifest."CargoItemId"
                LEFT JOIN shipment_movements AS movement ON movement."Id" = cargo."ShipmentMovementId"
                LEFT JOIN shipment_containers AS container ON container."Id" = cargo."ShipmentContainerId"
                """);

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifests_OriginCountryCode_DestinationCountryC~",
                table: "operational_manifests",
                columns: new[] { "OriginCountryCode", "DestinationCountryCode", "LogisticsMethod", "ManifestDate" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifests_ServiceRouteId_ManifestDate",
                table: "operational_manifests",
                columns: new[] { "ServiceRouteId", "ManifestDate" });

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
                name: "FK_operational_manifests_service_routes_ServiceRouteId",
                table: "operational_manifests",
                column: "ServiceRouteId",
                principalTable: "service_routes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_operational_manifests_service_routes_ServiceRouteId",
                table: "operational_manifests");

            migrationBuilder.DropTable(
                name: "operational_manifest_shipment_links");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifests_OriginCountryCode_DestinationCountryC~",
                table: "operational_manifests");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifests_ServiceRouteId_ManifestDate",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "DestinationCountryCode",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "OriginCountryCode",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "RouteCode",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "RouteName",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "ServiceRouteId",
                table: "operational_manifests");
        }
    }
}
