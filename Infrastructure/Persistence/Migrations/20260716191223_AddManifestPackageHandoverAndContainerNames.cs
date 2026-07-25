using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManifestPackageHandoverAndContainerNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "shipment_containers",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                table: "operational_manifests",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ActualWeightKg",
                table: "operational_manifest_shipment_links",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostWeightKg",
                table: "operational_manifest_shipment_links",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "HandoverAddress",
                table: "operational_manifest_shipment_links",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandoverCity",
                table: "operational_manifest_shipment_links",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandoverMethod",
                table: "operational_manifest_shipment_links",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PackageDescription",
                table: "operational_manifest_shipment_links",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "operational_manifest_shipment_links",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                table: "operational_manifest_shipment_links",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE operational_manifests
                SET "SenderName" = "ClientName"
                WHERE length(btrim("SenderName")) = 0
                """);

            migrationBuilder.Sql("""
                UPDATE operational_manifest_shipment_links AS link
                SET "SenderName" = manifest."SenderName",
                    "PackageDescription" = cargo."Description",
                    "Quantity" = GREATEST(manifest."Quantity", 1),
                    "ActualWeightKg" = GREATEST(cargo."ActualWeightKg", 0.001),
                    "CostWeightKg" = GREATEST(cargo."ActualWeightKg", cargo."VolumetricWeightKg", 0.001),
                    "HandoverMethod" = CASE
                        WHEN manifest."DeliveryAddress" ILIKE 'Hub pickup:%' THEN 'HubCollection'
                        ELSE 'SentOut'
                    END,
                    "HandoverAddress" = manifest."DeliveryAddress",
                    "HandoverCity" = manifest."DeliveryCity"
                FROM operational_manifests AS manifest, cargo_items AS cargo
                WHERE link."OperationalManifestId" = manifest."Id"
                  AND link."CargoItemId" = cargo."Id"
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_manifest_package_actual_weight",
                table: "operational_manifest_shipment_links",
                sql: "\"ActualWeightKg\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_manifest_package_cost_weight",
                table: "operational_manifest_shipment_links",
                sql: "\"CostWeightKg\" >= \"ActualWeightKg\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_manifest_package_description",
                table: "operational_manifest_shipment_links",
                sql: "length(btrim(\"PackageDescription\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_manifest_package_handover_method",
                table: "operational_manifest_shipment_links",
                sql: "\"HandoverMethod\" IN ('HubCollection','SentOut')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_manifest_package_quantity",
                table: "operational_manifest_shipment_links",
                sql: "\"Quantity\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_manifest_package_sender",
                table: "operational_manifest_shipment_links",
                sql: "length(btrim(\"SenderName\")) > 0");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_shipment_links_OperationalManifestId_H~",
                table: "operational_manifest_shipment_links",
                columns: new[] { "OperationalManifestId", "HandoverMethod", "LinkedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_manifest_package_actual_weight",
                table: "operational_manifest_shipment_links");

            migrationBuilder.DropCheckConstraint(
                name: "CK_manifest_package_cost_weight",
                table: "operational_manifest_shipment_links");

            migrationBuilder.DropCheckConstraint(
                name: "CK_manifest_package_description",
                table: "operational_manifest_shipment_links");

            migrationBuilder.DropCheckConstraint(
                name: "CK_manifest_package_handover_method",
                table: "operational_manifest_shipment_links");

            migrationBuilder.DropCheckConstraint(
                name: "CK_manifest_package_quantity",
                table: "operational_manifest_shipment_links");

            migrationBuilder.DropCheckConstraint(
                name: "CK_manifest_package_sender",
                table: "operational_manifest_shipment_links");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifest_shipment_links_OperationalManifestId_H~",
                table: "operational_manifest_shipment_links");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "shipment_containers");

            migrationBuilder.DropColumn(
                name: "SenderName",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "ActualWeightKg",
                table: "operational_manifest_shipment_links");

            migrationBuilder.DropColumn(
                name: "CostWeightKg",
                table: "operational_manifest_shipment_links");

            migrationBuilder.DropColumn(
                name: "HandoverAddress",
                table: "operational_manifest_shipment_links");

            migrationBuilder.DropColumn(
                name: "HandoverCity",
                table: "operational_manifest_shipment_links");

            migrationBuilder.DropColumn(
                name: "HandoverMethod",
                table: "operational_manifest_shipment_links");

            migrationBuilder.DropColumn(
                name: "PackageDescription",
                table: "operational_manifest_shipment_links");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "operational_manifest_shipment_links");

            migrationBuilder.DropColumn(
                name: "SenderName",
                table: "operational_manifest_shipment_links");
        }
    }
}
