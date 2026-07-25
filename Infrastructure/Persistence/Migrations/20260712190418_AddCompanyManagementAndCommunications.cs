using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyManagementAndCommunications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "shipment_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "branch_controls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAcceptingBookings = table.Column<bool>(type: "boolean", nullable: false),
                    IsWarehouseOpen = table.Column<bool>(type: "boolean", nullable: false),
                    IsDispatchEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CapacityAlertPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    DailyCargoLimit = table.Column<int>(type: "integer", nullable: true),
                    EmergencyNotice = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastControlledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastControlledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_controls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_branch_controls_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_branch_controls_user_accounts_LastControlledByUserId",
                        column: x => x.LastControlledByUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "client_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AccountManagerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreferredBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaxNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BusinessRegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BillingCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreditLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentTermsDays = table.Column<int>(type: "integer", nullable: false),
                    InternalNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RiskNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_profiles_branches_PreferredBranchId",
                        column: x => x.PreferredBranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_client_profiles_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_client_profiles_user_accounts_AccountManagerUserId",
                        column: x => x.AccountManagerUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "communication_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Subject = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communication_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_communication_messages_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TradingName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TaxNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Website = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RegisteredAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BaseCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LogoStorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ServiceInterest = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConvertedCustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastContactedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConvertedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guests_customers_ConvertedCustomerId",
                        column: x => x.ConvertedCustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_guests_user_accounts_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "communication_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunicationMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communication_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_communication_attachments_communication_messages_Communicat~",
                        column: x => x.CommunicationMessageId,
                        principalTable: "communication_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_communication_attachments_shipment_documents_ShipmentDocume~",
                        column: x => x.ShipmentDocumentId,
                        principalTable: "shipment_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_documents_CustomerId",
                table: "shipment_documents",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_branch_controls_BranchId",
                table: "branch_controls",
                column: "BranchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_branch_controls_LastControlledByUserId",
                table: "branch_controls",
                column: "LastControlledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_client_profiles_AccountManagerUserId",
                table: "client_profiles",
                column: "AccountManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_client_profiles_CustomerId",
                table: "client_profiles",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_profiles_PreferredBranchId",
                table: "client_profiles",
                column: "PreferredBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_communication_attachments_CommunicationMessageId_ShipmentDo~",
                table: "communication_attachments",
                columns: new[] { "CommunicationMessageId", "ShipmentDocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_communication_attachments_ShipmentDocumentId",
                table: "communication_attachments",
                column: "ShipmentDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_communication_messages_CustomerId_CreatedAt",
                table: "communication_messages",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_guests_AssignedToUserId",
                table: "guests",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_guests_ConvertedCustomerId",
                table: "guests",
                column: "ConvertedCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_guests_Email_Status",
                table: "guests",
                columns: new[] { "Email", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_shipment_documents_customers_CustomerId",
                table: "shipment_documents",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_shipment_documents_customers_CustomerId",
                table: "shipment_documents");

            migrationBuilder.DropTable(
                name: "branch_controls");

            migrationBuilder.DropTable(
                name: "client_profiles");

            migrationBuilder.DropTable(
                name: "communication_attachments");

            migrationBuilder.DropTable(
                name: "company_profiles");

            migrationBuilder.DropTable(
                name: "guests");

            migrationBuilder.DropTable(
                name: "communication_messages");

            migrationBuilder.DropIndex(
                name: "IX_shipment_documents_CustomerId",
                table: "shipment_documents");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "shipment_documents");
        }
    }
}
