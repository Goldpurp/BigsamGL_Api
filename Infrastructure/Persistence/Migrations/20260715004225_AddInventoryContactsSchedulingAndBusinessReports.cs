using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryContactsSchedulingAndBusinessReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "appointment_number_seq",
                minValue: 1L);

            migrationBuilder.CreateSequence(
                name: "inventory_transaction_number_seq",
                minValue: 1L);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "sales_leads",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastContactedAt",
                table: "sales_leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LostReason",
                table: "sales_leads",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "sales_leads",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<string>(
                name: "ReceivingStatus",
                table: "company_purchases",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NotRequired");

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryItemId",
                table: "company_purchase_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityReceived",
                table: "company_purchase_lines",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedFullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    JobTitle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NormalizedPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    SalesLeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreferredBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    LastContactedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextFollowUpAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contacts_branches_PreferredBranchId",
                        column: x => x.PreferredBranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contacts_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contacts_sales_leads_SalesLeadId",
                        column: x => x.SalesLeadId,
                        principalTable: "sales_leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contacts_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contacts_user_accounts_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "inventory_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DefaultUnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PreferredSupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsLotTracked = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_items", x => x.Id);
                    table.CheckConstraint("CK_inventory_items_default_cost", "\"DefaultUnitCost\" >= 0");
                    table.ForeignKey(
                        name: "FK_inventory_items_suppliers_PreferredSupplierId",
                        column: x => x.PreferredSupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "inventory_locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AddressOrArea = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_locations_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OrganizerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    SalesLeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    MeetingUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReminderAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReminderSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsPrivate = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointments", x => x.Id);
                    table.CheckConstraint("CK_appointments_time", "\"EndsAt\" > \"StartsAt\"");
                    table.ForeignKey(
                        name: "FK_appointments_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_appointments_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_appointments_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_appointments_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_appointments_sales_leads_SalesLeadId",
                        column: x => x.SalesLeadId,
                        principalTable: "sales_leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_appointments_user_accounts_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_appointments_user_accounts_OrganizerUserId",
                        column: x => x.OrganizerUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contact_interactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextActionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesLeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_interactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contact_interactions_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contact_interactions_sales_leads_SalesLeadId",
                        column: x => x.SalesLeadId,
                        principalTable: "sales_leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contact_interactions_user_accounts_StaffUserId",
                        column: x => x.StaffUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_stock_levels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    QuantityReserved = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    AverageUnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReorderLevel = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    TargetStockLevel = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    LastMovementAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_stock_levels", x => x.Id);
                    table.CheckConstraint("CK_inventory_stock_cost", "\"AverageUnitCost\" >= 0");
                    table.CheckConstraint("CK_inventory_stock_on_hand", "\"QuantityOnHand\" >= 0");
                    table.CheckConstraint("CK_inventory_stock_reserved", "\"QuantityReserved\" >= 0 AND \"QuantityReserved\" <= \"QuantityOnHand\"");
                    table.CheckConstraint("CK_inventory_stock_thresholds", "\"ReorderLevel\" >= 0 AND \"TargetStockLevel\" >= 0");
                    table.ForeignKey(
                        name: "FK_inventory_stock_levels_inventory_items_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "inventory_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_stock_levels_inventory_locations_InventoryLocatio~",
                        column: x => x.InventoryLocationId,
                        principalTable: "inventory_locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FromLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyPurchaseLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_transactions", x => x.Id);
                    table.CheckConstraint("CK_inventory_transactions_cost", "\"UnitCost\" >= 0 AND \"TotalCost\" >= 0");
                    table.CheckConstraint("CK_inventory_transactions_location", "\"FromLocationId\" IS NOT NULL OR \"ToLocationId\" IS NOT NULL");
                    table.CheckConstraint("CK_inventory_transactions_quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_inventory_transactions_company_purchase_lines_CompanyPurcha~",
                        column: x => x.CompanyPurchaseLineId,
                        principalTable: "company_purchase_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_inventory_items_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "inventory_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_inventory_locations_FromLocationId",
                        column: x => x.FromLocationId,
                        principalTable: "inventory_locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_inventory_locations_ToLocationId",
                        column: x => x.ToLocationId,
                        principalTable: "inventory_locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_user_accounts_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "appointment_attendees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ResponseStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment_attendees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_appointment_attendees_appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_appointment_attendees_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_appointment_attendees_user_accounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_leads_IsArchived_Priority_NextActionAt",
                table: "sales_leads",
                columns: new[] { "IsArchived", "Priority", "NextActionAt" });

            migrationBuilder.CreateIndex(
                name: "IX_company_purchases_ReceivingStatus_PurchaseDate",
                table: "company_purchases",
                columns: new[] { "ReceivingStatus", "PurchaseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_company_purchase_lines_InventoryItemId",
                table: "company_purchase_lines",
                column: "InventoryItemId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_company_purchase_lines_received",
                table: "company_purchase_lines",
                sql: "\"QuantityReceived\" >= 0 AND \"QuantityReceived\" <= \"Quantity\"");

            migrationBuilder.CreateIndex(
                name: "IX_appointment_attendees_AppointmentId_ContactId",
                table: "appointment_attendees",
                columns: new[] { "AppointmentId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_appointment_attendees_AppointmentId_UserAccountId",
                table: "appointment_attendees",
                columns: new[] { "AppointmentId", "UserAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_appointment_attendees_ContactId",
                table: "appointment_attendees",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_appointment_attendees_UserAccountId",
                table: "appointment_attendees",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_AppointmentNumber",
                table: "appointments",
                column: "AppointmentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_appointments_AssignedUserId_StartsAt_EndsAt",
                table: "appointments",
                columns: new[] { "AssignedUserId", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_BookingId",
                table: "appointments",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_BranchId_StartsAt",
                table: "appointments",
                columns: new[] { "BranchId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_ContactId",
                table: "appointments",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_CustomerId",
                table: "appointments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_OrganizerUserId_StartsAt_EndsAt",
                table: "appointments",
                columns: new[] { "OrganizerUserId", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_ReminderAt_ReminderSentAt",
                table: "appointments",
                columns: new[] { "ReminderAt", "ReminderSentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_SalesLeadId",
                table: "appointments",
                column: "SalesLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_StartsAt_Status",
                table: "appointments",
                columns: new[] { "StartsAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_contact_interactions_ContactId_OccurredAt",
                table: "contact_interactions",
                columns: new[] { "ContactId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_contact_interactions_SalesLeadId",
                table: "contact_interactions",
                column: "SalesLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_interactions_StaffUserId_NextActionAt",
                table: "contact_interactions",
                columns: new[] { "StaffUserId", "NextActionAt" });

            migrationBuilder.CreateIndex(
                name: "IX_contacts_CustomerId",
                table: "contacts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_NormalizedEmail",
                table: "contacts",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_NormalizedFullName",
                table: "contacts",
                column: "NormalizedFullName");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_NormalizedPhone",
                table: "contacts",
                column: "NormalizedPhone");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_OwnerUserId_Status_NextFollowUpAt",
                table: "contacts",
                columns: new[] { "OwnerUserId", "Status", "NextFollowUpAt" });

            migrationBuilder.CreateIndex(
                name: "IX_contacts_PreferredBranchId",
                table: "contacts",
                column: "PreferredBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_SalesLeadId",
                table: "contacts",
                column: "SalesLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_SupplierId",
                table: "contacts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_Type_Status_NextFollowUpAt",
                table: "contacts",
                columns: new[] { "Type", "Status", "NextFollowUpAt" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_Barcode",
                table: "inventory_items",
                column: "Barcode",
                unique: true,
                filter: "\"Barcode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_PreferredSupplierId",
                table: "inventory_items",
                column: "PreferredSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_Sku",
                table: "inventory_items",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_Type_IsActive_Name",
                table: "inventory_items",
                columns: new[] { "Type", "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_locations_BranchId_IsActive_Name",
                table: "inventory_locations",
                columns: new[] { "BranchId", "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_locations_Code",
                table: "inventory_locations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stock_levels_InventoryItemId_InventoryLocationId",
                table: "inventory_stock_levels",
                columns: new[] { "InventoryItemId", "InventoryLocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stock_levels_InventoryLocationId_QuantityOnHand",
                table: "inventory_stock_levels",
                columns: new[] { "InventoryLocationId", "QuantityOnHand" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_CompanyPurchaseLineId",
                table: "inventory_transactions",
                column: "CompanyPurchaseLineId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_FromLocationId_OccurredAt",
                table: "inventory_transactions",
                columns: new[] { "FromLocationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_IdempotencyKey",
                table: "inventory_transactions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_InventoryItemId_OccurredAt",
                table: "inventory_transactions",
                columns: new[] { "InventoryItemId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_PerformedByUserId",
                table: "inventory_transactions",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_ReferenceType_ReferenceId",
                table: "inventory_transactions",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_ToLocationId_OccurredAt",
                table: "inventory_transactions",
                columns: new[] { "ToLocationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_TransactionNumber",
                table: "inventory_transactions",
                column: "TransactionNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_company_purchase_lines_inventory_items_InventoryItemId",
                table: "company_purchase_lines",
                column: "InventoryItemId",
                principalTable: "inventory_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_company_purchase_lines_inventory_items_InventoryItemId",
                table: "company_purchase_lines");

            migrationBuilder.DropTable(
                name: "appointment_attendees");

            migrationBuilder.DropTable(
                name: "contact_interactions");

            migrationBuilder.DropTable(
                name: "inventory_stock_levels");

            migrationBuilder.DropTable(
                name: "inventory_transactions");

            migrationBuilder.DropTable(
                name: "appointments");

            migrationBuilder.DropTable(
                name: "inventory_items");

            migrationBuilder.DropTable(
                name: "inventory_locations");

            migrationBuilder.DropTable(
                name: "contacts");

            migrationBuilder.DropIndex(
                name: "IX_sales_leads_IsArchived_Priority_NextActionAt",
                table: "sales_leads");

            migrationBuilder.DropIndex(
                name: "IX_company_purchases_ReceivingStatus_PurchaseDate",
                table: "company_purchases");

            migrationBuilder.DropIndex(
                name: "IX_company_purchase_lines_InventoryItemId",
                table: "company_purchase_lines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_company_purchase_lines_received",
                table: "company_purchase_lines");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "sales_leads");

            migrationBuilder.DropColumn(
                name: "LastContactedAt",
                table: "sales_leads");

            migrationBuilder.DropColumn(
                name: "LostReason",
                table: "sales_leads");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "sales_leads");

            migrationBuilder.DropColumn(
                name: "ReceivingStatus",
                table: "company_purchases");

            migrationBuilder.DropColumn(
                name: "InventoryItemId",
                table: "company_purchase_lines");

            migrationBuilder.DropColumn(
                name: "QuantityReceived",
                table: "company_purchase_lines");

            migrationBuilder.DropSequence(
                name: "appointment_number_seq");

            migrationBuilder.DropSequence(
                name: "inventory_transaction_number_seq");
        }
    }
}
