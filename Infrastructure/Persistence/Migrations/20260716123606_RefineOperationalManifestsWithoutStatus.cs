using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefineOperationalManifestsWithoutStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_operational_manifests_user_accounts_CancelledByUserId",
                table: "operational_manifests");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifests_CancelledByUserId",
                table: "operational_manifests");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifests_Status_IssuedAt",
                table: "operational_manifests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_manifests_carry_weight",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "operational_manifests");

            migrationBuilder.RenameColumn(
                name: "ItemDescription",
                table: "operational_manifests",
                newName: "Contents");

            migrationBuilder.RenameColumn(
                name: "IssuedAt",
                table: "operational_manifests",
                newName: "ManifestDate");

            migrationBuilder.RenameColumn(
                name: "CarryWeightKg",
                table: "operational_manifests",
                newName: "CostWeightKg");

            migrationBuilder.RenameIndex(
                name: "IX_operational_manifests_PaymentType_IssuedAt",
                table: "operational_manifests",
                newName: "IX_operational_manifests_PaymentType_ManifestDate");

            migrationBuilder.RenameIndex(
                name: "IX_operational_manifests_InvoiceNumber_IssuedAt",
                table: "operational_manifests",
                newName: "IX_operational_manifests_InvoiceNumber_ManifestDate");

            migrationBuilder.RenameIndex(
                name: "IX_operational_manifests_CustomerId_IssuedAt",
                table: "operational_manifests",
                newName: "IX_operational_manifests_CustomerId_ManifestDate");

            migrationBuilder.Sql("UPDATE operational_manifests SET \"ManifestDate\" = date_trunc('day', \"ManifestDate\")");

            migrationBuilder.Sql("UPDATE operational_manifests SET \"CostWeightKg\" = \"ActualWeightKg\" WHERE \"CostWeightKg\" < \"ActualWeightKg\"");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryCity",
                table: "operational_manifests",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LogisticsMethod",
                table: "operational_manifests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Air");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifests_LogisticsMethod_ManifestDate_Currency",
                table: "operational_manifests",
                columns: new[] { "LogisticsMethod", "ManifestDate", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifests_ManifestDate_LogisticsMethod",
                table: "operational_manifests",
                columns: new[] { "ManifestDate", "LogisticsMethod" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_manifests_cost_weight",
                table: "operational_manifests",
                sql: "\"CostWeightKg\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_manifests_cost_weight_rule",
                table: "operational_manifests",
                sql: "\"CostWeightKg\" >= \"ActualWeightKg\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_manifests_logistics_method",
                table: "operational_manifests",
                sql: "\"LogisticsMethod\" IN ('Air','Ship')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_operational_manifests_LogisticsMethod_ManifestDate_Currency",
                table: "operational_manifests");

            migrationBuilder.DropIndex(
                name: "IX_operational_manifests_ManifestDate_LogisticsMethod",
                table: "operational_manifests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_manifests_cost_weight",
                table: "operational_manifests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_manifests_cost_weight_rule",
                table: "operational_manifests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_manifests_logistics_method",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "DeliveryCity",
                table: "operational_manifests");

            migrationBuilder.DropColumn(
                name: "LogisticsMethod",
                table: "operational_manifests");

            migrationBuilder.RenameColumn(
                name: "ManifestDate",
                table: "operational_manifests",
                newName: "IssuedAt");

            migrationBuilder.RenameColumn(
                name: "CostWeightKg",
                table: "operational_manifests",
                newName: "CarryWeightKg");

            migrationBuilder.RenameColumn(
                name: "Contents",
                table: "operational_manifests",
                newName: "ItemDescription");

            migrationBuilder.RenameIndex(
                name: "IX_operational_manifests_PaymentType_ManifestDate",
                table: "operational_manifests",
                newName: "IX_operational_manifests_PaymentType_IssuedAt");

            migrationBuilder.RenameIndex(
                name: "IX_operational_manifests_InvoiceNumber_ManifestDate",
                table: "operational_manifests",
                newName: "IX_operational_manifests_InvoiceNumber_IssuedAt");

            migrationBuilder.RenameIndex(
                name: "IX_operational_manifests_CustomerId_ManifestDate",
                table: "operational_manifests",
                newName: "IX_operational_manifests_CustomerId_IssuedAt");

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "operational_manifests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "operational_manifests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledByUserId",
                table: "operational_manifests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "operational_manifests",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Issued");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifests_CancelledByUserId",
                table: "operational_manifests",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_manifests_Status_IssuedAt",
                table: "operational_manifests",
                columns: new[] { "Status", "IssuedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_manifests_carry_weight",
                table: "operational_manifests",
                sql: "\"CarryWeightKg\" > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_operational_manifests_user_accounts_CancelledByUserId",
                table: "operational_manifests",
                column: "CancelledByUserId",
                principalTable: "user_accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
