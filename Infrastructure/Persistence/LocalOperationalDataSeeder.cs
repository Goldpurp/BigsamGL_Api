using Application.Common;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence;

/// <summary>
/// Creates a connected, local-only operating dataset. The production startup
/// validator rejects this seed flag, so these records can never be created by a
/// production process.
/// </summary>
public sealed partial class LocalOperationalDataSeeder
{
    private const string SeedMarker = "amina.bello@bigsamgl.test";
    public const string LocalStaffPasswordSetting = "LocalSeed:StaffPassword";
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public LocalOperationalDataSeeder(
        ApplicationDbContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var mainEmail = (_configuration["LocalSeed:CustomerEmail"] ?? SeedMarker)
            .Trim().ToLowerInvariant();
        if (await _context.Customers.AnyAsync(
                x => x.NormalizedEmail == ClientIdentityNormalizer.Email(mainEmail),
                cancellationToken))
        {
            return;
        }

        var executionStrategy = _context.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            // A retry must begin with a clean unit of work and recheck the marker.
            // The transaction then keeps the whole connected scenario all-or-nothing.
            _context.ChangeTracker.Clear();
            if (await _context.Customers.AnyAsync(
                    x => x.NormalizedEmail == ClientIdentityNormalizer.Email(mainEmail),
                    cancellationToken))
            {
                return;
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);

            var customerPassword = RequiredPassword("LocalSeed:CustomerPassword");
            var staffPassword = RequiredPassword(LocalStaffPasswordSetting);
            var branches = await EnsureBranchesAsync(cancellationToken);
            var routes = await EnsureRoutesAndRatesAsync(branches, cancellationToken);

        var customerSpecs = new[]
        {
            ("Amina Bello", mainEmail, "+2348010001001", "NG", "Amina Bello Trading", ClientAccountType.Business),
            ("Daniel Okafor", "daniel.okafor@bigsamgl.test", "+2348010001002", "NG", "", ClientAccountType.Individual),
            ("Grace Mensah", "grace.mensah@bigsamgl.test", "+233501000100", "GH", "Coastline Retail", ClientAccountType.Business),
            ("Oluwaseun Adeyemi", "seun.adeyemi@bigsamgl.test", "+2348010001004", "NG", "", ClientAccountType.Individual),
            ("Chen Wei", "chen.wei@bigsamgl.test", "+8613800100105", "CN", "Pearl River Supply", ClientAccountType.PartnerAgency),
            ("Sarah Williams", "sarah.williams@bigsamgl.test", "+447700900106", "GB", "Northline Stores", ClientAccountType.Business),
            ("Fatima Ibrahim", "fatima.ibrahim@bigsamgl.test", "+2348010001007", "NG", "", ClientAccountType.Individual),
            ("Kwame Boateng", "kwame.boateng@bigsamgl.test", "+233501000108", "GH", "Accra Market Link", ClientAccountType.Business)
        };
        var customers = customerSpecs.Select((spec, index) => new Customer
        {
            FullName = spec.Item1,
            Email = spec.Item2,
            Phone = spec.Item3,
            CountryCode = spec.Item4,
            CompanyName = string.IsNullOrWhiteSpace(spec.Item5) ? null : spec.Item5,
            AccountType = spec.Item6,
            IsBusiness = spec.Item6 != ClientAccountType.Individual,
            ClientNumber = $"BGL-CL-{2601 + index:0000}",
            BranchId = index == 4 ? branches["CN-GZ"].Id :
                index == 5 ? branches["UK-MAN"].Id : branches["NG-LOS"].Id,
            WalletCurrency = index == 4 ? "CNY" : index == 5 ? "GBP" : "NGN",
            WalletBalance = new[] { 185000m, 42000m, 2150m, 78000m, 9800m, 640m, 33500m, 1875m }[index]
        }).ToList();
        _context.Customers.AddRange(customers);
        _context.UserAccounts.AddRange(customers.Select((customer, index) => new UserAccount
        {
            FullName = customer.FullName,
            Email = customer.Email!,
            PasswordHash = PasswordService.Hash(index == 0 ? customerPassword : staffPassword),
            Role = UserRole.Customer,
            Status = AccountStatus.Active,
            CustomerId = customer.Id,
            EmailVerifiedAt = DateTime.UtcNow
        }));
        _context.NotificationPreferences.AddRange(customers.Select(customer =>
            new NotificationPreference
            {
                CustomerId = customer.Id,
                EmailEnabled = true,
                SmsEnabled = true,
                WhatsAppEnabled = customer.CountryCode is "NG" or "GH",
                OperationalUpdates = true,
                MarketingUpdates = false,
                ConsentUpdatedAt = DateTime.UtcNow
            }));

        var staffSpecs = new[]
        {
            ("Olivia Thompson", "superadmin.uk@bigsamgl.test", UserRole.SuperAdmin, "Group Super Administrator", "Executive", "UK-MAN"),
            ("Ifeoma Nwosu", "operator.lagos@bigsamgl.test", UserRole.OperationsManager, "Lagos Operations Manager", "Operations", "NG-LOS"),
            ("Lin Xia", "operator.guangzhou@bigsamgl.test", UserRole.OperationsManager, "Guangzhou Operations Manager", "Operations", "CN-GZ"),
            ("Michael Grant", "sales.uk@bigsamgl.test", UserRole.SalesManager, "UK Sales Manager", "Sales", "UK-MAN"),
            ("Tunde Akinola", "finance.lagos@bigsamgl.test", UserRole.FinanceManager, "Finance Manager", "Finance", "NG-LOS"),
            ("Mei Chen", "warehouse.china@bigsamgl.test", UserRole.Staff, "Warehouse Officer", "Warehouse", "CN-GZ"),
            ("Bola Yusuf", "delivery.lagos@bigsamgl.test", UserRole.Staff, "Delivery Coordinator", "Operations", "NG-LOS"),
            ("Adaeze Eze", "admin.uk@bigsamgl.test", UserRole.Administrator, "Office Administrator", "Administration", "UK-MAN")
        };
        var staffUsers = staffSpecs.Select(spec => new UserAccount
        {
            FullName = spec.Item1,
            Email = spec.Item2,
            PasswordHash = PasswordService.Hash(staffPassword),
            Role = spec.Item3,
            Status = AccountStatus.Active,
            EmailVerifiedAt = DateTime.UtcNow
        }).ToList();
        _context.UserAccounts.AddRange(staffUsers);
        _context.StaffProfiles.AddRange(staffUsers.Select((user, index) => new StaffProfile
        {
            UserAccountId = user.Id,
            EmployeeNumber = $"BGL-STF-{101 + index:000}",
            JobTitle = staffSpecs[index].Item4,
            Department = staffSpecs[index].Item5,
            BranchCode = staffSpecs[index].Item6,
            HireDate = DateTime.UtcNow.Date.AddMonths(-(16 + index)),
            Status = StaffStatus.Active
        }));
        _context.StaffBranchMemberships.AddRange(staffUsers.Select((user, index) =>
            new StaffBranchMembership
            {
                UserAccountId = user.Id,
                BranchId = branches[staffSpecs[index].Item6].Id,
                IsPrimary = true
            }));
        _context.StaffBranchMemberships.AddRange(
            new StaffBranchMembership
            {
                UserAccountId = staffUsers[0].Id,
                BranchId = branches["NG-LOS"].Id,
                IsPrimary = false
            },
            new StaffBranchMembership
            {
                UserAccountId = staffUsers[0].Id,
                BranchId = branches["CN-GZ"].Id,
                IsPrimary = false
            });
        await _context.SaveChangesAsync(cancellationToken);

        var addresses = customers.Select((customer, index) => new CustomerAddress
        {
            CustomerId = customer.Id,
            Type = AddressType.Collection,
            ContactName = customer.FullName,
            Phone = customer.Phone,
            Line1 = index switch
            {
                4 => "88 Baiyun Avenue",
                5 => "24 Deansgate",
                _ => $"{14 + index} Admiralty Way"
            },
            City = index == 4 ? "Guangzhou" : index == 5 ? "Manchester" : "Lagos",
            State = index == 4 ? "Guangdong" : index == 5 ? "Greater Manchester" : "Lagos",
            PostalCode = index == 4 ? "510000" : index == 5 ? "M3 2BY" : $"10124{index}",
            CountryCode = customer.CountryCode,
            IsDefault = true
        }).ToList();
        _context.CustomerAddresses.AddRange(addresses);

        var leadStages = new[]
        {
            LeadStage.New, LeadStage.Contacted, LeadStage.Qualified, LeadStage.Quoted,
            LeadStage.Won, LeadStage.Contacted, LeadStage.Qualified, LeadStage.Quoted
        };
        var leads = customers.Select((customer, index) => new SalesLead
        {
            CustomerId = customer.Id,
            OwnerUserId = staffUsers[index % staffUsers.Count].Id,
            ContactName = customer.FullName,
            ContactEmail = customer.Email!,
            ContactPhone = customer.Phone,
            CompanyName = customer.CompanyName,
            ServiceInterest = index % 3 == 0 ? "Sea freight" : "Air freight",
            Origin = routes[index].Name.Split(" to ")[0],
            Destination = routes[index].Name.Split(" to ")[1].Replace(" Air Freight", "").Replace(" Sea Freight", ""),
            Stage = leadStages[index],
            Priority = index is 0 or 4 ? LeadPriority.High : LeadPriority.Normal,
            Source = index % 2 == 0 ? "Website" : "Client referral",
            EstimatedValue = 450m + index * 175m,
            Currency = "GBP",
            NextActionAt = DateTime.UtcNow.AddDays(index + 1),
            LastContactedAt = DateTime.UtcNow.AddDays(-index)
        }).ToList();
        _context.SalesLeads.AddRange(leads);

        var quoteStatuses = new[]
        {
            QuoteStatus.Accepted, QuoteStatus.Sent, QuoteStatus.Accepted, QuoteStatus.Converted,
            QuoteStatus.Sent, QuoteStatus.Accepted, QuoteStatus.Draft, QuoteStatus.Sent
        };
        var quotes = Enumerable.Range(0, 8).Select(index =>
        {
            var baseAmount = 180m + index * 72m;
            return new ShippingQuote
            {
                QuoteNumber = $"BGL-QT-260{index + 1}",
                CustomerId = customers[index].Id,
                ServiceRouteId = routes[index].Id,
                SalesLeadId = leads[index].Id,
                Status = quoteStatuses[index],
                ContactName = customers[index].FullName,
                ContactEmail = customers[index].Email!,
                ContactPhone = customers[index].Phone,
                CargoDescription = CargoDescriptions[index],
                CargoCategory = CargoCategories[index],
                ActualWeightKg = 12m + index * 8m,
                VolumetricWeightKg = 10m + index * 7m,
                BillableWeightKg = 12m + index * 8m,
                BaseAmount = baseAmount,
                FuelSurchargeAmount = Math.Round(baseAmount * .08m, 2),
                CustomsAmount = Math.Round(baseAmount * .02m, 2),
                TotalAmount = Math.Round(baseAmount * 1.10m, 2),
                Currency = "GBP",
                ExpiresAt = DateTime.UtcNow.Date.AddDays(14 + index)
            };
        }).ToList();
        _context.ShippingQuotes.AddRange(quotes);
        await _context.SaveChangesAsync(cancellationToken);

        var statuses = new[]
        {
            CargoStatus.InTransit, CargoStatus.ReceivedAtWarehouse, CargoStatus.ArrivedAtDestinationHub,
            CargoStatus.OutForDelivery, CargoStatus.Delivered, CargoStatus.Delivered,
            CargoStatus.AssignedToBatch, CargoStatus.InTransit
        };
        var cargo = Enumerable.Range(0, 8).Select(index => new CargoItem
        {
            CustomerId = customers[index].Id,
            TrackingNumber = $"BGL-2607-{index + 1:0000}",
            MarkerCode = $"LOS-0724-{index + 1:00}",
            Description = CargoDescriptions[index],
            ActualWeightKg = 12m + index * 8m,
            LengthCm = 38 + index * 2,
            WidthCm = 30 + index,
            HeightCm = 24 + index,
            VolumetricWeightKg = Math.Round((38m + index * 2) * (30m + index) * (24m + index) / 5000m, 2),
            DeclaredValueAmount = 320m + index * 140m,
            DeclaredValueCurrency = "GBP",
            CustomsCategory = CargoCategories[index],
            Status = statuses[index],
            ShippingCostAmount = quotes[index].TotalAmount,
            ShippingCostCurrency = "GBP",
            AssignedTransportMode = routes[index].Mode
        }).ToList();
        _context.CargoItems.AddRange(cargo);

        var invoices = cargo.Select((item, index) =>
        {
            var isPaid = index is 4 or 5;
            var isPartPaid = index is 0 or 2 or 7;
            var issuer = branches.Values.Single(x => x.Id == routes[index].OriginBranchId);
            var currency = issuer.CountryCode switch
            {
                "NG" => "NGN",
                "CN" => "CNY",
                _ => "GBP"
            };
            var conversion = currency == "NGN" ? 2200m : currency == "CNY" ? 9.3m : 1m;
            var total = Math.Round(item.ShippingCostAmount * conversion, 2);
            var paid = isPaid ? total : isPartPaid ? Math.Round(total * .5m, 2) : 0m;
            return new Invoice
            {
                BranchId = issuer.Id,
                CustomerId = customers[index].Id,
                CustomerName = customers[index].FullName,
                InvoiceNumber = $"BGL{1001 + index:0000}INV",
                IssuerBranchCode = issuer.Code,
                IssueDate = DateTime.UtcNow.Date.AddDays(-(index + 2)),
                DueDate = DateTime.UtcNow.Date.AddDays(12 - index),
                Status = isPaid ? InvoiceStatus.Paid :
                    isPartPaid ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Issued,
                PaymentStatus = isPaid ? InvoicePaymentStatus.Paid :
                    isPartPaid ? InvoicePaymentStatus.PartiallyPaid : InvoicePaymentStatus.Unpaid,
                PreferredPaymentMethod = PaymentMethod.BankTransfer,
                Currency = currency,
                Subtotal = total,
                TotalAmount = total,
                AmountPaid = paid,
                VerificationCode = $"LOCAL26{index + 1:00000000}",
                Notes = $"Shipping charge for {item.TrackingNumber} via {routes[index].Mode}"
            };
        }).ToList();
        _context.Invoices.AddRange(invoices);
        _context.InvoiceLines.AddRange(invoices.Select((invoice, index) => new InvoiceLine
        {
            InvoiceId = invoice.Id,
            Description = $"{routes[index].Name}: {CargoDescriptions[index]}",
            Quantity = 1,
            Unit = "shipment",
            UnitPrice = invoice.TotalAmount,
            TaxRate = 0,
            LineTotal = invoice.TotalAmount
        }));
        for (var index = 0; index < cargo.Count; index++)
            cargo[index].InvoiceId = invoices[index].Id;

        _context.Payments.AddRange(new[] { 0, 2, 4, 5, 7 }.Select((cargoIndex, paymentIndex) =>
            new Payment
            {
                BranchId = invoices[cargoIndex].BranchId,
                InvoiceId = invoices[cargoIndex].Id,
                CustomerId = customers[cargoIndex].Id,
                Reference = $"BGL-PAY-260{paymentIndex + 1}",
                Amount = invoices[cargoIndex].AmountPaid,
                Currency = invoices[cargoIndex].Currency,
                Method = paymentIndex % 2 == 0 ? PaymentMethod.BankTransfer : PaymentMethod.Card,
                SettlementAccountCode = paymentIndex % 2 == 0 ? "1010" : "1020",
                Status = PaymentStatus.Confirmed,
                PaidAt = DateTime.UtcNow.AddDays(-(paymentIndex + 1)),
                Notes = $"Payment received for {invoices[cargoIndex].InvoiceNumber}"
            }));

        AddMilestones(cargo, routes, branches);
        await _context.SaveChangesAsync(cancellationToken);

        await SeedDirectoriesInventoryAndScheduleAsync(
            branches, customers, staffUsers, leads, cancellationToken);
            await SeedControlCentreScenarioAsync(
                branches, routes, customers, staffUsers, cargo, invoices, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private async Task<Dictionary<string, Branch>> EnsureBranchesAsync(
        CancellationToken cancellationToken)
    {
        var specs = new[]
        {
            ("UK-MAN", "Manchester Warehouse", "GB", "Europe/London", "1 Kay St, Manchester M11 2DX"),
            ("NG-LOS", "Lagos Cargo Hub", "NG", "Africa/Lagos", "Apapa, Lagos, Nigeria"),
            ("CN-GZ", "Guangzhou Sourcing Hub", "CN", "Asia/Shanghai", "Baiyun District, Guangzhou, China")
        };
        var existing = await _context.Branches.ToDictionaryAsync(x => x.Code, cancellationToken);
        foreach (var spec in specs.Where(spec => !existing.ContainsKey(spec.Item1)))
        {
            var branch = new Branch
            {
                Code = spec.Item1, Name = spec.Item2, CountryCode = spec.Item3,
                TimeZone = spec.Item4, Address = spec.Item5
            };
            _context.Branches.Add(branch);
            _context.BranchControls.Add(new BranchControl { BranchId = branch.Id });
            existing[branch.Code] = branch;
        }
        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    private async Task<List<ServiceRoute>> EnsureRoutesAndRatesAsync(
        IReadOnlyDictionary<string, Branch> branches,
        CancellationToken cancellationToken)
    {
        var specs = new[]
        {
            ("UK-NG-SEA", "UK to Nigeria Sea Freight", "UK-MAN", "NG-LOS", TransportMode.Sea, 35, 49, 1.50m),
            ("UK-NG-AIR", "UK to Nigeria Air Freight", "UK-MAN", "NG-LOS", TransportMode.Air, 5, 10, 4.50m),
            ("CN-NG-AIR", "China to Nigeria Air Freight", "CN-GZ", "NG-LOS", TransportMode.Air, 7, 14, 5.25m),
            ("NG-UK-AIR", "Nigeria to UK Air Freight", "NG-LOS", "UK-MAN", TransportMode.Air, 5, 10, 4.80m),
            ("NG-UK-SEA", "Nigeria to UK Sea Freight", "NG-LOS", "UK-MAN", TransportMode.Sea, 35, 49, 1.65m),
            ("CN-NG-SEA", "China to Nigeria Sea Freight", "CN-GZ", "NG-LOS", TransportMode.Sea, 28, 42, 1.40m),
            ("NG-CN-AIR", "Nigeria to China Air Freight", "NG-LOS", "CN-GZ", TransportMode.Air, 7, 14, 5.10m),
            ("UK-CN-SEA", "UK to China Sea Freight", "UK-MAN", "CN-GZ", TransportMode.Sea, 30, 45, 1.70m)
        };
        var existing = await _context.ServiceRoutes.ToDictionaryAsync(x => x.Code, cancellationToken);
        foreach (var spec in specs.Where(spec => !existing.ContainsKey(spec.Item1)))
        {
            var route = new ServiceRoute
            {
                Code = spec.Item1,
                Name = spec.Item2,
                OriginBranchId = branches[spec.Item3].Id,
                DestinationBranchId = branches[spec.Item4].Id,
                DestinationCountryCode = branches[spec.Item4].CountryCode,
                DestinationCity = spec.Item4 switch
                {
                    "UK-MAN" => "Manchester",
                    "NG-LOS" => "Lagos", "CN-GZ" => "Guangzhou",
                    _ => branches[spec.Item4].Name
                },
                EstimatedTimeline = $"{spec.Item6}–{spec.Item7} days",
                Mode = spec.Item5,
                TransitDaysMin = spec.Item6,
                TransitDaysMax = spec.Item7,
                WeeklyDepartureDay = spec.Item5 == TransportMode.Sea ? DayOfWeek.Tuesday : DayOfWeek.Thursday,
                VolumetricDivisor = spec.Item5 == TransportMode.Sea ? 1_000_000 : 5_000
            };
            _context.ServiceRoutes.Add(route);
            existing[route.Code] = route;
        }
        await _context.SaveChangesAsync(cancellationToken);

        var covered = (await _context.RateCards.Select(x => x.ServiceRouteId)
            .ToListAsync(cancellationToken)).ToHashSet();
        foreach (var spec in specs)
        {
            var route = existing[spec.Item1];
            if (covered.Contains(route.Id)) continue;
            _context.RateCards.Add(new RateCard
            {
                ServiceRouteId = route.Id,
                CargoCategory = "General Cargo",
                MinimumWeightKg = route.Mode == TransportMode.Sea ? 1 : 5,
                RatePerKg = spec.Item8,
                MinimumCharge = route.Mode == TransportMode.Sea ? 50 : 45,
                FuelSurchargeRate = route.Mode == TransportMode.Sea ? .05m : .08m,
                CustomsRate = .02m,
                Currency = "GBP",
                EffectiveFrom = DateTime.UtcNow.Date
            });
        }
        await _context.SaveChangesAsync(cancellationToken);
        return specs.Select(x => existing[x.Item1]).ToList();
    }

    private void AddMilestones(
        IReadOnlyList<CargoItem> cargo,
        IReadOnlyList<ServiceRoute> routes,
        IReadOnlyDictionary<string, Branch> branches)
    {
        for (var index = 0; index < cargo.Count; index++)
        {
            var item = cargo[index];
            var route = routes[index];
            var origin = branches.Values.First(x => x.Id == route.OriginBranchId);
            var destination = branches.Values.First(x => x.Id == route.DestinationBranchId!.Value);
            var started = DateTime.UtcNow.AddDays(-(10 - index));
            var events = new List<ShipmentMilestone>
            {
                Milestone(item, CargoStatus.Logged, ShipmentEventType.General,
                    "Shipment booked", "We received the shipment details.", origin.Name, started),
                Milestone(item, CargoStatus.ReceivedAtWarehouse, ShipmentEventType.WarehouseReceived,
                    "Goods received", "The goods were checked and accepted at our hub.", origin.Name, started.AddHours(8))
            };
            if (item.Status >= CargoStatus.InTransit && item.Status != CargoStatus.AssignedToBatch)
                events.Add(Milestone(item, CargoStatus.InTransit, ShipmentEventType.InTransit,
                    "On the way", $"The goods left {origin.Name} for {destination.Name}.",
                    origin.Name, started.AddDays(2), DateTime.UtcNow.AddDays(route.TransitDaysMin)));
            if (item.Status is CargoStatus.ArrivedAtDestinationHub or CargoStatus.OutForDelivery or CargoStatus.Delivered)
                events.Add(Milestone(item, CargoStatus.ArrivedAtDestinationHub, ShipmentEventType.ArrivedAtHub,
                    "Arrived at destination hub", "The goods arrived and are being prepared for release.",
                    destination.Name, started.AddDays(5)));
            if (item.Status is CargoStatus.OutForDelivery)
                events.Add(Milestone(item, CargoStatus.OutForDelivery, ShipmentEventType.OutForDelivery,
                    "Out for delivery", "The driver is taking the goods to the delivery address.",
                    destination.Name, DateTime.UtcNow.AddHours(-4), DateTime.UtcNow.AddHours(5)));
            if (item.Status is CargoStatus.Delivered)
                events.Add(Milestone(item, CargoStatus.Delivered, ShipmentEventType.Delivered,
                    "Delivered", "The goods were delivered successfully.",
                    destination.Name, DateTime.UtcNow.AddDays(-(index - 3))));
            _context.ShipmentMilestones.AddRange(events);
        }
    }

    private static ShipmentMilestone Milestone(
        CargoItem item, CargoStatus status, ShipmentEventType eventType,
        string title, string description, string location, DateTime occurredAt,
        DateTime? eta = null) => new()
        {
            CargoItemId = item.Id,
            Status = status,
            EventType = eventType,
            Title = title,
            Description = description,
            Location = location,
            OccurredAt = occurredAt,
            Visibility = MilestoneVisibility.CustomerVisible,
            Source = "BiGSAM Operations",
            EstimatedDeliveryAt = eta
        };

    private async Task SeedDirectoriesInventoryAndScheduleAsync(
        IReadOnlyDictionary<string, Branch> branches,
        IReadOnlyList<Customer> customers,
        IReadOnlyList<UserAccount> staffUsers,
        IReadOnlyList<SalesLead> leads,
        CancellationToken cancellationToken)
    {
        var supplierNames = new[]
        {
            "Atlas Packaging", "Harbour Handling Services", "Skyway Cargo Supplies",
            "West Coast Vehicle Care", "Pearl River Export Services"
        };
        var suppliers = supplierNames.Select((name, index) => new Supplier
        {
            SupplierNumber = $"BGL-SUP-{index + 1:000}",
            Name = name,
            Email = $"supplier{index + 1}@bigsamgl.test",
            Phone = $"+23480200010{index + 1:00}",
            CountryCode = index == 4 ? "CN" : index == 2 ? "GB" : "NG",
            TaxNumber = $"LOCAL-TAX-{26001 + index}"
        }).ToList();
        _context.Suppliers.AddRange(suppliers);

        var locations = branches.Values.Select((branch, index) => new InventoryLocation
        {
            BranchId = branch.Id,
            Code = $"LOC-{branch.Code}",
            Name = $"{branch.Name} Stock Area",
            Type = InventoryLocationType.Warehouse,
            AddressOrArea = branch.Address
        }).Take(5).ToList();
        _context.InventoryLocations.AddRange(locations);

        var itemNames = new[]
        {
            "Small shipping box", "Medium shipping box", "Large shipping box", "Packing tape",
            "Bubble wrap roll", "Pallet stretch film", "Cargo label pack", "Safety seal pack"
        };
        var items = itemNames.Select((name, index) => new InventoryItem
        {
            Sku = $"BGL-STOCK-{index + 1:000}",
            Barcode = $"260724000{index + 1}",
            Name = name,
            Description = $"Standard {name.ToLowerInvariant()} used in daily cargo handling.",
            Category = index < 3 ? "Boxes" : "Packing",
            Type = InventoryItemType.PackagingMaterial,
            UnitOfMeasure = index is 4 or 5 ? "Roll" : "Each",
            DefaultUnitCost = 1.25m + index * .85m,
            Currency = "GBP",
            PreferredSupplierId = suppliers[index % suppliers.Count].Id
        }).ToList();
        _context.InventoryItems.AddRange(items);
        _context.InventoryStockLevels.AddRange(items.Select((item, index) => new InventoryStockLevel
        {
            InventoryItemId = item.Id,
            InventoryLocationId = locations[index % locations.Count].Id,
            QuantityOnHand = 40 + index * 18,
            QuantityReserved = 4 + index,
            AverageUnitCost = item.DefaultUnitCost,
            ReorderLevel = 20,
            TargetStockLevel = 150,
            LastMovementAt = DateTime.UtcNow.AddHours(-(index + 1))
        }));

        var contacts = customers.Select((customer, index) => new Contact
        {
            Type = ContactType.Client,
            Status = ContactStatus.Active,
            FullName = customer.FullName,
            CompanyName = customer.CompanyName,
            JobTitle = customer.IsBusiness ? "Shipping Contact" : null,
            Email = customer.Email!,
            Phone = customer.Phone,
            CountryCode = customer.CountryCode,
            Tags = index % 2 == 0 ? "Active client,Air freight" : "Active client,Sea freight",
            CustomerId = customer.Id,
            SalesLeadId = leads[index].Id,
            PreferredBranchId = customer.BranchId,
            OwnerUserId = staffUsers[index % staffUsers.Count].Id,
            IsPrimary = true,
            LastContactedAt = DateTime.UtcNow.AddDays(-index),
            NextFollowUpAt = DateTime.UtcNow.AddDays(index + 2)
        }).ToList();
        _context.Contacts.AddRange(contacts);
        _context.ContactInteractions.AddRange(contacts.Select((contact, index) => new ContactInteraction
        {
            ContactId = contact.Id,
            Type = index % 2 == 0 ? ContactInteractionType.PhoneCall : ContactInteractionType.Email,
            Subject = index % 2 == 0 ? "Shipment update" : "Rate follow-up",
            Details = "Confirmed the next action and updated the client record.",
            OccurredAt = DateTime.UtcNow.AddDays(-index),
            NextActionAt = DateTime.UtcNow.AddDays(index + 2),
            StaffUserId = staffUsers[index % staffUsers.Count].Id,
            SalesLeadId = leads[index].Id
        }));

        var appointmentTypes = new[]
        {
            AppointmentType.Pickup, AppointmentType.ClientMeeting, AppointmentType.WarehouseInspection,
            AppointmentType.SupplierMeeting, AppointmentType.Delivery, AppointmentType.StaffMeeting,
            AppointmentType.CustomsAppointment, AppointmentType.Training
        };
        _context.Appointments.AddRange(Enumerable.Range(0, 8).Select(index =>
        {
            var start = DateTime.UtcNow.Date.AddDays(index + 1).AddHours(9 + index % 4);
            return new Appointment
            {
                AppointmentNumber = $"BGL-APT-260{index + 1}",
                Title = AppointmentTitles[index],
                Type = appointmentTypes[index],
                Status = index < 6 ? AppointmentStatus.Confirmed : AppointmentStatus.Scheduled,
                StartsAt = start,
                EndsAt = start.AddHours(index == 2 ? 2 : 1),
                TimeZone = branches.Values.ElementAt(index % branches.Count).TimeZone,
                OrganizerUserId = staffUsers[0].Id,
                AssignedUserId = staffUsers[index % staffUsers.Count].Id,
                BranchId = branches.Values.ElementAt(index % branches.Count).Id,
                ContactId = contacts[index].Id,
                CustomerId = customers[index].Id,
                SalesLeadId = leads[index].Id,
                Location = branches.Values.ElementAt(index % branches.Count).Name,
                Description = "Scheduled operational work with the linked client and assigned team member.",
                ReminderAt = start.AddHours(-2)
            };
        }));
        await _context.SaveChangesAsync(cancellationToken);
    }

    private string RequiredPassword(string key)
    {
        var value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value) || value.Length < 12)
            throw new InvalidOperationException($"{key} must contain at least 12 characters when local operational data is enabled.");
        return value;
    }

    private static readonly string[] CargoDescriptions =
    [
        "Retail clothing cartons", "Personal effects boxes", "Mobile phone accessories",
        "Restaurant equipment", "Printed books", "Home appliance parts",
        "Cosmetics cartons", "Office furniture fittings"
    ];

    private static readonly string[] CargoCategories =
    [
        "General Cargo", "Personal Effects", "Electronics", "Commercial Equipment",
        "Printed Materials", "Spare Parts", "Cosmetics", "General Cargo"
    ];

    private static readonly string[] AppointmentTitles =
    [
        "Collect retail cartons", "Client shipment review", "Weekly warehouse check",
        "Packaging supplier meeting", "Deliver appliance parts", "Operations planning",
        "Customs document review", "Cargo handling training"
    ];
}
