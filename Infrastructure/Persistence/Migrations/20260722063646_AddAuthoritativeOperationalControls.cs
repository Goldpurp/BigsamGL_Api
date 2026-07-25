using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthoritativeOperationalControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "cargo_item_issue_number_seq",
                minValue: 1L);

            migrationBuilder.CreateSequence(
                name: "scheduled_pickup_number_seq",
                minValue: 1L);

            migrationBuilder.CreateTable(
                name: "cargo_item_issues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CustomerShipmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ManifestId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManifestLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpectedDetails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FoundDetails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LastSeenPlace = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InternalNote = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CustomerVisible = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Resolution = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ClientNotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cargo_item_issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cargo_item_issues_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cargo_item_issues_customer_shipments_CustomerShipmentId",
                        column: x => x.CustomerShipmentId,
                        principalTable: "customer_shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cargo_item_issues_operational_manifest_entries_ManifestLine~",
                        column: x => x.ManifestLineId,
                        principalTable: "operational_manifest_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cargo_item_issues_operational_manifests_ManifestId",
                        column: x => x.ManifestId,
                        principalTable: "operational_manifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "manifest_tracking_connections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    Provider = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ShippingCompanyName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PublicTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProviderStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastKnownPlace = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EstimatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSuccessfulSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manifest_tracking_connections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manifest_tracking_connections_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_manifest_tracking_connections_operational_manifests_Manifes~",
                        column: x => x.ManifestId,
                        principalTable: "operational_manifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_pickups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PickupNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    GoodsType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Contents = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    HandlingNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExpectedPieceCount = table.Column<int>(type: "integer", nullable: true),
                    ActualWeightKg = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: true),
                    ChargeableWeightKg = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: true),
                    BoxSizesJson = table.Column<string>(type: "jsonb", nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PaymentReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PaymentState = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FinancialTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AssignedDriverUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedTeam = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CollectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeleteReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_pickups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scheduled_pickups_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_scheduled_pickups_customers_ClientId",
                        column: x => x.ClientId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_scheduled_pickups_financial_transactions_FinancialTransacti~",
                        column: x => x.FinancialTransactionId,
                        principalTable: "financial_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_scheduled_pickups_transaction_documents_ReceiptDocumentId",
                        column: x => x.ReceiptDocumentId,
                        principalTable: "transaction_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_scheduled_pickups_user_accounts_AssignedDriverUserId",
                        column: x => x.AssignedDriverUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "cargo_item_issue_evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CargoItemIssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cargo_item_issue_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cargo_item_issue_evidence_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cargo_item_issue_evidence_cargo_item_issues_CargoItemIssueId",
                        column: x => x.CargoItemIssueId,
                        principalTable: "cargo_item_issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cargo_item_issue_evidence_shipment_documents_ShipmentDocume~",
                        column: x => x.ShipmentDocumentId,
                        principalTable: "shipment_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "manifest_tracking_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    ProviderEventId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReviewState = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InternalTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InternalDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProviderStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RawPayloadProtected = table.Column<string>(type: "jsonb", nullable: true),
                    PublicTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PublicDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Place = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstimatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manifest_tracking_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manifest_tracking_events_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_manifest_tracking_events_manifest_tracking_connections_Conn~",
                        column: x => x.ConnectionId,
                        principalTable: "manifest_tracking_connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cargo_item_issue_evidence_BranchId",
                table: "cargo_item_issue_evidence",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_item_issue_evidence_CargoItemIssueId_Sha256",
                table: "cargo_item_issue_evidence",
                columns: new[] { "CargoItemIssueId", "Sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cargo_item_issue_evidence_ShipmentDocumentId",
                table: "cargo_item_issue_evidence",
                column: "ShipmentDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_item_issues_BranchId_State_Severity_CreatedAt",
                table: "cargo_item_issues",
                columns: new[] { "BranchId", "State", "Severity", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_cargo_item_issues_CustomerShipmentId",
                table: "cargo_item_issues",
                column: "CustomerShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_item_issues_InvoiceNumber",
                table: "cargo_item_issues",
                column: "InvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_item_issues_IssueNumber",
                table: "cargo_item_issues",
                column: "IssueNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cargo_item_issues_ManifestId",
                table: "cargo_item_issues",
                column: "ManifestId");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_item_issues_ManifestLineId",
                table: "cargo_item_issues",
                column: "ManifestLineId");

            migrationBuilder.CreateIndex(
                name: "IX_manifest_tracking_connections_BranchId_Status_LastSuccessfu~",
                table: "manifest_tracking_connections",
                columns: new[] { "BranchId", "Status", "LastSuccessfulSyncAt" });

            migrationBuilder.CreateIndex(
                name: "IX_manifest_tracking_connections_ManifestId",
                table: "manifest_tracking_connections",
                column: "ManifestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manifest_tracking_connections_Provider_ExternalReference",
                table: "manifest_tracking_connections",
                columns: new[] { "Provider", "ExternalReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manifest_tracking_connections_PublicTokenHash",
                table: "manifest_tracking_connections",
                column: "PublicTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manifest_tracking_events_BranchId_ReviewState_OccurredAt",
                table: "manifest_tracking_events",
                columns: new[] { "BranchId", "ReviewState", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_manifest_tracking_events_ConnectionId_ProviderEventId",
                table: "manifest_tracking_events",
                columns: new[] { "ConnectionId", "ProviderEventId" },
                unique: true,
                filter: "\"ProviderEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_pickups_AssignedDriverUserId",
                table: "scheduled_pickups",
                column: "AssignedDriverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_pickups_BranchId_Phone_ScheduledAt",
                table: "scheduled_pickups",
                columns: new[] { "BranchId", "Phone", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_pickups_BranchId_ScheduledAt_State",
                table: "scheduled_pickups",
                columns: new[] { "BranchId", "ScheduledAt", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_pickups_ClientId",
                table: "scheduled_pickups",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_pickups_FinancialTransactionId",
                table: "scheduled_pickups",
                column: "FinancialTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_pickups_PickupNumber",
                table: "scheduled_pickups",
                column: "PickupNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_pickups_ReceiptDocumentId",
                table: "scheduled_pickups",
                column: "ReceiptDocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cargo_item_issue_evidence");

            migrationBuilder.DropTable(
                name: "manifest_tracking_events");

            migrationBuilder.DropTable(
                name: "scheduled_pickups");

            migrationBuilder.DropTable(
                name: "cargo_item_issues");

            migrationBuilder.DropTable(
                name: "manifest_tracking_connections");

            migrationBuilder.DropSequence(
                name: "cargo_item_issue_number_seq");

            migrationBuilder.DropSequence(
                name: "scheduled_pickup_number_seq");
        }
    }
}
