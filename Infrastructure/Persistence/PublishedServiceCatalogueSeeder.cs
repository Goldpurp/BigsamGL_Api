using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>
/// Adds the initial BiGSAM published service catalogue without overwriting later
/// dashboard edits. Every price remains an ordinary database record.
/// </summary>
public sealed class PublishedServiceCatalogueSeeder
{
    private const string Europe =
        "AL,AD,AT,BE,BG,HR,CY,CZ,DK,EE,FI,FR,DE,GR,HU,IS,IE,IT,LV,LI,LT,LU,MT,MC,ME,NL,MK,NO,PL,PT,RO,SM,RS,SK,SI,ES,SE,CH,VA";
    private readonly ApplicationDbContext _context;

    public PublishedServiceCatalogueSeeder(ApplicationDbContext context) => _context = context;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var branches = await _context.Branches.ToDictionaryAsync(x => x.Code, cancellationToken);
        if (!branches.ContainsKey("UK-MAN") || !branches.ContainsKey("NG-LOS") ||
            !branches.ContainsKey("CN-GZ")) return;

        var seeds = new List<PriceSeed>();
        AddUkNigeriaSea(seeds);
        AddUkNigeriaAir(seeds, "UK-NG-AIR-MAINLAND", "Lagos Mainland", 5.50m, 50, 35, 25,
            "5–7 working days to Lagos");
        AddUkNigeriaAir(seeds, "UK-NG-AIR-ISLAND", "Lagos Island", 6.00m, 50, 35, 25,
            "5–7 working days to Lagos");
        AddUkNigeriaAir(seeds, "UK-NG-AIR-STATES", "Other Nigerian states", 6.50m, 60, 45, 35,
            "8–10 working days to other states");

        AddNigeriaOutbound(seeds);
        AddChinaServices(seeds);

        AddWeightService(seeds, "UK-MAN", "UK-USCA-AIR", "UK to USA / Canada Air",
            "USA / Canada", "US,CA", ShippingPriceServiceMethod.Air, "GBP",
            "Every week; confirm the next dispatch with the UK hub", "5–7 working days",
            20, 15, 20, 0);
        AddWeightService(seeds, "UK-MAN", "UK-EU-AIR", "UK to Europe Air",
            "Europe", Europe, ShippingPriceServiceMethod.Air, "GBP",
            "Every week; confirm the next dispatch with the UK hub", "5–7 working days",
            20, 6, 20, 0);
        AddWeightService(seeds, "UK-MAN", "UK-NG-DHL", "Express DHL to Nigeria",
            "Nigeria", "NG", ShippingPriceServiceMethod.Air, "GBP",
            "Dispatch arranged after acceptance", "3–5 working days",
            10, 20, 20, 0);

        var existing = (await _context.ShippingPriceCatalogueItems
            .Select(x => x.NormalizedCode).ToListAsync(cancellationToken)).ToHashSet();
        foreach (var seed in seeds.Where(x => !existing.Contains(Normalize(x.Code))))
        {
            var branch = branches[seed.BranchCode];
            _context.ShippingPriceCatalogueItems.Add(new ShippingPriceCatalogueItem
            {
                BranchId = branch.Id,
                ServiceCode = seed.ServiceCode,
                Code = seed.Code,
                NormalizedCode = Normalize(seed.Code),
                Category = seed.Category,
                Group = seed.Group,
                Name = seed.Name,
                NormalizedName = Normalize(seed.Name),
                Description = seed.Description,
                ServiceArea = seed.ServiceArea,
                DestinationCountryCodes = seed.DestinationCountryCodes,
                DestinationZone = seed.DestinationZone,
                Method = seed.Method,
                Currency = seed.Currency,
                Unit = seed.Unit,
                PriceKind = seed.PriceKind,
                LowerPrice = seed.Price,
                HandlingFee = seed.HandlingFee,
                DocumentationFee = seed.DocumentationFee,
                MinimumCharge = seed.MinimumCharge,
                MinimumQuantity = seed.MinimumQuantity,
                MaximumQuantity = seed.MaximumQuantity,
                DispatchSchedule = seed.DispatchSchedule,
                EstimatedDelivery = seed.EstimatedDelivery,
                Source = ShippingPriceSource.Staff,
                State = ShippingPriceState.Active,
                IsPublic = true,
                DisplayOrder = seed.DisplayOrder
            });
        }

