using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

public partial class AddSimpleOperationalManifests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateSequence(name: "operational_manifest_number_seq", minValue: 1L);

        migrationBuilder.CreateTable(
            name: "operational_manifests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ManifestNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                CargoItemId = table.Column<Guid>(type: "uuid", nullable: true),
                ShippingBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                ClientName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                ClientPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                ItemDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                ActualWeightKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                CarryWeightKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                PaymentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                DeliveryAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Issued"),
                IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                LastEditedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<string>(type: "text", nullable: true),
                LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifiedBy = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_operational_manifests", x => x.Id);
                table.CheckConstraint("CK_operational_manifests_actual_weight", "\"ActualWeightKg\" > 0");
                table.CheckConstraint("CK_operational_manifests_amount", "\"Amount\" >= 0");
                table.CheckConstraint("CK_operational_manifests_carry_weight", "\"CarryWeightKg\" > 0");
                table.CheckConstraint("CK_operational_manifests_quantity", "\"Quantity\" > 0");
                table.ForeignKey("FK_operational_manifests_cargo_items_CargoItemId", x => x.CargoItemId,
                    "cargo_items", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_operational_manifests_customers_CustomerId", x => x.CustomerId,
                    "customers", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_operational_manifests_invoices_InvoiceId", x => x.InvoiceId,
                    "invoices", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_operational_manifests_shipping_batches_ShippingBatchId", x => x.ShippingBatchId,
                    "shipping_batches", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_operational_manifests_user_accounts_CancelledByUserId", x => x.CancelledByUserId,
                    "user_accounts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_operational_manifests_user_accounts_CreatedByUserId", x => x.CreatedByUserId,
                    "user_accounts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_operational_manifests_user_accounts_LastEditedByUserId", x => x.LastEditedByUserId,
                    "user_accounts", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_operational_manifests_CancelledByUserId", "operational_manifests", "CancelledByUserId");
        migrationBuilder.CreateIndex("IX_operational_manifests_CargoItemId", "operational_manifests", "CargoItemId");
        migrationBuilder.CreateIndex("IX_operational_manifests_CreatedByUserId", "operational_manifests", "CreatedByUserId");
        migrationBuilder.CreateIndex("IX_operational_manifests_CustomerId_IssuedAt", "operational_manifests", new[] { "CustomerId", "IssuedAt" });
        migrationBuilder.CreateIndex("IX_operational_manifests_InvoiceId", "operational_manifests", "InvoiceId");
        migrationBuilder.CreateIndex("IX_operational_manifests_InvoiceNumber_IssuedAt", "operational_manifests", new[] { "InvoiceNumber", "IssuedAt" });
        migrationBuilder.CreateIndex("IX_operational_manifests_LastEditedByUserId", "operational_manifests", "LastEditedByUserId");
        migrationBuilder.CreateIndex("IX_operational_manifests_ManifestNumber", "operational_manifests", "ManifestNumber", unique: true);
        migrationBuilder.CreateIndex("IX_operational_manifests_PaymentType_IssuedAt", "operational_manifests", new[] { "PaymentType", "IssuedAt" });
        migrationBuilder.CreateIndex("IX_operational_manifests_ShippingBatchId", "operational_manifests", "ShippingBatchId");
        migrationBuilder.CreateIndex("IX_operational_manifests_Status_IssuedAt", "operational_manifests", new[] { "Status", "IssuedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "operational_manifests");
        migrationBuilder.DropSequence(name: "operational_manifest_number_seq");
    }
}
