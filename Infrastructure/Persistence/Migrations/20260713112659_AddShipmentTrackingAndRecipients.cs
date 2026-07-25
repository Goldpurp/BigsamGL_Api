using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentTrackingAndRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CarrierProvider",
                table: "shipment_milestones",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomerNotifiedAt",
                table: "shipment_milestones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EstimatedDeliveryAt",
                table: "shipment_milestones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "shipment_milestones",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "General");

            migrationBuilder.AddColumn<string>(
                name: "ExternalEventId",
                table: "shipment_milestones",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCorrected",
                table: "shipment_milestones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsException",
                table: "shipment_milestones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IssueCode",
                table: "shipment_milestones",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevisionReason",
                table: "shipment_milestones",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarrierProvider",
                table: "cargo_items",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalTrackingNumber",
                table: "cargo_items",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCarrierSyncAt",
                table: "cargo_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DeliveryAddressId",
                table: "bookings",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryMethod",
                table: "bookings",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "AddressDelivery");

            migrationBuilder.CreateTable(
                name: "shipment_recipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DeliveryMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PickupBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    AddressLine1 = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    AddressLine2 = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shipment_recipients_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shipment_recipients_branches_PickupBranchId",
                        column: x => x.PickupBranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipment_recipients_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_milestones_CarrierProvider_ExternalEventId",
                table: "shipment_milestones",
                columns: new[] { "CarrierProvider", "ExternalEventId" },
                unique: true,
                filter: "\"ExternalEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_items_CarrierProvider_ExternalTrackingNumber",
                table: "cargo_items",
                columns: new[] { "CarrierProvider", "ExternalTrackingNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_recipients_BookingId",
                table: "shipment_recipients",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipment_recipients_CustomerId",
                table: "shipment_recipients",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_recipients_PickupBranchId",
                table: "shipment_recipients",
                column: "PickupBranchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shipment_recipients");

            migrationBuilder.DropIndex(
                name: "IX_shipment_milestones_CarrierProvider_ExternalEventId",
                table: "shipment_milestones");

            migrationBuilder.DropIndex(
                name: "IX_cargo_items_CarrierProvider_ExternalTrackingNumber",
                table: "cargo_items");

            migrationBuilder.DropColumn(
                name: "CarrierProvider",
                table: "shipment_milestones");

            migrationBuilder.DropColumn(
                name: "CustomerNotifiedAt",
                table: "shipment_milestones");

            migrationBuilder.DropColumn(
                name: "EstimatedDeliveryAt",
                table: "shipment_milestones");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "shipment_milestones");

            migrationBuilder.DropColumn(
                name: "ExternalEventId",
                table: "shipment_milestones");

            migrationBuilder.DropColumn(
                name: "IsCorrected",
                table: "shipment_milestones");

            migrationBuilder.DropColumn(
                name: "IsException",
                table: "shipment_milestones");

            migrationBuilder.DropColumn(
                name: "IssueCode",
                table: "shipment_milestones");

            migrationBuilder.DropColumn(
                name: "RevisionReason",
                table: "shipment_milestones");

            migrationBuilder.DropColumn(
                name: "CarrierProvider",
                table: "cargo_items");

            migrationBuilder.DropColumn(
                name: "ExternalTrackingNumber",
                table: "cargo_items");

            migrationBuilder.DropColumn(
                name: "LastCarrierSyncAt",
                table: "cargo_items");

            migrationBuilder.DropColumn(
                name: "DeliveryMethod",
                table: "bookings");

            migrationBuilder.AlterColumn<Guid>(
                name: "DeliveryAddressId",
                table: "bookings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
