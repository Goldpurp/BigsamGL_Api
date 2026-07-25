using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedServiceToCustomerShipments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "HeightCm",
                table: "customer_shipments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LengthCm",
                table: "customer_shipments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceCodeSnapshot",
                table: "customer_shipments",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WidthCm",
                table: "customer_shipments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_shipments_BranchId_ServiceCodeSnapshot_CreatedAt",
                table: "customer_shipments",
                columns: new[] { "BranchId", "ServiceCodeSnapshot", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_customer_shipments_BranchId_ServiceCodeSnapshot_CreatedAt",
                table: "customer_shipments");

            migrationBuilder.DropColumn(
                name: "HeightCm",
                table: "customer_shipments");

            migrationBuilder.DropColumn(
                name: "LengthCm",
                table: "customer_shipments");

            migrationBuilder.DropColumn(
                name: "ServiceCodeSnapshot",
                table: "customer_shipments");

            migrationBuilder.DropColumn(
                name: "WidthCm",
                table: "customer_shipments");
        }
    }
}
