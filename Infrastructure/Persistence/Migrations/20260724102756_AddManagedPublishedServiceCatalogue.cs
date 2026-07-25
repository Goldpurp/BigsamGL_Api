using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedPublishedServiceCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_shipping_price_values",
                table: "shipping_price_catalogue");

            migrationBuilder.AddColumn<string>(
                name: "DestinationCountryCodes",
                table: "shipping_price_catalogue",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DestinationZone",
                table: "shipping_price_catalogue",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "shipping_price_catalogue",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DocumentationFee",
                table: "shipping_price_catalogue",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "shipping_price_catalogue",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MaximumQuantity",
                table: "shipping_price_catalogue",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumQuantity",
                table: "shipping_price_catalogue",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceCode",
                table: "shipping_price_catalogue",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_shipping_price_catalogue_BranchId_ServiceCode_DisplayOrder",
                table: "shipping_price_catalogue",
                columns: new[] { "BranchId", "ServiceCode", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_shipping_price_catalogue_IsPublic_State_Method",
                table: "shipping_price_catalogue",
                columns: new[] { "IsPublic", "State", "Method" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_shipping_price_values",
                table: "shipping_price_catalogue",
                sql: "(\"LowerPrice\" IS NULL OR \"LowerPrice\" >= 0) AND (\"UpperPrice\" IS NULL OR \"UpperPrice\" >= \"LowerPrice\") AND (\"HandlingFee\" IS NULL OR \"HandlingFee\" >= 0) AND (\"DocumentationFee\" IS NULL OR \"DocumentationFee\" >= 0) AND (\"MinimumCharge\" IS NULL OR \"MinimumCharge\" >= 0) AND (\"MinimumQuantity\" IS NULL OR \"MinimumQuantity\" >= 0) AND (\"MaximumQuantity\" IS NULL OR \"MaximumQuantity\" >= \"MinimumQuantity\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_shipping_price_catalogue_BranchId_ServiceCode_DisplayOrder",
                table: "shipping_price_catalogue");

            migrationBuilder.DropIndex(
                name: "IX_shipping_price_catalogue_IsPublic_State_Method",
                table: "shipping_price_catalogue");

            migrationBuilder.DropCheckConstraint(
                name: "CK_shipping_price_values",
                table: "shipping_price_catalogue");

            migrationBuilder.DropColumn(
                name: "DestinationCountryCodes",
                table: "shipping_price_catalogue");

            migrationBuilder.DropColumn(
                name: "DestinationZone",
                table: "shipping_price_catalogue");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "shipping_price_catalogue");

            migrationBuilder.DropColumn(
                name: "DocumentationFee",
                table: "shipping_price_catalogue");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "shipping_price_catalogue");

            migrationBuilder.DropColumn(
                name: "MaximumQuantity",
                table: "shipping_price_catalogue");

            migrationBuilder.DropColumn(
                name: "MinimumQuantity",
                table: "shipping_price_catalogue");

            migrationBuilder.DropColumn(
                name: "ServiceCode",
                table: "shipping_price_catalogue");

            migrationBuilder.AddCheckConstraint(
                name: "CK_shipping_price_values",
                table: "shipping_price_catalogue",
                sql: "(\"LowerPrice\" IS NULL OR \"LowerPrice\" >= 0) AND (\"UpperPrice\" IS NULL OR \"UpperPrice\" >= \"LowerPrice\") AND (\"HandlingFee\" IS NULL OR \"HandlingFee\" >= 0) AND (\"MinimumCharge\" IS NULL OR \"MinimumCharge\" >= 0)");
        }
    }
}
