using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Customer> Customers { get; }
    DbSet<CargoItem> CargoItems { get; }
    DbSet<ShippingBatch> ShippingBatches { get; }
    DbSet<WalletLedger> WalletLedgers { get; }
    DbSet<UserAccount> UserAccounts { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<StaffProfile> StaffProfiles { get; }
    DbSet<StaffBranchMembership> StaffBranchMemberships { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<SalesLead> SalesLeads { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceLine> InvoiceLines { get; }
    DbSet<Payment> Payments { get; }
    DbSet<CreditNote> CreditNotes { get; }
    DbSet<CreditApplication> CreditApplications { get; }
    DbSet<FinancialTransaction> FinancialTransactions { get; }
    DbSet<TransactionDocument> TransactionDocuments { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<AccountActionToken> AccountActionTokens { get; }
    DbSet<Branch> Branches { get; }
    DbSet<ServiceRoute> ServiceRoutes { get; }
    DbSet<RateCard> RateCards { get; }
    DbSet<ShippingPriceCatalogueItem> ShippingPriceCatalogueItems { get; }
    DbSet<ShippingPriceHistory> ShippingPriceHistories { get; }
    DbSet<AdditionalServicePrice> AdditionalServicePrices { get; }
    DbSet<CustomerAddress> CustomerAddresses { get; }
    DbSet<ShippingQuote> ShippingQuotes { get; }
    DbSet<CargoPiece> CargoPieces { get; }
    DbSet<ShipmentMilestone> ShipmentMilestones { get; }
    DbSet<ShipmentDocument> ShipmentDocuments { get; }
    DbSet<SalesActivity> SalesActivities { get; }
    DbSet<SalesTarget> SalesTargets { get; }
    DbSet<LedgerAccount> LedgerAccounts { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalLine> JournalLines { get; }
    DbSet<AccountingPeriod> AccountingPeriods { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<SupplierBill> SupplierBills { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<CompanyPurchase> CompanyPurchases { get; }
    DbSet<CompanyPurchaseLine> CompanyPurchaseLines { get; }
    DbSet<PurchasePayout> PurchasePayouts { get; }
    DbSet<BankAccount> BankAccounts { get; }
    DbSet<IntegrationMessage> IntegrationMessages { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }
    DbSet<Guest> Guests { get; }
    DbSet<ClientProfile> ClientProfiles { get; }
    DbSet<CompanyProfile> CompanyProfiles { get; }
    DbSet<BranchControl> BranchControls { get; }
    DbSet<CommunicationMessage> CommunicationMessages { get; }
    DbSet<CommunicationAttachment> CommunicationAttachments { get; }
    DbSet<ExchangeRateSnapshot> ExchangeRateSnapshots { get; }
    DbSet<ShipmentContainer> ShipmentContainers { get; }
    DbSet<ShipmentMovement> ShipmentMovements { get; }
    DbSet<CargoAllocation> CargoAllocations { get; }
    DbSet<CarrierTrackingEvent> CarrierTrackingEvents { get; }
    DbSet<ClientDuplicateReview> ClientDuplicateReviews { get; }
    DbSet<StaffActivityLog> StaffActivityLogs { get; }
    DbSet<StaffLocationPing> StaffLocationPings { get; }
    DbSet<LegacyImportBatch> LegacyImportBatches { get; }
    DbSet<LegacyImportRow> LegacyImportRows { get; }
    DbSet<InventoryItem> InventoryItems { get; }
    DbSet<InventoryLocation> InventoryLocations { get; }
    DbSet<InventoryStockLevel> InventoryStockLevels { get; }
    DbSet<InventoryTransaction> InventoryTransactions { get; }
    DbSet<Contact> Contacts { get; }
    DbSet<ContactInteraction> ContactInteractions { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<AppointmentAttendee> AppointmentAttendees { get; }
    DbSet<EmailBroadcast> EmailBroadcasts { get; }
    DbSet<EmailBroadcastRecipient> EmailBroadcastRecipients { get; }
    DbSet<EmailBroadcastAttachment> EmailBroadcastAttachments { get; }
    DbSet<CustomerShipment> CustomerShipments { get; }
    DbSet<CustomerShipmentPriceComponent> CustomerShipmentPriceComponents { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }
    DbSet<OperationalManifest> OperationalManifests { get; }
    DbSet<OperationalManifestEntry> OperationalManifestEntries { get; }
    DbSet<OperationalManifestChargeLine> OperationalManifestChargeLines { get; }
    DbSet<CustomerLoyaltyAccount> CustomerLoyaltyAccounts { get; }
    DbSet<LoyaltyPointTransaction> LoyaltyPointTransactions { get; }
    DbSet<ScheduledPickup> ScheduledPickups { get; }
    DbSet<ManifestTrackingConnection> ManifestTrackingConnections { get; }
    DbSet<ManifestTrackingEvent> ManifestTrackingEvents { get; }
    DbSet<CargoItemIssue> CargoItemIssues { get; }
    DbSet<CargoItemIssueEvidence> CargoItemIssueEvidence { get; }
    DbSet<ReportExportJob> ReportExportJobs { get; }
    DbSet<StaffNotification> StaffNotifications { get; }
    DbSet<UserNotificationPreference> UserNotificationPreferences { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
