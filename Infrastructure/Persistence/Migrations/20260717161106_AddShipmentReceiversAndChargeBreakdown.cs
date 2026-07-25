using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentReceiversAndChargeBreakdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChargeBasis",
                table: "operational_manifest_entries",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NegotiationReference",
                table: "operational_manifest_entries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiverEmail",
                table: "operational_manifest_entries",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReceiverName",
                table: "operational_manifest_entries",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReceiverPhone",
                table: "operational_manifest_entries",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "operational_manifest_charge_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ManifestEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    BoxSize = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LengthCm = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    WidthCm = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    HeightCm = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    ActualWeightKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    VolumetricDivisor = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    VolumetricWeightKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    BillableWeightKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    LineAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_manifest_charge_lines", x => x.Id);
                    table.CheckConstraint("CK_manifest_charge_line_amount", "\"LineAmount\" > 0");
                    table.CheckConstraint("CK_manifest_charge_line_description", "length(btrim(\"Description\")) > 0");
                    table.CheckConstraint("CK_manifest_charge_line_dimensions", "(\"LengthCm\" IS NULL OR \"LengthCm\" > 0) AND (\"WidthCm\" IS NULL OR \"WidthCm\" > 0) AND (\"HeightCm\" IS NULL OR \"HeightCm\" > 0)");
                    table.CheckConstraint("CK_manifest_charge_line_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_manifest_charge_line_sort", "\"SortOrder\" >= 0");
                    table.CheckConstraint("CK_manifest_charge_line_type", "\"Type\" IN ('AirWeight','SeaBox','SeaItem','Negotiated')");
                    table.CheckConstraint("CK_manifest_charge_line_unit_price", "\"UnitPrice\" > 0");
                    table.CheckConstraint("CK_manifest_charge_line_weights", "(\"ActualWeightKg\" IS NULL OR \"ActualWeightKg\" > 0) AND (\"VolumetricDivisor\" IS NULL OR \"VolumetricDivisor\" > 0) AND (\"VolumetricWeightKg\" IS NULL OR \"VolumetricWeightKg\" > 0) AND (\"BillableWeightKg\" IS NULL OR \"BillableWeightKg\" > 0)");
                    table.ForeignKey(
                        name: "FK_operational_manifest_charge_lines_operational_manifest_entr~",
                        column: x => x.ManifestEntryId,
                        principalTable: "operational_manifest_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Preserve every existing shipment without pretending that historical dimension or
            // box-size evidence exists. Legacy entries become clearly labelled negotiated rows,
            // and their recorded customer identity is used as the receiver fallback.
            migrationBuilder.Sql("""
                UPDATE operational_manifest_entries
                SET "ReceiverName" = "CustomerNameSnapshot",
                    "ReceiverPhone" = "CustomerPhoneSnapshot",
                    "ReceiverEmail" = "CustomerEmailSnapshot",
                    "ChargeBasis" = 'Negotiated',
                    "NegotiationReference" = 'LEGACY-MIGRATION';

                INSERT INTO operational_manifest_charge_lines
                    ("Id", "ManifestEntryId", "Type", "Description", "Quantity", "UnitPrice",
                     "LineAmount", "SortOrder", "CreatedAt", "CreatedBy")
                SELECT gen_random_uuid(), entry."Id", 'Negotiated',
                       'Legacy shipment charge — original pricing breakdown was not captured',
                       1, entry."Amount", entry."Amount", 0,
                       COALESCE(entry."AddedAt", entry."CreatedAt"), 'migration'
                FROM operational_manifest_entries entry
                WHERE entry."Amount" > 0;

                ALTER TABLE operational_manifest_entries
                    ALTER COLUMN "ReceiverName" DROP DEFAULT,
                    ALTER COLUMN "ReceiverPhone" DROP DEFAULT,
                    ALTER COLUMN "ReceiverEmail" DROP DEFAULT,
                    ALTER COLUMN "ChargeBasis" DROP DEFAULT;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_manifest_entry_charge_basis",
                table: "operational_manifest_entries",
                sql: "\"ChargeBasis\" IN ('AirWeightAndDimensions','SeaBoxesAndItems','Negotiated')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_manifest_entry_receiver_email",
                table: "operational_manifest_entries",
                sql: "length(btrim(\"ReceiverEmail\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_manifest_entry_receiver_name",
                table: "operational_manifest_entries",
                sql: "length(btrim(\"ReceiverName\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_manifest_entry_receiver_phone",
                table: "operational_manifest_entries",
                sql: "length(btrim(\"ReceiverPhone\")) > 0");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifest_charge_lines_ManifestEntryId_SortOrder",
                table: "operational_manifest_charge_lines",
                columns: new[] { "ManifestEntryId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operational_manifest_charge_lines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_manifest_entry_charge_basis",
                table: "operational_manifest_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_manifest_entry_receiver_email",
                table: "operational_manifest_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_manifest_entry_receiver_name",
                table: "operational_manifest_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_manifest_entry_receiver_phone",
                table: "operational_manifest_entries");

            migrationBuilder.DropColumn(
                name: "ChargeBasis",
                table: "operational_manifest_entries");

            migrationBuilder.DropColumn(
                name: "NegotiationReference",
                table: "operational_manifest_entries");

            migrationBuilder.DropColumn(
                name: "ReceiverEmail",
                table: "operational_manifest_entries");

            migrationBuilder.DropColumn(
                name: "ReceiverName",
                table: "operational_manifest_entries");

            migrationBuilder.DropColumn(
                name: "ReceiverPhone",
                table: "operational_manifest_entries");
        }
    }
}
