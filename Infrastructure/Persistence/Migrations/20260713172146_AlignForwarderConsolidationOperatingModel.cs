using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignForwarderConsolidationOperatingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConsolidationType",
                table: "shipping_batches",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Groupage");

            migrationBuilder.AddColumn<string>(
                name: "DestinationGateway",
                table: "shipment_movements",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MasterTransportReference",
                table: "shipment_movements",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatorType",
                table: "shipment_movements",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "ThirdPartyCarrier");

            migrationBuilder.AddColumn<string>(
                name: "OriginGateway",
                table: "shipment_movements",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountType",
                table: "customers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Individual");

            migrationBuilder.Sql("""
                UPDATE customers
                SET "AccountType" = 'Business'
                WHERE "IsBusiness" = TRUE;

                UPDATE shipment_movements AS movement
                SET "MasterTransportReference" = COALESCE(
                        movement."MasterTransportReference", NULLIF(batch."AirwayBillNumber", '')),
                    "OriginGateway" = COALESCE(
                        movement."OriginGateway", NULLIF(batch."PortOfLoading", '')),
                    "DestinationGateway" = COALESCE(
                        movement."DestinationGateway", NULLIF(batch."PortOfDischarge", ''))
                FROM shipping_batches AS batch
                WHERE movement."ShippingBatchId" = batch."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_shipping_batches_ConsolidationType_Status_ScheduledReleaseD~",
                table: "shipping_batches",
                columns: new[] { "ConsolidationType", "Status", "ScheduledReleaseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_movements_OperatorType_ScheduledDepartureAt",
                table: "shipment_movements",
                columns: new[] { "OperatorType", "ScheduledDepartureAt" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_AccountType_CreatedAt",
                table: "customers",
                columns: new[] { "AccountType", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_shipping_batches_ConsolidationType_Status_ScheduledReleaseD~",
                table: "shipping_batches");

            migrationBuilder.DropIndex(
                name: "IX_shipment_movements_OperatorType_ScheduledDepartureAt",
                table: "shipment_movements");

            migrationBuilder.DropIndex(
                name: "IX_customers_AccountType_CreatedAt",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "ConsolidationType",
                table: "shipping_batches");

            migrationBuilder.DropColumn(
                name: "DestinationGateway",
                table: "shipment_movements");

            migrationBuilder.DropColumn(
                name: "MasterTransportReference",
                table: "shipment_movements");

            migrationBuilder.DropColumn(
                name: "OperatorType",
                table: "shipment_movements");

            migrationBuilder.DropColumn(
                name: "OriginGateway",
                table: "shipment_movements");

            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "customers");
        }
    }
}
