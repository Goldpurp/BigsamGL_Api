using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260713144300_AddPaginationAndPerformanceIndexes")]
public partial class AddPaginationAndPerformanceIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_wallet_ledgers_CustomerId", table: "wallet_ledgers");
        migrationBuilder.DropIndex(name: "IX_shipping_quotes_CustomerId", table: "shipping_quotes");
        migrationBuilder.DropIndex(name: "IX_audit_logs_CreatedAt", table: "audit_logs");

        migrationBuilder.CreateIndex(name: "IX_wallet_ledgers_CustomerId_Timestamp", table: "wallet_ledgers", columns: new[] { "CustomerId", "Timestamp" });
        migrationBuilder.CreateIndex(name: "IX_suppliers_Name", table: "suppliers", column: "Name");
        migrationBuilder.CreateIndex(name: "IX_staff_profiles_BranchCode_Status", table: "staff_profiles", columns: new[] { "BranchCode", "Status" });
        migrationBuilder.CreateIndex(name: "IX_staff_activity_logs_OccurredAt_Id", table: "staff_activity_logs", columns: new[] { "OccurredAt", "Id" });
        migrationBuilder.CreateIndex(name: "IX_shipping_quotes_CustomerId_CreatedAt", table: "shipping_quotes", columns: new[] { "CustomerId", "CreatedAt" });
        migrationBuilder.CreateIndex(name: "IX_shipping_batches_CreatedAt", table: "shipping_batches", column: "CreatedAt");
        migrationBuilder.CreateIndex(name: "IX_shipment_containers_IsActive_LastEventAt", table: "shipment_containers", columns: new[] { "IsActive", "LastEventAt" });
        migrationBuilder.CreateIndex(name: "IX_sales_leads_Stage_CreatedAt", table: "sales_leads", columns: new[] { "Stage", "CreatedAt" });
        migrationBuilder.CreateIndex(name: "IX_leave_requests_Status_StartDate", table: "leave_requests", columns: new[] { "Status", "StartDate" });
        migrationBuilder.CreateIndex(name: "IX_journal_entries_Status_EntryDate", table: "journal_entries", columns: new[] { "Status", "EntryDate" });
        migrationBuilder.CreateIndex(name: "IX_invoices_Currency_IssueDate", table: "invoices", columns: new[] { "Currency", "IssueDate" });
        migrationBuilder.CreateIndex(name: "IX_invoices_Status_IssueDate", table: "invoices", columns: new[] { "Status", "IssueDate" });
        migrationBuilder.CreateIndex(name: "IX_guests_Status_CreatedAt", table: "guests", columns: new[] { "Status", "CreatedAt" });
        migrationBuilder.CreateIndex(name: "IX_customers_CreatedAt", table: "customers", column: "CreatedAt");
        migrationBuilder.CreateIndex(name: "IX_cargo_items_CreatedAt", table: "cargo_items", column: "CreatedAt");
        migrationBuilder.CreateIndex(name: "IX_cargo_items_Status_CreatedAt", table: "cargo_items", columns: new[] { "Status", "CreatedAt" });
        migrationBuilder.CreateIndex(name: "IX_bookings_CustomerId_CreatedAt", table: "bookings", columns: new[] { "CustomerId", "CreatedAt" });
        migrationBuilder.CreateIndex(name: "IX_audit_logs_ActorId_CreatedAt", table: "audit_logs", columns: new[] { "ActorId", "CreatedAt" });
        migrationBuilder.CreateIndex(name: "IX_audit_logs_CreatedAt_Id", table: "audit_logs", columns: new[] { "CreatedAt", "Id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_wallet_ledgers_CustomerId_Timestamp", table: "wallet_ledgers");
        migrationBuilder.DropIndex(name: "IX_suppliers_Name", table: "suppliers");
        migrationBuilder.DropIndex(name: "IX_staff_profiles_BranchCode_Status", table: "staff_profiles");
        migrationBuilder.DropIndex(name: "IX_staff_activity_logs_OccurredAt_Id", table: "staff_activity_logs");
        migrationBuilder.DropIndex(name: "IX_shipping_quotes_CustomerId_CreatedAt", table: "shipping_quotes");
        migrationBuilder.DropIndex(name: "IX_shipping_batches_CreatedAt", table: "shipping_batches");
        migrationBuilder.DropIndex(name: "IX_shipment_containers_IsActive_LastEventAt", table: "shipment_containers");
        migrationBuilder.DropIndex(name: "IX_sales_leads_Stage_CreatedAt", table: "sales_leads");
        migrationBuilder.DropIndex(name: "IX_leave_requests_Status_StartDate", table: "leave_requests");
        migrationBuilder.DropIndex(name: "IX_journal_entries_Status_EntryDate", table: "journal_entries");
        migrationBuilder.DropIndex(name: "IX_invoices_Currency_IssueDate", table: "invoices");
        migrationBuilder.DropIndex(name: "IX_invoices_Status_IssueDate", table: "invoices");
        migrationBuilder.DropIndex(name: "IX_guests_Status_CreatedAt", table: "guests");
        migrationBuilder.DropIndex(name: "IX_customers_CreatedAt", table: "customers");
        migrationBuilder.DropIndex(name: "IX_cargo_items_CreatedAt", table: "cargo_items");
        migrationBuilder.DropIndex(name: "IX_cargo_items_Status_CreatedAt", table: "cargo_items");
        migrationBuilder.DropIndex(name: "IX_bookings_CustomerId_CreatedAt", table: "bookings");
        migrationBuilder.DropIndex(name: "IX_audit_logs_ActorId_CreatedAt", table: "audit_logs");
        migrationBuilder.DropIndex(name: "IX_audit_logs_CreatedAt_Id", table: "audit_logs");

        migrationBuilder.CreateIndex(name: "IX_wallet_ledgers_CustomerId", table: "wallet_ledgers", column: "CustomerId");
        migrationBuilder.CreateIndex(name: "IX_shipping_quotes_CustomerId", table: "shipping_quotes", column: "CustomerId");
        migrationBuilder.CreateIndex(name: "IX_audit_logs_CreatedAt", table: "audit_logs", column: "CreatedAt");
    }
}