        if (_context.ChangeTracker.HasChanges())
            await _context.SaveChangesAsync(cancellationToken);
    }

    private static void AddUkNigeriaSea(List<PriceSeed> values)
    {
        const string service = "UK-NG-SEA";
        const string area = "UK to Lagos warehouse collection";
        const string schedule = "Containers load every Tuesday";
        const string delivery = "6–8 weeks to Lagos";
        var order = 0;

        void Add(string code, string category, string name, decimal price, string unit,
            string description, ShippingPriceKind kind = ShippingPriceKind.Fixed) =>
            values.Add(new("UK-MAN", service, code, category, category, name, description,
                area, "NG", "Lagos warehouse collection", ShippingPriceServiceMethod.Sea,
                "GBP", unit, kind, price, null, null, null, null, null,
                schedule, delivery, order++));

        Add("UK-NG-SEA-BOX-S", "Boxes", "Small box", 60, "box", "60 × 40 × 40 cm; maximum 60 kg.");
        Add("UK-NG-SEA-BOX-M", "Boxes", "Medium box", 90, "box", "80 × 50 × 50 cm; maximum 60 kg.");
        Add("UK-NG-SEA-BOX-L", "Boxes", "Large box", 120, "box", "80 × 60 × 60 cm; maximum 60 kg.");
        Add("UK-NG-SEA-MICROWAVE", "Household items", "Microwave", 50, "item", "One microwave.");
        Add("UK-NG-SEA-WASHER", "Household items",
            "Washing machine / dryer / cooker / tabletop fridge", 130, "item",
            "One listed household appliance.");
        Add("UK-NG-SEA-FREEZER-U400", "Household items", "Small freezer under 400 L", 180, "item", "One freezer.");
        Add("UK-NG-SEA-FREEZER-400", "Household items", "400 L freezer", 200, "item", "One freezer.");
        Add("UK-NG-SEA-FREEZER-500", "Household items", "500 L freezer", 230, "item", "One freezer.");
        Add("UK-NG-SEA-FREEZER-600", "Household items", "600 L freezer", 250, "item", "One freezer.");
        Add("UK-NG-SEA-FRIDGE-STANDING", "Household items", "Standing fridge freezer", 180, "item", "One fridge freezer.");
        Add("UK-NG-SEA-FRIDGE-DOUBLE", "Household items", "Double-door fridge freezer", 350, "item", "One fridge freezer.");
        Add("UK-NG-SEA-BARREL", "Barrels and pallets", "Standard 220 L barrel", 160, "item", "One standard barrel.");
        Add("UK-NG-SEA-PALLET", "Barrels and pallets", "Pallet", 650, "pallet", "160 × 120 × 100 cm.");
        Add("UK-NG-SEA-BIKE-S", "Bicycles", "Small bicycle", 20, "item", "One small bicycle.");
        Add("UK-NG-SEA-BIKE-M", "Bicycles", "Medium bicycle", 35, "item", "One medium bicycle.");
        Add("UK-NG-SEA-BIKE-L", "Bicycles", "Large bicycle", 50, "item", "One large bicycle.");
        Add("UK-NG-SEA-TV-32-42", "Televisions", "32–42 inch television", 60, "item", "One television.");
        Add("UK-NG-SEA-TV-43-46", "Televisions", "43–46 inch television", 70, "item", "One television.");
        Add("UK-NG-SEA-TV-47-55", "Televisions", "47–55 inch television", 90, "item", "One television.");
        Add("UK-NG-SEA-TV-57-65", "Televisions", "57–65 inch television", 140, "item", "One television.");
        Add("UK-NG-SEA-TV-67-75", "Televisions", "67–75 inch television", 220, "item", "One television.");
        Add("UK-NG-SEA-TV-77-85", "Televisions", "77–85 inch television", 280, "item", "One television.");
        Add("UK-NG-SEA-CAR-HATCH-LOADED", "Vehicles", "Hatchback, loaded", 2700, "vehicle", "Vehicle manufactured before 2015.");
        Add("UK-NG-SEA-CAR-HATCH-EMPTY", "Vehicles", "Hatchback, empty", 2200, "vehicle", "Vehicle manufactured before 2015.");
        Add("UK-NG-SEA-CAR-SALOON-LOADED", "Vehicles", "Saloon, loaded", 3000, "vehicle", "Vehicle manufactured before 2015.");
        Add("UK-NG-SEA-CAR-SALOON-EMPTY", "Vehicles", "Saloon, empty", 2700, "vehicle", "Vehicle manufactured before 2015.");
        Add("UK-NG-SEA-CAR-SUV-S-LOADED", "Vehicles", "Small SUV, loaded", 3700, "vehicle", "Vehicle manufactured before 2015.");
        Add("UK-NG-SEA-CAR-SUV-S-EMPTY", "Vehicles", "Small SUV, empty", 3200, "vehicle", "Vehicle manufactured before 2015.");
        Add("UK-NG-SEA-CAR-SUV-M-LOADED", "Vehicles", "Medium SUV, loaded", 4000, "vehicle", "Vehicle manufactured before 2015.");
        Add("UK-NG-SEA-CAR-SUV-M-EMPTY", "Vehicles", "Medium SUV, empty", 3700, "vehicle", "Vehicle manufactured before 2015.");
        Add("UK-NG-SEA-VAN", "Vehicles", "Van", 3700, "vehicle",
            "Vehicle manufactured before 2015; final price may require confirmation.", ShippingPriceKind.From);
    }

    private static void AddUkNigeriaAir(List<PriceSeed> values, string service,
        string zone, decimal rate, decimal laptop, decimal tablet, decimal phone, string delivery)
    {
        const string schedule = "Every Thursday; 16:00 UK cut-off";
        values.Add(new("UK-MAN", service, $"{service}-BASE", "Air freight", "Base rate",
            $"{zone} air freight", "Charged on the greater of actual or volumetric weight.",
            $"UK to {zone}", "NG", zone, ShippingPriceServiceMethod.Air, "GBP", "kg",
            ShippingPriceKind.Fixed, rate, 20, null, 10, 10, null, schedule, delivery, 0));
        AddExtra(values, service, $"{service}-LAPTOP", "Special items",
            "Laptop / PlayStation / drone", laptop, "Compliance handling for one listed item.", 10);
        AddExtra(values, service, $"{service}-TABLET", "Special items",
            "iPad / tablet", tablet, "Compliance handling for one listed item.", 11);
        AddExtra(values, service, $"{service}-PHONE", "Special items",
            "Phone", phone, "Compliance handling for one phone.", 12);
        AddExtra(values, service, $"{service}-NAFDAC", "Compliance surcharge",
            "NAFDAC / regulated goods", 50,
            "Food, alcohol, perfume, cosmetics, medication, aerosols or battery-powered goods.", 13,
            "surcharge");

        void AddExtra(List<PriceSeed> target, string serviceCode, string code,
            string category, string name, decimal price, string description, int order,
            string unit = "item") =>
            target.Add(new("UK-MAN", serviceCode, code, category, category, name, description,
                $"UK to {zone}", "NG", zone, ShippingPriceServiceMethod.Air, "GBP", unit,
                ShippingPriceKind.Fixed, price, null, null, null, null, null,
                schedule, delivery, order));
    }

    private static void AddNigeriaOutbound(List<PriceSeed> values)
    {
        const string schedule = "Every Friday";
        const string delivery = "10–15 working days";
        AddNigeriaUk(values, "NG-GB-AIR-MAN", "Manchester", 6500,
            [(0, 2, 35000), (3, 5, 50000), (6, 9, 65000)]);
        AddNigeriaUk(values, "NG-GB-AIR-OTHER", "Other UK cities", 8500,
            [(0, 2, 45000), (3, 5, 60000), (6, 9, 85000)]);
        AddWeightService(values, "NG-LOS", "NG-EU-AIR", "Nigeria to Ireland / Europe Air",
            "Ireland / Europe", $"IE,{Europe}", ShippingPriceServiceMethod.Air, "NGN",
            schedule, delivery, 20, 15500, 10000, 0);
        AddWeightService(values, "NG-LOS", "NG-USCA-AIR", "Nigeria to USA / Canada Air",
            "USA / Canada", "US,CA", ShippingPriceServiceMethod.Air, "NGN",
            schedule, delivery, 20, 18500, 10000, 0);

        void AddNigeriaUk(List<PriceSeed> target, string service, string zone,
            decimal rate, (decimal Min, decimal Max, decimal Price)[] bands)
        {
            var order = 0;
            foreach (var band in bands)
                target.Add(new("NG-LOS", service, $"{service}-{band.Min:0}-{band.Max:0}",
                    "Air freight", "Base rate", $"{band.Min:0}–{band.Max:0} kg flat rate",
                    "Small-shipment flat rate.", $"Nigeria to {zone}", "GB", zone,
                    ShippingPriceServiceMethod.Air, "NGN", "shipment", ShippingPriceKind.Fixed,
                    band.Price, band.Min == 6 ? 5000 : 0, null, null, band.Min, band.Max,
                    schedule, delivery, order++));
            target.Add(new("NG-LOS", service, $"{service}-10PLUS", "Air freight", "Base rate",
                "10 kg and above", "Standard per-kilogram rate.", $"Nigeria to {zone}",
                "GB", zone, ShippingPriceServiceMethod.Air, "NGN", "kg",
                ShippingPriceKind.Fixed, rate, 5000, null, null, 10, null,
                schedule, delivery, order));
        }
    }

    private static void AddChinaServices(List<PriceSeed> values)
    {
        const string schedule = "Contact the China hub for the next consolidation";
        AddTierService(values, "CN-GB-SEA", "China to United Kingdom Sea", "United Kingdom", "GB",
            ShippingPriceServiceMethod.Sea, "cbm", "6–8 weeks", 5,
            [(0, 1, 350), (1.001m, 10, 320), (10.001m, null, 300)]);
        AddTierService(values, "CN-NG-AIR", "China to Nigeria Air", "Lagos, Nigeria", "NG",
            ShippingPriceServiceMethod.Air, "kg", "8–10 working days", 50,
            [(20, 70, 15), (70.001m, 200, 12), (200.001m, null, 10)]);
        AddTierService(values, "CN-NG-SEA", "China to Nigeria Sea", "Lagos, Nigeria", "NG",
            ShippingPriceServiceMethod.Sea, "cbm", "6–8 weeks", 50,
            [(0, 1, 400), (1.001m, 10, 370), (10.001m, null, 350)]);
        AddTierService(values, "CN-GB-AIR", "China to United Kingdom Standard Air",
            "United Kingdom", "GB", ShippingPriceServiceMethod.Air, "kg", "8–10 working days", 50,
            [(20, 70, 20), (70.001m, 200, 15), (200.001m, null, 13)]);
        AddTierService(values, "CN-GB-AIR-BATTERY", "China to United Kingdom Battery Air",
            "United Kingdom", "GB", ShippingPriceServiceMethod.Air, "kg", "10–15 working days", 50,
            [(20, 70, 25), (70.001m, 200, 20), (200.001m, null, 18)]);

        void AddTierService(List<PriceSeed> target, string service, string name,
            string zone, string countries, ShippingPriceServiceMethod method, string unit,
            string delivery, decimal documentation,
            (decimal Min, decimal? Max, decimal Price)[] bands)
        {
            var order = 0;
            foreach (var band in bands)
                target.Add(new("CN-GZ", service, $"{service}-{order + 1}", name, "Base rate",
                    band.Max.HasValue ? $"{band.Min:0.###}–{band.Max:0.###} {unit}" :
                        $"{band.Min:0.###} {unit} and above",
                    $"Published {unit} price band.", name, countries, zone, method, "USD", unit,
                    ShippingPriceKind.Fixed, band.Price, null, documentation, null,
                    band.Min, band.Max, schedule, delivery, order++));
        }
    }

    private static void AddWeightService(List<PriceSeed> values, string branchCode,
        string serviceCode, string name, string zone, string countries,
        ShippingPriceServiceMethod method, string currency, string schedule,
        string delivery, decimal minimumWeight, decimal rate,
        decimal handling, decimal documentation) =>
        values.Add(new(branchCode, serviceCode, $"{serviceCode}-BASE", name, "Base rate",
            name, "Charged on the greater of actual, volumetric and minimum weight.",
            name, countries, zone, method, currency, "kg", ShippingPriceKind.Fixed,
            rate, handling, documentation, null, minimumWeight, null,
            schedule, delivery, 0));

    private static string Normalize(string value) => string.Join(' ', value.Trim().ToUpperInvariant()
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private sealed record PriceSeed(string BranchCode, string ServiceCode, string Code,
        string Category, string Group, string Name, string Description, string ServiceArea,
        string DestinationCountryCodes, string DestinationZone, ShippingPriceServiceMethod Method,
        string Currency, string Unit, ShippingPriceKind PriceKind, decimal? Price,
        decimal? HandlingFee, decimal? DocumentationFee, decimal? MinimumCharge,
        decimal? MinimumQuantity, decimal? MaximumQuantity, string? DispatchSchedule,
        string? EstimatedDelivery, int DisplayOrder);
}
