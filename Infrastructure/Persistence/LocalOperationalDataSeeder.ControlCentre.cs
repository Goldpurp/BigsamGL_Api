using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed partial class LocalOperationalDataSeeder
{
    private async Task SeedControlCentreScenarioAsync(
        IReadOnlyDictionary<string, Branch> branches,
        IReadOnlyList<ServiceRoute> routes,
        IReadOnlyList<Customer> customers,
        IReadOnlyList<UserAccount> staffUsers,
        IReadOnlyList<CargoItem> cargo,
        IReadOnlyList<Invoice> invoices,
        CancellationToken cancellationToken)
    {
        var scenario = await SeedCustomerShipmentsAndManifestsAsync(
            branches, routes, customers, staffUsers, cargo, invoices, cancellationToken);
        await SeedFinancePurchasesAndInventoryAsync(
            branches, customers, staffUsers, cancellationToken);
        await SeedPricingWorkforceAndScheduleAsync(
            branches, customers, staffUsers, cancellationToken);
        await SeedContactsCommunicationsAndAuditAsync(
            branches, customers, staffUsers, scenario, cancellationToken);
    }

    private async Task<SeededManifestScenario> SeedCustomerShipmentsAndManifestsAsync(
        IReadOnlyDictionary<string, Branch> branches,
        IReadOnlyList<ServiceRoute> routes,
        IReadOnlyList<Customer> customers,
        IReadOnlyList<UserAccount> staffUsers,
        IReadOnlyList<CargoItem> cargo,
        IReadOnlyList<Invoice> invoices,
        CancellationToken cancellationToken)
    {
        var customerShipments = Enumerable.Range(0, invoices.Count).Select(index =>
        {
            var route = routes[index];
            var origin = branches.Values.Single(x => x.Id == route.OriginBranchId);
            var destination = branches.Values.Single(x => x.Id == route.DestinationBranchId);
            var actual = cargo[index].ActualWeightKg;
            var volumetric = cargo[index].VolumetricWeightKg;
            var chargeable = Math.Max(actual, volumetric);
            var handover = index % 3 == 0
                ? ShipmentHandoverMethod.Delivery
                : ShipmentHandoverMethod.Pickup;
            return new CustomerShipment
            {
                BranchId = origin.Id,
                CustomerId = customers[index].Id,
                InvoiceId = invoices[index].Id,
                InvoiceNumber = invoices[index].InvoiceNumber,
                InvoiceNumericFragment = new string(
                    invoices[index].InvoiceNumber.Where(char.IsDigit).ToArray()),
                ReceiverName = index % 2 == 0
                    ? customers[index].FullName
                    : $"{customers[index].FullName} Receiver",
                Method = route.Mode == TransportMode.Air
                    ? CustomerShipmentMethod.Air
                    : CustomerShipmentMethod.Sea,
                OriginCountryCode = origin.CountryCode,
                OriginCity = City(origin.Code),
                DestinationCountryCode = destination.CountryCode,
                DestinationCityOrState = City(destination.Code),
                DeliveryAddress = handover == ShipmentHandoverMethod.Delivery
                    ? $"{21 + index} Destination Road, {City(destination.Code)}"
                    : null,
                HandoverMethod = handover,
                GoodsType = index % 4 == 0 ? ShipmentGoodsType.Fragile : ShipmentGoodsType.NonFragile,
                Description = cargo[index].Description,
                Contents = CargoDescriptions[index],
                PieceCount = index % 3 + 1,
                PricingMethod = route.Mode == TransportMode.Air
                    ? CustomerShipmentPricingMethod.Weight
                    : index == 7
                        ? CustomerShipmentPricingMethod.NegotiatedPrice
                        : CustomerShipmentPricingMethod.BoxSizesAndItems,
                ServiceCodeSnapshot = route.Code,
                ActualWeightKg = actual,
                ChargeableWeightKg = chargeable,
                LengthCm = cargo[index].LengthCm,
                WidthCm = cargo[index].WidthCm,
                HeightCm = cargo[index].HeightCm,
                NegotiatedReason = index == 7
                    ? "Industrial equipment shipped under an approved machinery rate."
                    : null,
                MovementState = index < 2
                    ? CustomerShipmentMovementState.OnTheWay
                    : index < 6
                        ? CustomerShipmentMovementState.AssignedToManifest
                        : CustomerShipmentMovementState.ReadyForManifest,
                ReferredByClientId = index > 0 && index % 3 == 0 ? customers[0].Id : null,
                CustomerNote = "Local operational shipment used for workflow checks."
            };
        }).ToList();
        _context.CustomerShipments.AddRange(customerShipments);
        _context.CustomerShipmentPriceComponents.AddRange(
            customerShipments.Select((shipment, index) => new CustomerShipmentPriceComponent
            {
                CustomerShipmentId = shipment.Id,
                Type = shipment.PricingMethod switch
                {
                    CustomerShipmentPricingMethod.Weight => ShipmentPriceComponentType.Weight,
                    CustomerShipmentPricingMethod.NegotiatedPrice => ShipmentPriceComponentType.Negotiated,
                    _ => ShipmentPriceComponentType.CatalogueBox
                },
                CodeSnapshot = shipment.ServiceCodeSnapshot ?? $"LOCAL-{index + 1}",
                DescriptionSnapshot = shipment.PricingMethod switch
                {
                    CustomerShipmentPricingMethod.Weight => "Chargeable air weight",
                    CustomerShipmentPricingMethod.NegotiatedPrice => "Approved negotiated machinery charge",
                    _ => "Sea cargo box and item charge"
                },
                UnitSnapshot = shipment.PricingMethod == CustomerShipmentPricingMethod.Weight
                    ? "kg"
                    : "shipment",
                Quantity = shipment.PricingMethod == CustomerShipmentPricingMethod.Weight
                    ? shipment.ChargeableWeightKg!.Value
                    : 1,
                UnitPrice = shipment.PricingMethod == CustomerShipmentPricingMethod.Weight
                    ? Math.Round(invoices[index].TotalAmount / shipment.ChargeableWeightKg!.Value, 2)
                    : invoices[index].TotalAmount,
                LineAmount = invoices[index].TotalAmount,
                SortOrder = 0
            }));

        var manifests = Enumerable.Range(0, customerShipments.Count).Select(index =>
        {
            var route = routes[index];
            var origin = branches.Values.Single(x => x.Id == route.OriginBranchId);
            var destination = branches.Values.Single(x => x.Id == route.DestinationBranchId);
            var sea = route.Mode == TransportMode.Sea;
            var creator = OperatorForBranch(staffUsers, origin.Code);
            return new OperationalManifest
            {
                BranchId = origin.Id,
                ManifestNumber = $"BGL{2001 + index:0000}MNF",
                ShipmentId = $"BGL-{route.Code}-{26001 + index}",
                ShipmentBookingId = sea ? $"MSC-BKG-26{index + 1:000}" : $"AIR-AWB-26{index + 1:000}",
                BookingReference = $"BOOK-{route.Code}-{index + 1:00}",
                ServiceRouteId = route.Id,
                RouteCode = route.Code,
                RouteName = route.Name,
                OriginCountryCode = origin.CountryCode,
                OriginCity = City(origin.Code),
                DestinationCountryCode = destination.CountryCode,
                DestinationCity = City(destination.Code),
                ManifestDate = DateTime.UtcNow.Date.AddDays(-(index + 1)),
                LogisticsMethod = sea ? ManifestLogisticsMethod.Ship : ManifestLogisticsMethod.Air,
                CreatedByUserId = creator.Id,
                LastEditedByUserId = creator.Id,
                CarrierName = sea ? "MSC Mediterranean Shipping Company" : "BiGSAM Partner Airline",
                CarrierReference = sea ? $"MSCU26{index + 1:05}" : $"BA-CARGO-26{index + 1:04}",
                TrackingProvider = sea ? "MSC" : "LocalAir",
                ContainerOrFlightNumber = sea ? $"MSCU{2600000 + index}" : $"BA{200 + index}",
                BillOfLadingNumber = sea ? $"MEDUGB{260001 + index}" : null,
                FlightNumber = sea ? null : $"BA{200 + index}",
                VesselName = sea ? $"MSC Demo Vessel {index + 1}" : null,
                VoyageNumber = sea ? $"VY26{index + 1:03}" : null,
                Notes = "Seeded operational shipment linked to one protected customer invoice."
            };
        }).ToList();
        _context.OperationalManifests.AddRange(manifests);

        var entries = customerShipments.Select((shipment, index) =>
        {
            var invoice = invoices[index];
            var client = customers[index];
            var manifest = manifests[index];
            var route = routes[index];
            var actual = shipment.ActualWeightKg!.Value;
            var costWeight = shipment.ChargeableWeightKg!.Value;
            var sentOut = shipment.HandoverMethod == ShipmentHandoverMethod.Delivery;
            return new OperationalManifestEntry
            {
                ManifestId = manifest.Id,
                CustomerShipmentId = shipment.Id,
                CustomerId = client.Id,
                InvoiceId = invoice.Id,
                ReferrerCustomerId = shipment.ReferredByClientId,
                CargoItemId = cargo[index].Id,
                InvoiceNumberSnapshot = invoice.InvoiceNumber,
                ReferrerNameSnapshot = shipment.ReferredByClientId.HasValue ? customers[0].FullName : null,
                ReferralPointsAwarded = shipment.ReferredByClientId.HasValue ? 25 : 0,
                CustomerNameSnapshot = client.FullName,
                CustomerPhoneSnapshot = client.Phone,
                CustomerEmailSnapshot = client.Email!,
                ReceiverName = shipment.ReceiverName,
                ReceiverPhone = client.Phone,
                ReceiverEmail = client.Email,
                TrackingNumberSnapshot = cargo[index].TrackingNumber,
                SenderName = client.FullName,
                PackageDescription = cargo[index].Description,
                Contents = shipment.Contents,
                Quantity = shipment.PieceCount,
                ActualWeightKg = actual,
                CostWeightKg = costWeight,
                Amount = invoice.TotalAmount,
                Currency = invoice.Currency,
                PaymentType = invoice.PreferredPaymentMethod ?? PaymentMethod.BankTransfer,
                ChargeBasis = shipment.PricingMethod switch
                {
                    CustomerShipmentPricingMethod.Weight => ShipmentChargeBasis.AirWeightAndDimensions,
                    CustomerShipmentPricingMethod.NegotiatedPrice => ShipmentChargeBasis.Negotiated,
                    _ => ShipmentChargeBasis.SeaBoxesAndItems
                },
                NegotiationReference = shipment.PricingMethod == CustomerShipmentPricingMethod.NegotiatedPrice
                    ? $"NEG-{invoice.InvoiceNumericFragment()}"
                    : null,
                HandoverMethod = sentOut ? ManifestHandoverMethod.SentOut : ManifestHandoverMethod.HubCollection,
                HandoverAddress = sentOut ? shipment.DeliveryAddress : null,
                HandoverCity = sentOut ? shipment.DestinationCityOrState : City(
                    branches.Values.Single(x => x.Id == route.DestinationBranchId).Code),
                MovementCodeSnapshot = cargo[index].TrackingNumber,
                ContainerNumberSnapshot = route.Mode == TransportMode.Sea
                    ? manifest.ContainerOrFlightNumber
                    : null,
                Notes = "Invoice is immutable; package details remain editable through manifest controls.",
                AddedAt = DateTime.UtcNow.AddDays(-(index + 1)),
                AddedByUserId = manifest.CreatedByUserId
            };
        }).ToList();
        _context.OperationalManifestEntries.AddRange(entries);
        _context.OperationalManifestChargeLines.AddRange(entries.Select((entry, index) =>
        {
            var shipment = customerShipments[index];
            var air = shipment.Method == CustomerShipmentMethod.Air;
            var negotiated = shipment.PricingMethod == CustomerShipmentPricingMethod.NegotiatedPrice;
            return new OperationalManifestChargeLine
            {
                ManifestEntryId = entry.Id,
                Type = negotiated
                    ? ShipmentChargeLineType.Negotiated
                    : air
                        ? ShipmentChargeLineType.AirWeight
                        : ShipmentChargeLineType.SeaBox,
                Description = negotiated
                    ? "Negotiated industrial cargo charge"
                    : air
                        ? "Air chargeable weight"
                        : "Sea cargo box charge",
                BoxSize = air ? null : index % 2 == 0 ? "Large" : "Medium",
                Quantity = air ? entry.CostWeightKg : entry.Quantity,
                UnitPrice = Math.Round(entry.Amount / (air ? entry.CostWeightKg : entry.Quantity), 2),
                LengthCm = air ? shipment.LengthCm : null,
                WidthCm = air ? shipment.WidthCm : null,
                HeightCm = air ? shipment.HeightCm : null,
                ActualWeightKg = air ? shipment.ActualWeightKg : null,
                VolumetricDivisor = air ? 5000 : null,
                VolumetricWeightKg = air ? cargo[index].VolumetricWeightKg : null,
                BillableWeightKg = air ? shipment.ChargeableWeightKg : null,
                LineAmount = entry.Amount,
                SortOrder = 0
            };
        }));

        var connections = manifests.Select((manifest, index) =>
        {
            var sea = manifest.LogisticsMethod == ManifestLogisticsMethod.Ship;
            return new ManifestTrackingConnection
            {
                BranchId = manifest.BranchId,
                ManifestId = manifest.Id,
                Provider = sea ? "MSC" : "LocalAir",
                ShippingCompanyName = manifest.CarrierName!,
                ExternalReference = sea
                    ? manifest.BillOfLadingNumber!
                    : manifest.CarrierReference!,
                ReferenceType = sea
                    ? CarrierTrackingReferenceType.BillOfLading
                    : CarrierTrackingReferenceType.BookingNumber,
                PublicTokenHash = Sha256($"manifest-public-token-{index + 1}"),
                Status = index < 2
                    ? ManifestTrackingStatus.InTransit
                    : index == 2
                        ? ManifestTrackingStatus.Delayed
                        : index < 6
                            ? ManifestTrackingStatus.Connected
                            : ManifestTrackingStatus.Arrived,
                ProviderStatus = index == 2 ? "Carrier schedule adjusted" : "Movement confirmed",
                LastKnownPlace = index % 3 == 0 ? City(branches["UK-MAN"].Code) :
                    index % 3 == 1 ? City(branches["NG-LOS"].Code) : City(branches["CN-GZ"].Code),
                EstimatedAt = DateTime.UtcNow.AddDays(8 + index),
                LastSuccessfulSyncAt = DateTime.UtcNow.AddHours(-(index + 1)),
                NextSyncAt = DateTime.UtcNow.AddHours(2)
            };
        }).ToList();
        _context.ManifestTrackingConnections.AddRange(connections);
        _context.ManifestTrackingEvents.AddRange(connections.SelectMany((connection, index) =>
        {
            var user = OperatorForBranch(staffUsers,
                branches.Values.Single(x => x.Id == connection.BranchId).Code);
            return new[]
            {
                new ManifestTrackingEvent
                {
                    BranchId = connection.BranchId,
                    ConnectionId = connection.Id,
                    ProviderEventId = $"PROVIDER-{index + 1:00}-1",
                    Source = TrackingEventSource.Provider,
                    ReviewState = TrackingEventReviewState.Draft,
                    InternalTitle = "Carrier movement imported",
                    InternalDescription = "Provider movement stored internally for staff review.",
                    ProviderStatus = "Received by carrier",
                    RawPayloadProtected = JsonSerializer.Serialize(new
                    {
                        provider = connection.Provider,
                        reference = connection.ExternalReference,
                        environment = "local"
                    }),
                    Place = connection.LastKnownPlace,
                    OccurredAt = DateTime.UtcNow.AddDays(-(index + 2))
                },
                new ManifestTrackingEvent
                {
                    BranchId = connection.BranchId,
                    ConnectionId = connection.Id,
                    ProviderEventId = $"PROVIDER-{index + 1:00}-2",
                    Source = TrackingEventSource.Provider,
                    ReviewState = TrackingEventReviewState.Published,
                    InternalTitle = "Departure confirmed",
                    InternalDescription = "Staff reviewed the external movement.",
                    ProviderStatus = "In transit",
                    PublicTitle = "Your shipment is on the way",
                    PublicDescription = "BiGSAM has confirmed that this shipment is moving to the next hub.",
                    Place = connection.LastKnownPlace,
                    OccurredAt = DateTime.UtcNow.AddDays(-(index + 1)),
                    ReviewedByUserId = user.Id,
                    ReviewedAt = DateTime.UtcNow.AddHours(-(index + 3)),
                    PublishedByUserId = user.Id,
                    PublishedAt = DateTime.UtcNow.AddHours(-(index + 2))
                }
            };
        }));

        var issues = Enumerable.Range(0, 5).Select(index => new CargoItemIssue
        {
            BranchId = manifests[index].BranchId,
            IssueNumber = $"BGL-ISS-26{index + 1:03}",
            Type = (CargoItemIssueType)index,
            Severity = index < 2 ? CargoItemIssueSeverity.Low :
                index < 4 ? CargoItemIssueSeverity.Medium : CargoItemIssueSeverity.High,
            State = index < 2 ? CargoItemIssueState.Resolved :
                index == 2 ? CargoItemIssueState.Released : CargoItemIssueState.Open,
            CustomerShipmentId = customerShipments[index].Id,
            InvoiceNumber = invoices[index].InvoiceNumber,
            ManifestId = manifests[index].Id,
            ManifestLineId = entries[index].Id,
            ExpectedDetails = $"Expected {cargo[index].Description} with marker {cargo[index].MarkerCode}.",
            FoundDetails = index % 2 == 0
                ? "Label required a second warehouse check."
                : "Package was scanned in the adjacent holding bay.",
            LastSeenPlace = manifests[index].OriginCity,
            LastSeenAt = DateTime.UtcNow.AddHours(-(index + 2)),
            InternalNote = "Local exception record for traceable mismatch and location handling.",
            CustomerVisible = index >= 3,
            ResolvedAt = index < 2 ? DateTime.UtcNow.AddHours(-index) : null,
            ResolvedByUserId = index < 2 ? manifests[index].CreatedByUserId : null,
            Resolution = index < 2 ? "Package verified and returned to the correct manifest lane." : null,
            ClientNotifiedAt = index >= 3 ? DateTime.UtcNow.AddHours(-1) : null
        }).ToList();
        _context.CargoItemIssues.AddRange(issues);
        await _context.SaveChangesAsync(cancellationToken);
        return new SeededManifestScenario(customerShipments, manifests, entries, connections, issues);
    }

    private async Task SeedFinancePurchasesAndInventoryAsync(
        IReadOnlyDictionary<string, Branch> branches,
        IReadOnlyList<Customer> customers,
        IReadOnlyList<UserAccount> staffUsers,
        CancellationToken cancellationToken)
    {
        var payments = await _context.Payments.OrderBy(x => x.PaidAt).ToListAsync(cancellationToken);
        var invoices = await _context.Invoices.ToDictionaryAsync(x => x.Id, cancellationToken);
        var accounts = await _context.LedgerAccounts.ToDictionaryAsync(x => x.Code, cancellationToken);
        var paymentTransactions = new List<FinancialTransaction>();
        var receiptDocuments = new List<TransactionDocument>();
        for (var index = 0; index < payments.Count; index++)
        {
            var payment = payments[index];
            var invoice = invoices[payment.InvoiceId];
            var transaction = new FinancialTransaction
            {
                BranchId = payment.BranchId,
                TransactionNumber = $"BGL{3001 + index:0000}TXN",
                Direction = FinancialTransactionDirection.Credit,
                Type = FinancialTransactionType.SalesPayment,
                SourceType = nameof(Payment),
                SourceId = payment.Id,
                CounterpartyId = payment.CustomerId,
                CounterpartyName = invoice.CustomerName,
                Amount = payment.Amount,
                Currency = payment.Currency,
                Method = payment.Method,
                SettlementAccountCode = payment.SettlementAccountCode,
                ExternalReference = payment.Reference,
                OccurredAt = payment.PaidAt,
                BranchCode = invoice.IssuerBranchCode,
                RecordedByUserId = OperatorForBranch(staffUsers, invoice.IssuerBranchCode).Id,
                FreightClassification = FreightForInvoice(invoice)
            };
            var journal = new JournalEntry
            {
                BranchId = payment.BranchId,
                Currency = payment.Currency,
                JournalNumber = $"BGL-JNL-{3001 + index}",
                EntryDate = payment.PaidAt,
                Description = $"Payment received for {invoice.InvoiceNumber}",
                SourceType = nameof(Payment),
                SourceId = payment.Id,
                Status = JournalStatus.Posted,
                PostedAt = payment.PaidAt,
                PostedByUserId = transaction.RecordedByUserId
            };
            transaction.JournalEntryId = journal.Id;
            _context.JournalEntries.Add(journal);
            _context.JournalLines.AddRange(
                new JournalLine
                {
                    JournalEntryId = journal.Id,
                    LedgerAccountId = accounts[payment.SettlementAccountCode].Id,
                    Description = "Payment settlement",
                    Debit = payment.Amount,
                    Currency = payment.Currency
                },
                new JournalLine
                {
                    JournalEntryId = journal.Id,
                    LedgerAccountId = accounts["1100"].Id,
                    Description = "Reduce client invoice balance",
                    Credit = payment.Amount,
                    Currency = payment.Currency
                });
            var receipt = new TransactionDocument
            {
                DocumentNumber = $"BGL{4001 + index:0000}RCT",
                Type = FinancialDocumentType.Receipt,
                FinancialTransactionId = transaction.Id,
                InvoiceId = invoice.Id,
                PaymentId = payment.Id,
                IssuedAt = payment.PaidAt,
                IssuedByUserId = transaction.RecordedByUserId,
                VerificationCode = $"RCT26{index + 1:0000000000}"
            };
            paymentTransactions.Add(transaction);
            receiptDocuments.Add(receipt);
        }
        _context.FinancialTransactions.AddRange(paymentTransactions);
        _context.TransactionDocuments.AddRange(receiptDocuments);
        _context.CreditNotes.AddRange(Enumerable.Range(0, 5).Select(index =>
        {
            var invoice = invoices[payments[index].InvoiceId];
            var originalAmount = Math.Max(1m, Math.Round(invoice.TotalAmount * 0.08m, 2));
            var usedAmount = index switch
            {
                1 => Math.Round(originalAmount / 2m, 2),
                2 => originalAmount,
                _ => 0m
            };
            return new CreditNote
            {
                BranchId = invoice.BranchId,
                CustomerId = invoice.CustomerId,
                OriginalInvoiceId = invoice.Id,
                CreditNoteNumber = $"BGL{4501 + index:0000}CRN",
                Currency = invoice.Currency,
                OriginalAmount = originalAmount,
                UsedAmount = usedAmount,
                State = index switch
                {
                    1 => CreditNoteState.CreditPartUsed,
                    2 => CreditNoteState.CreditUsed,
                    _ => CreditNoteState.CreditAvailable
                },
                Reason = "Local credit for a verified shipment service adjustment.",
                ApprovedByUserId = staffUsers[0].Id,
                IssuedAt = DateTime.UtcNow.AddDays(-(index + 2))
            };
        }));

        var bankAccounts = new[]
        {
            new BankAccount { Name = "UK Operating Account", BankName = "Barclays", MaskedAccountNumber = "**** 1048", Currency = "GBP", OpeningBalance = 125000m },
            new BankAccount { Name = "Lagos Operations Account", BankName = "Guaranty Trust Bank", MaskedAccountNumber = "**** 7842", Currency = "NGN", OpeningBalance = 8500000m },
            new BankAccount { Name = "China Sourcing Account", BankName = "Bank of China", MaskedAccountNumber = "**** 3921", Currency = "CNY", OpeningBalance = 420000m },
            new BankAccount { Name = "Card Settlement Account", BankName = "Stripe Treasury", MaskedAccountNumber = "**** 6670", Currency = "GBP", OpeningBalance = 38000m },
            new BankAccount { Name = "Petty Cash Control", BankName = "Internal Cash Account", MaskedAccountNumber = "CASH-001", Currency = "NGN", OpeningBalance = 650000m }
        };
        _context.BankAccounts.AddRange(bankAccounts);
        await _context.SaveChangesAsync(cancellationToken);

        var suppliers = await _context.Suppliers.OrderBy(x => x.SupplierNumber).Take(5)
            .ToListAsync(cancellationToken);
        var inventoryItems = await _context.InventoryItems.OrderBy(x => x.Sku)
            .ToListAsync(cancellationToken);
        var purchaseSpecs = new[]
        {
            (0, CompanyPurchaseCategory.Supplies, CompanyPurchaseType.GoodsInput, "UK-MAN", "GBP", 920m),
            (1, CompanyPurchaseCategory.LogisticsService, CompanyPurchaseType.Service, "NG-LOS", "NGN", 780000m),
            (2, CompanyPurchaseCategory.Equipment, CompanyPurchaseType.Asset, "CN-GZ", "CNY", 12600m),
            (3, CompanyPurchaseCategory.Maintenance, CompanyPurchaseType.Service, "NG-LOS", "NGN", 465000m),
            (4, CompanyPurchaseCategory.Regulatory, CompanyPurchaseType.Service, "UK-MAN", "GBP", 740m)
        };
        var purchases = purchaseSpecs.Select((spec, index) => new CompanyPurchase
        {
            PurchaseNumber = $"BGL{5001 + index:0000}PUR",
            SupplierId = suppliers[spec.Item1].Id,
            VendorName = suppliers[spec.Item1].Name,
            Category = spec.Item2,
            Type = spec.Item3,
            Status = CompanyPurchaseStatus.Paid,
            ReceivingStatus = spec.Item3 == CompanyPurchaseType.GoodsInput
                ? PurchaseReceivingStatus.Received
                : PurchaseReceivingStatus.NotRequired,
            BranchCode = spec.Item4,
            PurchaseDate = DateTime.UtcNow.Date.AddDays(-(index + 3)),
            DueDate = DateTime.UtcNow.Date.AddDays(10 - index),
            Currency = spec.Item5,
            Subtotal = spec.Item6,
            TaxAmount = 0,
            TotalAmount = spec.Item6,
            AmountPaid = spec.Item6,
            Description = index switch
            {
                0 => "Packaging material replenishment",
                1 => "Last-mile partner service payout",
                2 => "Warehouse weighing equipment",
                3 => "Cargo van scheduled maintenance",
                _ => "Annual cargo compliance filing"
            },
            SupplierReference = $"SUP-INV-26{index + 1:03}",
            RequestedByUserId = OperatorForBranch(staffUsers, spec.Item4).Id,
            ApprovedByUserId = staffUsers[0].Id,
            ApprovedAt = DateTime.UtcNow.AddDays(-(index + 2)),
            DecisionReason = "Approved operational requirement."
        }).ToList();
        _context.CompanyPurchases.AddRange(purchases);
        var purchaseLines = purchases.Select((purchase, index) => new CompanyPurchaseLine
        {
            CompanyPurchaseId = purchase.Id,
            InventoryItemId = index == 0 ? inventoryItems[index].Id : null,
            Description = purchase.Description,
            Quantity = index == 0 ? 100 : 1,
            QuantityReceived = index == 0 ? 100 : 0,
            UnitPrice = index == 0 ? purchase.TotalAmount / 100 : purchase.TotalAmount,
            TaxRate = 0,
            LineTotal = purchase.TotalAmount
        }).ToList();
        _context.CompanyPurchaseLines.AddRange(purchaseLines);
        await _context.SaveChangesAsync(cancellationToken);

        var purchaseTransactions = new List<FinancialTransaction>();
        var vouchers = new List<TransactionDocument>();
        var payouts = new List<PurchasePayout>();
        for (var index = 0; index < purchases.Count; index++)
        {
            var purchase = purchases[index];
            var branch = branches[purchase.BranchCode];
            var recorder = OperatorForBranch(staffUsers, purchase.BranchCode);
            var transaction = new FinancialTransaction
            {
                BranchId = branch.Id,
                TransactionNumber = $"BGL{6001 + index:0000}TXN",
                Direction = FinancialTransactionDirection.Debit,
                Type = FinancialTransactionType.Purchase,
                SourceType = nameof(CompanyPurchase),
                SourceId = purchase.Id,
                CounterpartyId = purchase.SupplierId,
                CounterpartyName = purchase.VendorName,
                Amount = purchase.TotalAmount,
                Currency = purchase.Currency,
                Method = index % 2 == 0 ? PaymentMethod.BankTransfer : PaymentMethod.Card,
                SettlementAccountCode = index % 2 == 0 ? "1010" : "1020",
                BankAccountId = bankAccounts[index % bankAccounts.Length].Id,
                ExternalReference = purchase.SupplierReference,
                OccurredAt = purchase.PurchaseDate.AddHours(12),
                BranchCode = purchase.BranchCode,
                RecordedByUserId = recorder.Id,
                FreightClassification = FreightClassification.Other
            };
            var journal = new JournalEntry
            {
                BranchId = branch.Id,
                Currency = purchase.Currency,
                JournalNumber = $"BGL-JNL-{6001 + index}",
                EntryDate = transaction.OccurredAt,
                Description = purchase.Description,
                SourceType = nameof(CompanyPurchase),
                SourceId = purchase.Id,
                Status = JournalStatus.Posted,
                PostedAt = transaction.OccurredAt,
                PostedByUserId = recorder.Id
            };
            transaction.JournalEntryId = journal.Id;
            _context.JournalEntries.Add(journal);
            _context.JournalLines.AddRange(
                new JournalLine
                {
                    JournalEntryId = journal.Id,
                    LedgerAccountId = accounts["5000"].Id,
                    Description = purchase.Description,
                    Debit = purchase.TotalAmount,
                    Currency = purchase.Currency
                },
                new JournalLine
                {
                    JournalEntryId = journal.Id,
                    LedgerAccountId = accounts[transaction.SettlementAccountCode].Id,
                    Description = "Purchase settlement",
                    Credit = purchase.TotalAmount,
                    Currency = purchase.Currency
                });
            var voucher = new TransactionDocument
            {
                DocumentNumber = $"BGL{7001 + index:0000}PV",
                Type = FinancialDocumentType.PaymentVoucher,
                FinancialTransactionId = transaction.Id,
                CompanyPurchaseId = purchase.Id,
                IssuedAt = transaction.OccurredAt,
                IssuedByUserId = recorder.Id,
                VerificationCode = $"PV26{index + 1:00000000000}"
            };
            var payout = new PurchasePayout
            {
                PayoutNumber = $"BGL{8001 + index:0000}PYO",
                CompanyPurchaseId = purchase.Id,
                FinancialTransactionId = transaction.Id,
                TransactionDocumentId = voucher.Id,
                Reference = $"PAYOUT-26{index + 1:04}",
                Amount = purchase.TotalAmount,
                Currency = purchase.Currency,
                Method = transaction.Method,
                BankAccountId = transaction.BankAccountId,
                SettlementAccountCode = transaction.SettlementAccountCode,
                PaidAt = transaction.OccurredAt,
                PaidByUserId = recorder.Id,
                Notes = "Paid against the matching approved company purchase."
            };
            voucher.PurchasePayoutId = payout.Id;
            purchaseTransactions.Add(transaction);
            vouchers.Add(voucher);
            payouts.Add(payout);
        }
        _context.FinancialTransactions.AddRange(purchaseTransactions);
        _context.TransactionDocuments.AddRange(vouchers);
        _context.PurchasePayouts.AddRange(payouts);

        _context.Expenses.AddRange(purchases.Select((purchase, index) => new Expense
        {
            ExpenseNumber = $"BGL-EXP-26{index + 1:03}",
            Category = purchase.Category.ToString(),
            Description = purchase.Description,
            Payee = purchase.VendorName,
            Amount = purchase.TotalAmount,
            Currency = purchase.Currency,
            IncurredAt = purchase.PurchaseDate,
            BranchCode = purchase.BranchCode,
            Status = ExpenseStatus.Paid,
            SubmittedByUserId = purchase.RequestedByUserId,
            ApprovedByUserId = staffUsers[0].Id,
            ApprovedAt = purchase.ApprovedAt
        }));

        var locations = await _context.InventoryLocations.OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);
        _context.InventoryTransactions.AddRange(inventoryItems.Select((item, index) =>
        {
            var location = locations[index % locations.Count];
            return new InventoryTransaction
            {
                TransactionNumber = $"BGL-INVTX-26{index + 1:03}",
                IdempotencyKey = $"local-opening-balance-{index + 1}",
                InventoryItemId = item.Id,
                Type = InventoryTransactionType.OpeningBalance,
                ToLocationId = location.Id,
                Quantity = 40 + index * 18,
                UnitCost = item.DefaultUnitCost,
                TotalCost = Math.Round((40 + index * 18) * item.DefaultUnitCost, 2),
                ReferenceType = "LocalSeed",
                Reason = "Opening stock for the integrated local company scenario.",
                OccurredAt = DateTime.UtcNow.AddDays(-(index + 10)),
                PerformedByUserId = OperatorForBranch(staffUsers,
                    branches.Values.Single(x => x.Id == location.BranchId).Code).Id
            };
        }));
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPricingWorkforceAndScheduleAsync(
        IReadOnlyDictionary<string, Branch> branches,
        IReadOnlyList<Customer> customers,
        IReadOnlyList<UserAccount> staffUsers,
        CancellationToken cancellationToken)
    {
        var priceSpecs = new[]
        {
            ("BOX-S", "Small shipping box", AdditionalServiceCategory.Packaging, PriceUnit.Each, 5m, "GBP", "UK-MAN"),
            ("BOX-M", "Medium shipping box", AdditionalServiceCategory.Packaging, PriceUnit.Each, 8m, "GBP", "UK-MAN"),
            ("BOX-L", "Large shipping box", AdditionalServiceCategory.Packaging, PriceUnit.Each, 12500m, "NGN", "NG-LOS"),
            ("BATTERY", "Battery handling", AdditionalServiceCategory.DangerousGoods, PriceUnit.Each, 18000m, "NGN", "NG-LOS"),
            ("NAFDAC", "NAFDAC processing", AdditionalServiceCategory.Regulatory, PriceUnit.FlatRate, 65000m, "NGN", "NG-LOS"),
            ("SAME-DAY", "Same day hub delivery", AdditionalServiceCategory.Delivery, PriceUnit.FlatRate, 35000m, "NGN", "NG-LOS"),
            ("STORAGE", "Warehouse storage", AdditionalServiceCategory.Storage, PriceUnit.PerBox, 18m, "CNY", "CN-GZ"),
            ("INSURE", "Cargo insurance", AdditionalServiceCategory.Insurance, PriceUnit.Percentage, 2.5m, "GBP", "UK-MAN")
        };
        _context.AdditionalServicePrices.AddRange(priceSpecs.Select(spec => new AdditionalServicePrice
        {
            Code = spec.Item1,
            Name = spec.Item2,
            Description = $"{spec.Item2} managed from the dashboard.",
            Category = spec.Item3,
            Unit = spec.Item4,
            Amount = spec.Item5,
            Currency = spec.Item6,
            BranchCode = spec.Item7,
            EffectiveFrom = DateTime.UtcNow.Date.AddDays(-30)
        }));

        var clients = customers.Take(5).ToList();
        _context.ScheduledPickups.AddRange(clients.Select((client, index) =>
        {
            var branch = branches.Values.Single(x => x.Id == client.BranchId);
            var price = branch.CountryCode == "NG" ? 25000m + index * 2500 :
                branch.CountryCode == "CN" ? 120m + index * 20 : 35m + index * 5;
            var currency = branch.CountryCode == "NG" ? "NGN" :
                branch.CountryCode == "CN" ? "CNY" : "GBP";
            return new ScheduledPickup
            {
                BranchId = branch.Id,
                PickupNumber = $"BGL-PU-26{index + 1:03}",
                ClientId = client.Id,
                ContactName = client.FullName,
                Phone = client.Phone,
                Email = client.Email,
                Address = $"{50 + index} Collection Street",
                City = City(branch.Code),
                ScheduledAt = DateTime.UtcNow.Date.AddDays(index + 1).AddHours(10),
                TimeZone = branch.TimeZone,
                GoodsType = index % 2 == 0 ? ShipmentGoodsType.NonFragile : ShipmentGoodsType.Fragile,
                Contents = CargoDescriptions[index],
                HandlingNote = "Call the customer 30 minutes before arrival.",
                ExpectedPieceCount = index + 1,
                ActualWeightKg = 8 + index * 3,
                ChargeableWeightKg = 9 + index * 3,
                BoxSizesJson = JsonSerializer.Serialize(new[]
                {
                    new { size = index % 2 == 0 ? "Medium" : "Large", quantity = index + 1 }
                }),
                Price = price,
                Currency = currency,
                PaymentMethod = index % 2 == 0 ? Domain.Enums.PaymentMethod.BankTransfer : Domain.Enums.PaymentMethod.Cash,
                PaymentReference = $"PU-PAY-26{index + 1:03}",
                PaymentState = index < 3 ? PickupPaymentState.Paid : PickupPaymentState.Unpaid,
                AmountPaid = index < 3 ? price : 0,
                State = index == 0 ? PickupState.Collected :
                    index < 3 ? PickupState.DriverAssigned : PickupState.Booked,
                AssignedDriverUserId = staffUsers[6].Id,
                AssignedTeam = "Local pickup team",
                CollectedAt = index == 0 ? DateTime.UtcNow.AddHours(-2) : null
            };
        }));

        var leads = await _context.SalesLeads.OrderBy(x => x.CreatedAt).Take(8)
            .ToListAsync(cancellationToken);
        _context.SalesActivities.AddRange(leads.Select((lead, index) => new SalesActivity
        {
            SalesLeadId = lead.Id,
            StaffUserId = staffUsers[index % staffUsers.Count].Id,
            ActivityType = index % 2 == 0 ? "Quotation" : "ClientCall",
            Summary = "Recorded a real operational follow-up for the coherent seed scenario.",
            OccurredAt = DateTime.UtcNow.AddDays(-(index + 1)),
            FollowUpAt = DateTime.UtcNow.AddDays(index + 1)
        }));
        var monthStart = new DateTime(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        _context.SalesTargets.AddRange(staffUsers.Take(5).Select((user, index) => new SalesTarget
        {
            StaffUserId = user.Id,
            PeriodStart = monthStart,
            PeriodEnd = monthStart.AddMonths(1).AddTicks(-1),
            RevenueTarget = 25000 + index * 5000,
            WonLeadTarget = 5 + index,
            Currency = "GBP"
        }));
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedContactsCommunicationsAndAuditAsync(
        IReadOnlyDictionary<string, Branch> branches,
        IReadOnlyList<Customer> customers,
        IReadOnlyList<UserAccount> staffUsers,
        SeededManifestScenario scenario,
        CancellationToken cancellationToken)
    {
        var suppliers = await _context.Suppliers.OrderBy(x => x.SupplierNumber).Take(5)
            .ToListAsync(cancellationToken);
        var additionalContacts = new List<Contact>();
        for (var index = 0; index < 5; index++)
        {
            additionalContacts.Add(new Contact
            {
                Type = ContactType.Supplier,
                FullName = $"{suppliers[index].Name} Account Team",
                CompanyName = suppliers[index].Name,
                JobTitle = "Supplier Account Manager",
                Email = $"accounts{index + 1}@supplier.bigsamgl.test",
                Phone = $"+447700910{index + 1:000}",
                CountryCode = suppliers[index].CountryCode,
                Tags = "Supplier,Approved",
                SupplierId = suppliers[index].Id,
                PreferredBranchId = branches.Values.ElementAt(index % branches.Count).Id,
                OwnerUserId = staffUsers[0].Id,
                IsPrimary = true
            });
            additionalContacts.Add(new Contact
            {
                Type = ContactType.Director,
                FullName = $"Director Test {index + 1}",
                CompanyName = "BiGSAM Global Logistics",
                JobTitle = index == 0 ? "Managing Director" : "Board Director",
                Email = $"director{index + 1}@bigsamgl.test",
                Phone = $"+447700920{index + 1:000}",
                CountryCode = "GB",
                Tags = "Director,Governance",
                PreferredBranchId = branches["UK-MAN"].Id,
                OwnerUserId = staffUsers[0].Id,
                IsPrimary = index == 0
            });
            additionalContacts.Add(new Contact
            {
                Type = ContactType.Shareholder,
                FullName = $"Shareholder Test {index + 1}",
                CompanyName = "BiGSAM Global Logistics",
                JobTitle = "Shareholder",
                Email = $"shareholder{index + 1}@bigsamgl.test",
                Phone = $"+447700930{index + 1:000}",
                CountryCode = "GB",
                Tags = "Shareholder,Governance",
                PreferredBranchId = branches["UK-MAN"].Id,
                OwnerUserId = staffUsers[0].Id
            });
        }
        _context.Contacts.AddRange(additionalContacts);
        await _context.SaveChangesAsync(cancellationToken);

        _context.ContactInteractions.AddRange(additionalContacts.Take(15).Select((contact, index) =>
            new ContactInteraction
            {
                ContactId = contact.Id,
                Type = index % 3 == 0 ? ContactInteractionType.Meeting :
                    index % 3 == 1 ? ContactInteractionType.Email : ContactInteractionType.PhoneCall,
                Subject = "Company relationship review",
                Details = "Verified contact details and recorded the agreed company action.",
                OccurredAt = DateTime.UtcNow.AddDays(-(index + 1)),
                StaffUserId = staffUsers[index % staffUsers.Count].Id
            }));

        var broadcasts = Enumerable.Range(0, 5).Select(index =>
        {
            var sent = index < 3;
            return new EmailBroadcast
            {
                CampaignNumber = $"BGL-EML-26{index + 1:03}",
                Name = index switch
                {
                    0 => "UK to Nigeria sea departure",
                    1 => "China air cargo update",
                    2 => "Warehouse collection reminder",
                    3 => "Service price notice",
                    _ => "Client care announcement"
                },
                Subject = $"BiGSAM service update {index + 1}",
                Body = "This local broadcast checks the approved email workflow and recipient history.",
                ReplyTo = "operations@bigsamgl.com",
                Purpose = index < 3 ? EmailBroadcastPurpose.Operational : EmailBroadcastPurpose.General,
                Status = sent ? EmailBroadcastStatus.Sent : EmailBroadcastStatus.Draft,
                AudienceSelectionJson = JsonSerializer.Serialize(new { clientIds = customers.Take(5).Select(x => x.Id) }),
                CreatedByUserId = staffUsers[0].Id,
                QueuedAt = sent ? DateTime.UtcNow.AddDays(-(index + 2)) : null,
                StartedAt = sent ? DateTime.UtcNow.AddDays(-(index + 2)).AddMinutes(1) : null,
                CompletedAt = sent ? DateTime.UtcNow.AddDays(-(index + 2)).AddMinutes(3) : null,
                TotalRecipients = 5,
                PendingRecipients = sent ? 0 : 5,
                SentRecipients = sent ? 5 : 0
            };
        }).ToList();
        _context.EmailBroadcasts.AddRange(broadcasts);
        _context.EmailBroadcastRecipients.AddRange(broadcasts.SelectMany((broadcast, broadcastIndex) =>
            customers.Take(5).Select(client => new EmailBroadcastRecipient
            {
                EmailBroadcastId = broadcast.Id,
                SourceType = EmailRecipientSourceType.Client,
                SourceId = client.Id,
                RecipientName = client.FullName,
                Email = client.Email!,
                NormalizedEmail = client.Email!.Trim().ToUpperInvariant(),
                Status = broadcast.Status == EmailBroadcastStatus.Sent
                    ? EmailBroadcastRecipientStatus.Sent
                    : EmailBroadcastRecipientStatus.Pending,
                AttemptCount = broadcast.Status == EmailBroadcastStatus.Sent ? 1 : 0,
                LastAttemptAt = broadcast.Status == EmailBroadcastStatus.Sent
                    ? DateTime.UtcNow.AddDays(-(broadcastIndex + 2))
                    : null,
                SentAt = broadcast.Status == EmailBroadcastStatus.Sent
                    ? DateTime.UtcNow.AddDays(-(broadcastIndex + 2))
                    : null,
                ProviderReference = broadcast.Status == EmailBroadcastStatus.Sent
                    ? $"LOCAL-MAIL-{broadcastIndex + 1}-{client.ClientNumber}"
                    : null
            })));

        var loyaltyAccounts = customers.Select((customer, index) => new CustomerLoyaltyAccount
        {
            CustomerId = customer.Id,
            AvailablePoints = index < 5 ? 25 + index * 10 : 0,
            LifetimeEarnedPoints = index < 5 ? 25 + index * 10 : 0
        }).ToList();
        _context.CustomerLoyaltyAccounts.AddRange(loyaltyAccounts);
        _context.LoyaltyPointTransactions.AddRange(loyaltyAccounts.Take(5).Select((account, index) =>
            new LoyaltyPointTransaction
            {
                CustomerLoyaltyAccountId = account.Id,
                CustomerId = account.CustomerId,
                InvoiceId = index < scenario.Entries.Count ? scenario.Entries[index].InvoiceId : null,
                Type = LoyaltyPointTransactionType.ReferralShipmentAward,
                Points = 25 + index * 10,
                Description = "Seeded loyalty award linked to an invoiced shipment.",
                OccurredAt = DateTime.UtcNow.AddDays(-(index + 1))
            }));

        var profiles = await _context.StaffProfiles.OrderBy(x => x.EmployeeNumber)
            .ToListAsync(cancellationToken);
        var locations = profiles.Take(5).Select((profile, index) =>
        {
            var branch = branches[profile.BranchCode];
            var coordinate = profile.BranchCode switch
            {
                "UK-MAN" => (53.480800m, -2.242600m),
                "CN-GZ" => (23.129100m, 113.264400m),
                _ => (6.455000m, 3.384100m)
            };
            return new StaffLocationPing
            {
                UserAccountId = profile.UserAccountId,
                StaffProfileId = profile.Id,
                StaffName = staffUsers.Single(x => x.Id == profile.UserAccountId).FullName,
                EmployeeNumber = profile.EmployeeNumber,
                BranchCode = branch.Code,
                Latitude = coordinate.Item1,
                Longitude = coordinate.Item2,
                AccuracyMeters = 18 + index,
                CapturedAt = DateTime.UtcNow.AddMinutes(-(index + 1)),
                ReceivedAt = DateTime.UtcNow.AddMinutes(-index),
                Source = StaffLocationSource.BrowserGps,
                ConsentConfirmed = true,
                IpAddress = "127.0.0.1",
                DeviceId = $"local-device-{index + 1}"
            };
        }).ToList();
        _context.StaffLocationPings.AddRange(locations);
        _context.StaffActivityLogs.AddRange(locations.Select((location, index) =>
        {
            var user = staffUsers.Single(x => x.Id == location.UserAccountId);
            var profile = profiles.Single(x => x.Id == location.StaffProfileId);
            return new StaffActivityLog
            {
                UserAccountId = user.Id,
                StaffProfileId = profile.Id,
                StaffName = user.FullName,
                StaffEmail = user.Email,
                Role = user.Role.ToString(),
                EmployeeNumber = profile.EmployeeNumber,
                BranchCode = profile.BranchCode,
                ActivityType = "SeededApiAction",
                HttpMethod = index % 2 == 0 ? "POST" : "PATCH",
                RouteTemplate = "/api/operations/manifests/{id}",
                Path = $"/api/operations/manifests/{scenario.Manifests[index].Id}",
                Controller = "Manifests",
                ActionName = index % 2 == 0 ? "Create" : "Update",
                StatusCode = 200,
                Outcome = StaffActivityOutcome.Succeeded,
                DurationMilliseconds = 45 + index * 7,
                IpAddress = location.IpAddress,
                UserAgent = "BiGSAM local seed",
                DeviceId = location.DeviceId,
                CorrelationId = $"seed-correlation-{index + 1}",
                SessionId = $"seed-session-{index + 1}",
                LocationPingId = location.Id,
                LocationSource = StaffLocationSource.BrowserGps,
                LocationFreshness = StaffLocationFreshness.Current,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                LocationAccuracyMeters = location.AccuracyMeters,
                LocationCapturedAt = location.CapturedAt,
                OccurredAt = DateTime.UtcNow.AddMinutes(-index)
            };
        }));
        _context.AuditLogs.AddRange(Enumerable.Range(0, 8).Select(index => new AuditLog
        {
            ActorId = staffUsers[index % staffUsers.Count].Id.ToString(),
            Action = index % 2 == 0 ? "Create" : "Update",
            EntityType = index < 5 ? nameof(OperationalManifest) : nameof(CompanyPurchase),
            EntityId = index < 5
                ? scenario.Manifests[index].Id.ToString()
                : $"purchase-seed-{index}",
            ChangesJson = JsonSerializer.Serialize(new
            {
                reason = "Coherent company-wide local seed",
                branch = branches.Values.ElementAt(index % branches.Count).Code
            }),
            IpAddress = "127.0.0.1"
        }));
        _context.StaffNotifications.AddRange(staffUsers.Take(5).Select((user, index) => new StaffNotification
        {
            UserAccountId = user.Id,
            BranchId = index == 0 ? null : branches.Values.ElementAt(index % branches.Count).Id,
            Category = index % 2 == 0 ? "Operations" : "Finance",
            Title = $"Local control-centre notification {index + 1}",
            Message = "This notification is linked to the coherent testing dataset.",
            Path = index % 2 == 0 ? "/operations/manifests" : "/finance/transactions",
            IsUrgent = index == 4
        }));
        _context.UserNotificationPreferences.AddRange(staffUsers.Select(user =>
            new UserNotificationPreference { UserAccountId = user.Id }));
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static UserAccount OperatorForBranch(
        IReadOnlyList<UserAccount> staffUsers,
        string branchCode) =>
        branchCode switch
        {
            "NG-LOS" => staffUsers.Single(x => x.Email == "operator.lagos@bigsamgl.test"),
            "CN-GZ" => staffUsers.Single(x => x.Email == "operator.guangzhou@bigsamgl.test"),
            _ => staffUsers.Single(x => x.Email == "superadmin.uk@bigsamgl.test")
        };

    private static string City(string branchCode) => branchCode switch
    {
        "UK-MAN" => "Manchester",
        "NG-LOS" => "Lagos",
        "CN-GZ" => "Guangzhou",
        _ => branchCode
    };

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static FreightClassification FreightForInvoice(Invoice invoice) =>
        invoice.Notes?.Contains("SEA", StringComparison.OrdinalIgnoreCase) == true
            ? FreightClassification.Sea
            : FreightClassification.Air;

    private sealed record SeededManifestScenario(
        IReadOnlyList<CustomerShipment> CustomerShipments,
        IReadOnlyList<OperationalManifest> Manifests,
        IReadOnlyList<OperationalManifestEntry> Entries,
        IReadOnlyList<ManifestTrackingConnection> Connections,
        IReadOnlyList<CargoItemIssue> Issues);
}

internal static class InvoiceSeedExtensions
{
    public static string InvoiceNumericFragment(this Invoice invoice) =>
        new(invoice.InvoiceNumber.Where(char.IsDigit).ToArray());
}
