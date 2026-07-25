using Application.Common.Interfaces;
using Domain.Common;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json;

namespace Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CargoItem> CargoItems => Set<CargoItem>();
    public DbSet<ShippingBatch> ShippingBatches => Set<ShippingBatch>();
    public DbSet<WalletLedger> WalletLedgers => Set<WalletLedger>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<StaffBranchMembership> StaffBranchMemberships => Set<StaffBranchMembership>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SalesLead> SalesLeads => Set<SalesLead>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CreditNote> CreditNotes => Set<CreditNote>();
    public DbSet<CreditApplication> CreditApplications => Set<CreditApplication>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<TransactionDocument> TransactionDocuments => Set<TransactionDocument>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<AccountActionToken> AccountActionTokens => Set<AccountActionToken>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<ServiceRoute> ServiceRoutes => Set<ServiceRoute>();
    public DbSet<RateCard> RateCards => Set<RateCard>();
    public DbSet<ShippingPriceCatalogueItem> ShippingPriceCatalogueItems => Set<ShippingPriceCatalogueItem>();
    public DbSet<ShippingPriceHistory> ShippingPriceHistories => Set<ShippingPriceHistory>();
    public DbSet<AdditionalServicePrice> AdditionalServicePrices => Set<AdditionalServicePrice>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<ShippingQuote> ShippingQuotes => Set<ShippingQuote>();
    public DbSet<CargoPiece> CargoPieces => Set<CargoPiece>();
    public DbSet<ShipmentMilestone> ShipmentMilestones => Set<ShipmentMilestone>();
    public DbSet<ShipmentDocument> ShipmentDocuments => Set<ShipmentDocument>();
    public DbSet<SalesActivity> SalesActivities => Set<SalesActivity>();
    public DbSet<SalesTarget> SalesTargets => Set<SalesTarget>();
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierBill> SupplierBills => Set<SupplierBill>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<CompanyPurchase> CompanyPurchases => Set<CompanyPurchase>();
    public DbSet<CompanyPurchaseLine> CompanyPurchaseLines => Set<CompanyPurchaseLine>();
    public DbSet<PurchasePayout> PurchasePayouts => Set<PurchasePayout>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<IntegrationMessage> IntegrationMessages => Set<IntegrationMessage>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<ClientProfile> ClientProfiles => Set<ClientProfile>();
    public DbSet<CompanyProfile> CompanyProfiles => Set<CompanyProfile>();
    public DbSet<BranchControl> BranchControls => Set<BranchControl>();
    public DbSet<CommunicationMessage> CommunicationMessages => Set<CommunicationMessage>();
    public DbSet<CommunicationAttachment> CommunicationAttachments => Set<CommunicationAttachment>();
    public DbSet<ExchangeRateSnapshot> ExchangeRateSnapshots => Set<ExchangeRateSnapshot>();
    public DbSet<ShipmentContainer> ShipmentContainers => Set<ShipmentContainer>();
    public DbSet<ShipmentMovement> ShipmentMovements => Set<ShipmentMovement>();
    public DbSet<CargoAllocation> CargoAllocations => Set<CargoAllocation>();
    public DbSet<CarrierTrackingEvent> CarrierTrackingEvents => Set<CarrierTrackingEvent>();
    public DbSet<ClientDuplicateReview> ClientDuplicateReviews => Set<ClientDuplicateReview>();
    public DbSet<StaffActivityLog> StaffActivityLogs => Set<StaffActivityLog>();
    public DbSet<StaffLocationPing> StaffLocationPings => Set<StaffLocationPing>();
    public DbSet<LegacyImportBatch> LegacyImportBatches => Set<LegacyImportBatch>();
    public DbSet<LegacyImportRow> LegacyImportRows => Set<LegacyImportRow>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryLocation> InventoryLocations => Set<InventoryLocation>();
    public DbSet<InventoryStockLevel> InventoryStockLevels => Set<InventoryStockLevel>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<ContactInteraction> ContactInteractions => Set<ContactInteraction>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentAttendee> AppointmentAttendees => Set<AppointmentAttendee>();
    public DbSet<EmailBroadcast> EmailBroadcasts => Set<EmailBroadcast>();
    public DbSet<EmailBroadcastRecipient> EmailBroadcastRecipients => Set<EmailBroadcastRecipient>();
    public DbSet<EmailBroadcastAttachment> EmailBroadcastAttachments => Set<EmailBroadcastAttachment>();
    public DbSet<CustomerShipment> CustomerShipments => Set<CustomerShipment>();
    public DbSet<CustomerShipmentPriceComponent> CustomerShipmentPriceComponents => Set<CustomerShipmentPriceComponent>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<OperationalManifest> OperationalManifests => Set<OperationalManifest>();
    public DbSet<OperationalManifestEntry> OperationalManifestEntries => Set<OperationalManifestEntry>();
    public DbSet<OperationalManifestChargeLine> OperationalManifestChargeLines => Set<OperationalManifestChargeLine>();
    public DbSet<CustomerLoyaltyAccount> CustomerLoyaltyAccounts => Set<CustomerLoyaltyAccount>();
    public DbSet<LoyaltyPointTransaction> LoyaltyPointTransactions => Set<LoyaltyPointTransaction>();
    public DbSet<ScheduledPickup> ScheduledPickups => Set<ScheduledPickup>();
    public DbSet<ManifestTrackingConnection> ManifestTrackingConnections => Set<ManifestTrackingConnection>();
    public DbSet<ManifestTrackingEvent> ManifestTrackingEvents => Set<ManifestTrackingEvent>();
    public DbSet<CargoItemIssue> CargoItemIssues => Set<CargoItemIssue>();
    public DbSet<CargoItemIssueEvidence> CargoItemIssueEvidence => Set<CargoItemIssueEvidence>();
    public DbSet<ReportExportJob> ReportExportJobs => Set<ReportExportJob>();
    public DbSet<StaffNotification> StaffNotifications => Set<StaffNotification>();
    public DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();

    private readonly ICurrentUserService _currentUser;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<BaseEvent>();
        modelBuilder.HasSequence<long>("client_number_seq").StartsAt(1).IncrementsBy(1).HasMin(1);
        modelBuilder.HasSequence<long>("invoice_number_seq").StartsAt(0).IncrementsBy(1).HasMin(0);
        modelBuilder.HasSequence<long>("financial_transaction_number_seq").StartsAt(1).IncrementsBy(1).HasMin(1);
        modelBuilder.HasSequence<long>("receipt_number_seq").StartsAt(1).IncrementsBy(1).HasMin(1);
        modelBuilder.HasSequence<long>("credit_note_number_seq").StartsAt(1).IncrementsBy(1).HasMin(1);
        modelBuilder.HasSequence<long>("payment_voucher_number_seq").StartsAt(1).IncrementsBy(1).HasMin(1);
        modelBuilder.HasSequence<long>("company_purchase_number_seq").StartsAt(1).IncrementsBy(1).HasMin(1);
        modelBuilder.HasSequence<long>("purchase_payout_number_seq").StartsAt(1).IncrementsBy(1).HasMin(1);
        modelBuilder.HasSequence<long>("inventory_transaction_number_seq").StartsAt(1).IncrementsBy(1).HasMin(1);
        modelBuilder.HasSequence<long>("appointment_number_seq").StartsAt(1).IncrementsBy(1).HasMin(1);
        modelBuilder.HasSequence<long>("email_broadcast_number_seq").StartsAt(1).IncrementsBy(1).HasMin(1);
        modelBuilder.HasSequence<long>("operational_manifest_number_seq").StartsAt(1).IncrementsBy(1).HasMin(1);
        modelBuilder.HasSequence<long>("scheduled_pickup_number_seq").StartsAt(1).IncrementsBy(1).HasMin(1);
        modelBuilder.HasSequence<long>("cargo_item_issue_number_seq").StartsAt(1).IncrementsBy(1).HasMin(1);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        NormalizeCustomerIdentities();
        NormalizeContacts();
        NormalizeUserIdentities();
        ApplyAuditInformation();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void NormalizeUserIdentities()
    {
        foreach (var entry in ChangeTracker.Entries<UserAccount>().Where(x =>
                     x.State is EntityState.Added or EntityState.Modified))
        {
            var user = entry.Entity;
            user.FullName = string.Join(' ', user.FullName.Trim()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            user.Email = user.Email.Trim().ToLowerInvariant();
            user.NormalizedEmail = Application.Common.ClientIdentityNormalizer.Email(user.Email);
            user.NormalizedFullName = Application.Common.ClientIdentityNormalizer.Name(user.FullName);
        }
    }

    private void NormalizeCustomerIdentities()
    {
        foreach (var entry in ChangeTracker.Entries<Customer>().Where(x =>
                     x.State is EntityState.Added or EntityState.Modified))
        {
            var customer = entry.Entity;
            customer.FullName = string.Join(' ', customer.FullName.Trim()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            customer.Email = string.IsNullOrWhiteSpace(customer.Email) ? null : customer.Email.Trim().ToLowerInvariant();
            customer.Phone = customer.Phone.Trim();
            customer.NormalizedFullName = Application.Common.ClientIdentityNormalizer.Name(customer.FullName);
            customer.NormalizedEmail = string.IsNullOrWhiteSpace(customer.Email) ? null :
                Application.Common.ClientIdentityNormalizer.Email(customer.Email);
            customer.NormalizedPhone = Application.Common.ClientIdentityNormalizer.Phone(customer.Phone);
        }
    }

    private void NormalizeContacts()
    {
        foreach (var entry in ChangeTracker.Entries<Contact>().Where(x =>
                     x.State is EntityState.Added or EntityState.Modified))
        {
            var contact = entry.Entity;
            contact.FullName = string.Join(' ', contact.FullName.Trim()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            contact.Email = contact.Email.Trim().ToLowerInvariant();
            contact.Phone = contact.Phone.Trim();
            contact.NormalizedFullName = Application.Common.ClientIdentityNormalizer.Name(contact.FullName);
            contact.NormalizedEmail = Application.Common.ClientIdentityNormalizer.Email(contact.Email);
            contact.NormalizedPhone = Application.Common.ClientIdentityNormalizer.Phone(contact.Phone);
        }
    }

    private void ApplyAuditInformation()
    {
        var now = DateTime.UtcNow;
        var actor = _currentUser.UserId ?? "system";
        var entries = ChangeTracker.Entries<BaseEntity>()
            .Where(entry => entry.Entity is not AuditLog and not StaffActivityLog &&
                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = actor;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.LastModifiedAt = now;
                entry.Entity.LastModifiedBy = actor;
            }

            var changedValues = entry.Properties
                .Where(property => entry.State != EntityState.Modified || property.IsModified)
                .Where(property => !string.Equals(property.Metadata.Name, "PasswordHash", StringComparison.Ordinal))
                .ToDictionary(
                    property => property.Metadata.Name,
                    property => entry.State == EntityState.Deleted ? property.OriginalValue : property.CurrentValue);

            AuditLogs.Add(new AuditLog
            {
                ActorId = actor,
                Action = entry.State.ToString(),
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entry.Entity.Id.ToString(),
                ChangesJson = JsonSerializer.Serialize(changedValues),
                IpAddress = _currentUser.IpAddress,
                CreatedAt = now,
                CreatedBy = actor
            });
        }
    }
}
