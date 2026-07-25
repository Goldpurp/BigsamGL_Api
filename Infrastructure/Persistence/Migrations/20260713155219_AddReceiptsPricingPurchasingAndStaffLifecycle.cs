using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptsPricingPurchasingAndStaffLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "company_purchase_number_seq",
                minValue: 1L);

            migrationBuilder.CreateSequence(
                name: "financial_transaction_number_seq",
                minValue: 1L);

            migrationBuilder.CreateSequence(
                name: "payment_voucher_number_seq",
                minValue: 1L);

            migrationBuilder.CreateSequence(
                name: "purchase_payout_number_seq",
                minValue: 1L);

            migrationBuilder.CreateSequence(
                name: "receipt_number_seq",
                minValue: 1L);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "staff_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                table: "staff_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusReason",
                table: "staff_profiles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BankAccountId",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementAccountCode",
                table: "payments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreferredPaymentMethod",
                table: "invoices",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationCode",
                table: "invoices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE invoices
                SET "PreferredPaymentMethod" = CASE WHEN "Type" = 'CreditNote' THEN NULL ELSE 'BankTransfer' END,
                    "VerificationCode" = UPPER(md5("Id"::text || random()::text || clock_timestamp()::text));

                UPDATE payments
                SET "SettlementAccountCode" = CASE WHEN "Method" = 'Cash' THEN '1000' ELSE '1010' END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "VerificationCode",
                table: "invoices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "SettlementAccountCode",
                table: "payments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "");

            migrationBuilder.CreateTable(
                name: "additional_service_prices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    BranchCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ReplacesPriceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_additional_service_prices", x => x.Id);
                    table.CheckConstraint("CK_additional_service_prices_amount", "\"Amount\" >= 0");
                    table.CheckConstraint("CK_additional_service_prices_dates", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\"");
                    table.ForeignKey(
                        name: "FK_additional_service_prices_additional_service_prices_Replace~",
                        column: x => x.ReplacesPriceId,
                        principalTable: "additional_service_prices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company_purchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    VendorName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BranchCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SupplierReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_purchases", x => x.Id);
                    table.CheckConstraint("CK_company_purchases_due_date", "\"DueDate\" IS NULL OR \"DueDate\" >= \"PurchaseDate\"");
                    table.CheckConstraint("CK_company_purchases_totals", "\"Subtotal\" >= 0 AND \"TaxAmount\" >= 0 AND \"TotalAmount\" > 0 AND \"AmountPaid\" >= 0 AND \"AmountPaid\" <= \"TotalAmount\"");
                    table.ForeignKey(
                        name: "FK_company_purchases_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CounterpartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CounterpartyName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SettlementAccountCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BranchCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_transactions", x => x.Id);
                    table.CheckConstraint("CK_financial_transactions_amount", "\"Amount\" > 0");
                    table.CheckConstraint("CK_financial_transactions_currency", "char_length(\"Currency\") = 3");
                    table.ForeignKey(
                        name: "FK_financial_transactions_bank_accounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "bank_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_financial_transactions_journal_entries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "company_purchase_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyPurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_purchase_lines", x => x.Id);
                    table.CheckConstraint("CK_company_purchase_lines_price", "\"UnitPrice\" >= 0");
                    table.CheckConstraint("CK_company_purchase_lines_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_company_purchase_lines_tax", "\"TaxRate\" >= 0 AND \"TaxRate\" <= 1");
                    table.ForeignKey(
                        name: "FK_company_purchase_lines_company_purchases_CompanyPurchaseId",
                        column: x => x.CompanyPurchaseId,
                        principalTable: "company_purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transaction_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FinancialTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyPurchaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchasePayoutId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerificationCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_transaction_documents_company_purchases_CompanyPurchaseId",
                        column: x => x.CompanyPurchaseId,
                        principalTable: "company_purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transaction_documents_financial_transactions_FinancialTrans~",
                        column: x => x.FinancialTransactionId,
                        principalTable: "financial_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transaction_documents_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transaction_documents_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_payouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayoutNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CompanyPurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    SettlementAccountCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaidByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_payouts", x => x.Id);
                    table.CheckConstraint("CK_purchase_payouts_amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_purchase_payouts_bank_accounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "bank_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_purchase_payouts_company_purchases_CompanyPurchaseId",
                        column: x => x.CompanyPurchaseId,
                        principalTable: "company_purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_payouts_financial_transactions_FinancialTransactio~",
                        column: x => x.FinancialTransactionId,
                        principalTable: "financial_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_payouts_transaction_documents_TransactionDocumentId",
                        column: x => x.TransactionDocumentId,
                        principalTable: "transaction_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO financial_transactions
                    ("Id", "TransactionNumber", "Direction", "Type", "Status", "SourceType", "SourceId",
                     "CounterpartyId", "CounterpartyName", "Amount", "Currency", "Method",
                     "SettlementAccountCode", "BankAccountId", "ExternalReference", "OccurredAt", "BranchCode",
                     "RecordedByUserId", "JournalEntryId", "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy")
                SELECT payment."Id",
                       'BGL' || lpad(nextval('financial_transaction_number_seq')::text, 8, '0') || 'TRX',
                       'MoneyIn', 'InvoicePayment', 'Posted', 'Payment', payment."Id",
                       payment."CustomerId", invoice."CustomerName", payment."Amount", payment."Currency",
                       payment."Method", payment."SettlementAccountCode", payment."BankAccountId", payment."Reference",
                       payment."PaidAt", invoice."IssuerBranchCode", '00000000-0000-0000-0000-000000000000'::uuid,
                       journal."Id", payment."CreatedAt", payment."CreatedBy", payment."LastModifiedAt", payment."LastModifiedBy"
                FROM payments AS payment
                JOIN invoices AS invoice ON invoice."Id" = payment."InvoiceId"
                LEFT JOIN LATERAL (
                    SELECT entry."Id"
                    FROM journal_entries AS entry
                    WHERE entry."SourceType" = 'Payment' AND entry."SourceId" = payment."Id"
                    ORDER BY entry."CreatedAt", entry."Id"
                    LIMIT 1
                ) AS journal ON TRUE;

                INSERT INTO transaction_documents
                    ("Id", "DocumentNumber", "Type", "Status", "FinancialTransactionId", "InvoiceId", "PaymentId",
                     "CompanyPurchaseId", "PurchasePayoutId", "IssuedAt", "IssuedByUserId", "VerificationCode",
                     "VoidedAt", "VoidedByUserId", "VoidReason", "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy")
                SELECT payment."Id", 'BGL' || lpad(nextval('receipt_number_seq')::text, 6, '0') || 'RCT',
                       'Receipt', 'Issued', payment."Id", payment."InvoiceId", payment."Id", NULL, NULL,
                       payment."CreatedAt", '00000000-0000-0000-0000-000000000000'::uuid,
                       UPPER(md5(payment."Id"::text || random()::text || clock_timestamp()::text)),
                       NULL, NULL, NULL, payment."CreatedAt", payment."CreatedBy", NULL, NULL
                FROM payments AS payment;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_payments_BankAccountId",
                table: "payments",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_SourceType_SourceId",
                table: "journal_entries",
                columns: new[] { "SourceType", "SourceId" },
                unique: true,
                filter: "\"SourceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_VerificationCode",
                table: "invoices",
                column: "VerificationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_additional_service_prices_Category_BranchCode_IsActive",
                table: "additional_service_prices",
                columns: new[] { "Category", "BranchCode", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_additional_service_prices_Code_BranchCode",
                table: "additional_service_prices",
                columns: new[] { "Code", "BranchCode" },
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_additional_service_prices_Code_BranchCode_EffectiveFrom",
                table: "additional_service_prices",
                columns: new[] { "Code", "BranchCode", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_additional_service_prices_ReplacesPriceId",
                table: "additional_service_prices",
                column: "ReplacesPriceId");

            migrationBuilder.CreateIndex(
                name: "IX_company_purchase_lines_CompanyPurchaseId",
                table: "company_purchase_lines",
                column: "CompanyPurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_company_purchases_BranchCode_Status_PurchaseDate",
                table: "company_purchases",
                columns: new[] { "BranchCode", "Status", "PurchaseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_company_purchases_Category_PurchaseDate",
                table: "company_purchases",
                columns: new[] { "Category", "PurchaseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_company_purchases_PurchaseNumber",
                table: "company_purchases",
                column: "PurchaseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_purchases_Status_PurchaseDate",
                table: "company_purchases",
                columns: new[] { "Status", "PurchaseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_company_purchases_SupplierId_PurchaseDate",
                table: "company_purchases",
                columns: new[] { "SupplierId", "PurchaseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_BankAccountId",
                table: "financial_transactions",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_BranchCode_OccurredAt",
                table: "financial_transactions",
                columns: new[] { "BranchCode", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_CounterpartyId",
                table: "financial_transactions",
                column: "CounterpartyId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_Currency_OccurredAt",
                table: "financial_transactions",
                columns: new[] { "Currency", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_Direction_Status_OccurredAt",
                table: "financial_transactions",
                columns: new[] { "Direction", "Status", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_JournalEntryId",
                table: "financial_transactions",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_SourceType_SourceId",
                table: "financial_transactions",
                columns: new[] { "SourceType", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_TransactionNumber",
                table: "financial_transactions",
                column: "TransactionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_Type_OccurredAt",
                table: "financial_transactions",
                columns: new[] { "Type", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_payouts_BankAccountId",
                table: "purchase_payouts",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_payouts_CompanyPurchaseId_PaidAt",
                table: "purchase_payouts",
                columns: new[] { "CompanyPurchaseId", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_payouts_FinancialTransactionId",
                table: "purchase_payouts",
                column: "FinancialTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_payouts_PayoutNumber",
                table: "purchase_payouts",
                column: "PayoutNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_payouts_Reference",
                table: "purchase_payouts",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_payouts_TransactionDocumentId",
                table: "purchase_payouts",
                column: "TransactionDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transaction_documents_CompanyPurchaseId",
                table: "transaction_documents",
                column: "CompanyPurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_documents_DocumentNumber",
                table: "transaction_documents",
                column: "DocumentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transaction_documents_FinancialTransactionId",
                table: "transaction_documents",
                column: "FinancialTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transaction_documents_InvoiceId",
                table: "transaction_documents",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_documents_PaymentId",
                table: "transaction_documents",
                column: "PaymentId",
                unique: true,
                filter: "\"PaymentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_documents_PurchasePayoutId",
                table: "transaction_documents",
                column: "PurchasePayoutId",
                unique: true,
                filter: "\"PurchasePayoutId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_documents_Type_Status_IssuedAt",
                table: "transaction_documents",
                columns: new[] { "Type", "Status", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_transaction_documents_VerificationCode",
                table: "transaction_documents",
                column: "VerificationCode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_bank_accounts_BankAccountId",
                table: "payments",
                column: "BankAccountId",
                principalTable: "bank_accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payments_bank_accounts_BankAccountId",
                table: "payments");

            migrationBuilder.DropTable(
                name: "additional_service_prices");

            migrationBuilder.DropTable(
                name: "company_purchase_lines");

            migrationBuilder.DropTable(
                name: "purchase_payouts");

            migrationBuilder.DropTable(
                name: "transaction_documents");

            migrationBuilder.DropTable(
                name: "company_purchases");

            migrationBuilder.DropTable(
                name: "financial_transactions");

            migrationBuilder.DropIndex(
                name: "IX_payments_BankAccountId",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_journal_entries_SourceType_SourceId",
                table: "journal_entries");

            migrationBuilder.DropIndex(
                name: "IX_invoices_VerificationCode",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "staff_profiles");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "staff_profiles");

            migrationBuilder.DropColumn(
                name: "StatusReason",
                table: "staff_profiles");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "SettlementAccountCode",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "PreferredPaymentMethod",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "VerificationCode",
                table: "invoices");

            migrationBuilder.DropSequence(
                name: "company_purchase_number_seq");

            migrationBuilder.DropSequence(
                name: "financial_transaction_number_seq");

            migrationBuilder.DropSequence(
                name: "payment_voucher_number_seq");

            migrationBuilder.DropSequence(
                name: "purchase_payout_number_seq");

            migrationBuilder.DropSequence(
                name: "receipt_number_seq");
        }
    }
}
