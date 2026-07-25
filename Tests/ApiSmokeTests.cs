using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace Tests;

public sealed class ApiSmokeTests : IClassFixture<BigsamApiFactory>
{
    private readonly BigsamApiFactory _factory;
    public ApiSmokeTests(BigsamApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AuthoritativeSurface_AuthenticatesScopesAndRetiresOldWorkflows()
    {
        using var client = await CreateStaffClientAsync();

        var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal("local", live.Headers.GetValues("X-BiGSAM-API-Environment").Single());
        using var wrongEnvironment = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        wrongEnvironment.Headers.Add("X-BiGSAM-App-Environment", "production");
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.SendAsync(wrongEnvironment)).StatusCode);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("SuperAdmin", me.GetProperty("user").GetProperty("role").GetString());
        Assert.Equal("manchester", me.GetProperty("activeBranch").GetString());
        Assert.Contains(me.GetProperty("branchMemberships").EnumerateArray(), x =>
            x.GetProperty("branchId").GetString() == "manchester");
        Assert.Contains(me.GetProperty("permissions").EnumerateArray(), x =>
            x.GetString() == "shipment:create");
        Assert.Equal("SuperAdmin", (await client.GetFromJsonAsync<JsonElement>("/api/me"))
            .GetProperty("user").GetProperty("role").GetString());
        Assert.Equal("UK-MAN", (await client.GetFromJsonAsync<JsonElement>("/api/me/branch-scope"))
            .GetProperty("activeBranch").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/branches")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/api/service-routes?pageSize=10")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/api/pricing?pageSize=10")).StatusCode);

        var sessions = await client.GetFromJsonAsync<JsonElement>("/api/auth/sessions");
        Assert.NotEmpty(sessions.EnumerateArray());

        Assert.Equal(HttpStatusCode.Gone,
            (await client.GetAsync("/api/operations/warehouse/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Gone,
            (await client.GetAsync("/api/attendance/check-in")).StatusCode);
        Assert.Equal(HttpStatusCode.Gone,
            (await client.PostAsJsonAsync("/api/finance/invoices", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Gone,
            (await client.GetAsync("/api/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.Gone,
            (await client.GetAsync("/api/contacts/shareholders")).StatusCode);

        var swagger = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = swagger.GetProperty("paths");
        Assert.False(paths.TryGetProperty("/api/attendance", out _));
        Assert.False(paths.TryGetProperty("/api/operations/warehouse/summary", out _));
        Assert.True(paths.TryGetProperty("/api/customer-shipments", out _));
        Assert.True(paths.TryGetProperty("/api/operations/manifests", out _));
        Assert.True(paths.TryGetProperty("/api/service-routes", out _));
        Assert.True(paths.TryGetProperty("/api/documents/manifests/{manifestId}/print", out _));
        Assert.True(paths.TryGetProperty("/api/finance/invoices", out var invoices));
        Assert.False(invoices.TryGetProperty("post", out _));
        Assert.True(paths.TryGetProperty("/api/reports/catalog", out _));
        Assert.True(paths.TryGetProperty("/api/notifications", out _));
        Assert.True(paths.TryGetProperty("/api/admin/staff", out _));
    }

    [Fact]
    public async Task EveryDashboardStaffRole_CanManageBookingsPickupsAndPublishedPrices()
    {
        using var administrator = await CreateStaffClientAsync();
        var roles = new[]
        {
            UserRole.Administrator,
            UserRole.OperationsManager,
            UserRole.SalesManager,
            UserRole.FinanceManager,
            UserRole.Staff
        };

        foreach (var role in roles)
        {
            var suffix = Guid.NewGuid().ToString("N")[..10];
            var email = $"shared-access.{role.ToString().ToLowerInvariant()}.{suffix}@example.test";
            const string password = "SharedAccessPassword!2026";
            var staffResponse = await administrator.PostAsJsonAsync("/api/admin/staff", new
            {
                fullName = $"Shared Access {role} {suffix}",
                email,
                temporaryPassword = password,
                role = role.ToString(),
                employeeNumber = $"BGL-{suffix.ToUpperInvariant()}",
                jobTitle = role.ToString(),
                department = "Shared operations",
                branchCode = "UK-MAN",
                hireDate = DateTime.UtcNow.Date
            });
            Assert.True(staffResponse.StatusCode == HttpStatusCode.Created,
                $"Could not create the {role} verification account: {await staffResponse.Content.ReadAsStringAsync()}");

            using var staff = _factory.CreateClient(new WebApplicationFactoryClientOptions
                { AllowAutoRedirect = false, HandleCookies = true });
            var login = await staff.PostAsJsonAsync("/api/auth/login", new { email, password });
            login.EnsureSuccessStatusCode();
            var session = await login.Content.ReadFromJsonAsync<SessionResponse>();
            staff.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", session!.AccessToken);
            staff.DefaultRequestHeaders.Add("X-Active-Branch", "manchester");

            var me = await staff.GetFromJsonAsync<JsonElement>("/api/auth/me");
            var claims = me.GetProperty("permissions").EnumerateArray()
                .Select(x => x.GetString()).ToHashSet();
            Assert.Contains("price:manage", claims);
            Assert.Contains("shipment:create", claims);
            Assert.Contains("shipment:update", claims);
            Assert.Contains("schedule:manage", claims);
            Assert.Contains("pickup:manage", claims);

            Assert.Equal(HttpStatusCode.OK,
                (await staff.GetAsync("/api/customer-shipments?pageSize=5")).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await staff.GetAsync("/api/schedule/pickups?pageSize=5")).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await staff.GetAsync("/api/sales/shipping-prices?pageSize=5")).StatusCode);

            var priceRequest = new
            {
                serviceCode = $"SHARED-{suffix.ToUpperInvariant()}",
                code = $"SHARED-PRICE-{suffix.ToUpperInvariant()}",
                category = "Shared staff price",
                group = "Role verification",
                name = $"{role} editable price {suffix}",
                description = "Price managed by an authenticated dashboard staff role.",
                serviceArea = "Manchester to Lagos",
                destinationCountryCodes = "NG",
                destinationZone = "Lagos",
                method = "Air",
                currency = "GBP",
                unit = "kg",
                priceKind = "Fixed",
                lowerPrice = 7.25m,
                upperPrice = (decimal?)null,
                handlingFee = 20m,
                documentationFee = 0m,
                minimumCharge = 72.50m,
                minimumQuantity = 10m,
                maximumQuantity = (decimal?)null,
                dispatchSchedule = "Every Thursday",
                estimatedDelivery = "5-7 working days",
                isPublic = false,
                displayOrder = 500,
                reason = $"Confirmed shared price access for {role}."
            };
            var priceResponse = await SendJsonAsync(staff, HttpMethod.Post,
                "/api/sales/shipping-prices", priceRequest, Guid.NewGuid());
            Assert.Equal(HttpStatusCode.Created, priceResponse.StatusCode);

            var shipmentResponse = await SendJsonAsync(staff, HttpMethod.Post,
                "/api/customer-shipments", ShipmentRequest(suffix), Guid.NewGuid());
            Assert.Equal(HttpStatusCode.Created, shipmentResponse.StatusCode);
            var shipment = await shipmentResponse.Content.ReadFromJsonAsync<JsonElement>();
            var invoiceNumber = shipment.GetProperty("invoiceNumber").GetString()!;
            var rowVersion = shipment.GetProperty("rowVersion").GetInt64();
            var updateResponse = await SendJsonAsync(staff, HttpMethod.Patch,
                $"/api/customer-shipments/{invoiceNumber}", new
                {
                    receiverName = $"Updated Receiver {suffix}",
                    destination = new
                    {
                        countryCode = "NG",
                        cityOrState = "Lagos",
                        address = (string?)null
                    },
                    handoverMethod = "Pickup",
                    goodsType = "NonFragile",
                    description = "Confirmed dashboard booking edit",
                    contents = (string?)null,
                    pieceCount = 1,
                    customerNote = $"Updated by {role}",
                    reason = $"Confirmed shared booking access for {role}."
                }, ifMatch: rowVersion);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        }
    }

    [Fact]
    public async Task CustomerShipment_IsAtomicBranchSafeAndIdempotent()
    {
        using var client = await CreateStaffClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var key = Guid.NewGuid();
        var request = ShipmentRequest(suffix);

        var created = await SendJsonAsync(client, HttpMethod.Post, "/api/customer-shipments",
            request, key);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var shipment = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Matches("^BGL[0-9]{4,}INV$", shipment.GetProperty("invoiceNumber").GetString()!);
        Assert.Matches("^ID-[0-9]{2,}$", shipment.GetProperty("clientNumber").GetString()!);
        Assert.Equal("manchester", shipment.GetProperty("branch").GetString());
        Assert.Equal("GBP", shipment.GetProperty("currency").GetString());
        Assert.Equal("Unpaid", shipment.GetProperty("paymentState").GetString());
        Assert.Equal(125.50m, shipment.GetProperty("total").GetDecimal());
        Assert.Equal(125.50m, shipment.GetProperty("balance").GetDecimal());

        var replay = await SendJsonAsync(client, HttpMethod.Post, "/api/customer-shipments",
            request, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayed = await replay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(shipment.GetProperty("id").GetGuid(), replayed.GetProperty("id").GetGuid());
        Assert.Equal(shipment.GetProperty("invoiceNumber").GetString(),
            replayed.GetProperty("invoiceNumber").GetString());

        var duplicate = await SendJsonAsync(client, HttpMethod.Post, "/api/customer-shipments",
            request, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var problem = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CLIENT_MATCH_FOUND", problem.GetProperty("code").GetString());

        var list = await client.GetFromJsonAsync<JsonElement>(
            "/api/customer-shipments?page=1&pageSize=25");
        Assert.Equal(25, list.GetProperty("pageSize").GetInt32());
        Assert.True(list.GetProperty("total").GetInt32() >= 1);
        Assert.True(list.TryGetProperty("hasNextPage", out _));
        Assert.True(list.TryGetProperty("hasPreviousPage", out _));

        client.DefaultRequestHeaders.Remove("X-Active-Branch");
        client.DefaultRequestHeaders.Add("X-Active-Branch", "all");
        var blocked = await SendJsonAsync(client, HttpMethod.Post, "/api/customer-shipments",
            ShipmentRequest(Guid.NewGuid().ToString("N")[..10]), Guid.NewGuid());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
        Assert.Equal("BRANCH_REQUIRED",
            (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Manifest_AttachesExistingInvoiceAndPrintsSafeOperationalView()
    {
        using var client = await CreateStaffClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var shipmentResponse = await SendJsonAsync(client, HttpMethod.Post, "/api/customer-shipments",
            ShipmentRequest(suffix), Guid.NewGuid());
        shipmentResponse.EnsureSuccessStatusCode();
        var shipment = await shipmentResponse.Content.ReadFromJsonAsync<JsonElement>();
        var invoiceNumber = shipment.GetProperty("invoiceNumber").GetString()!;

        var manifestResponse = await SendJsonAsync(client, HttpMethod.Post, "/api/operations/manifests",
            new
            {
                shipmentName = $"UK-NG-AIR-{suffix}", bookingReference = $"MOCK-{suffix}",
                manifestDate = DateTime.UtcNow.Date, method = "Air",
                destination = new { countryCode = "NG", city = "Lagos" }
            }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, manifestResponse.StatusCode);
        var manifest = await manifestResponse.Content.ReadFromJsonAsync<JsonElement>();
        var manifestId = manifest.GetProperty("id").GetGuid();
        Assert.Equal("manchester", manifest.GetProperty("branch").GetString());
        Assert.Equal("GB-MANCHESTER-NG-LAGOS", manifest.GetProperty("routeCode").GetString());

        var wrongManifestResponse = await SendJsonAsync(client, HttpMethod.Post,
            "/api/operations/manifests", new
            {
                shipmentName = $"UK-NG-AIR-WRONG-{suffix}",
                bookingReference = $"WRONG-{suffix}",
                manifestDate = DateTime.UtcNow.Date,
                method = "Air",
                destination = new { countryCode = "NG", city = "Abuja" }
            }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, wrongManifestResponse.StatusCode);
        var wrongManifest = await wrongManifestResponse.Content.ReadFromJsonAsync<JsonElement>();
        var wrongManifestId = wrongManifest.GetProperty("id").GetGuid();
        var wrongAttach = await SendJsonAsync(client, HttpMethod.Post,
            $"/api/operations/manifests/{wrongManifestId}/lines",
            new
            {
                invoiceNumber,
                senderName = $"Integration Sender {suffix}",
                actualWeightKg = 45.2m,
                chargeableWeightKg = 50m
            }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongAttach.StatusCode);
        Assert.Equal("MANIFEST_ROUTE_MISMATCH",
            (await wrongAttach.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var deleteWrong = await SendJsonAsync(client, HttpMethod.Delete,
            $"/api/operations/manifests/{wrongManifestId}?reason=Route+validation+test", null,
            ifMatch: wrongManifest.GetProperty("rowVersion").GetInt64());
        Assert.Equal(HttpStatusCode.NoContent, deleteWrong.StatusCode);

        var connectionResponse = await SendJsonAsync(client, HttpMethod.Put,
            $"/api/operations/tracking/{manifestId}/connection", new
            {
                provider = "MSC", shippingCompanyName = "Mediterranean Shipping Company",
                externalReference = "MSCU1234567", referenceType = "ContainerNumber",
                reason = "Integration test carrier binding"
            }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, connectionResponse.StatusCode);
        var connection = await connectionResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ContainerNumber", connection.GetProperty("referenceType").GetString());
        var publicToken = connection.GetProperty("publicTrackingToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(publicToken));

        var syncKey = Guid.NewGuid();
        var syncResponse = await SendJsonAsync(client, HttpMethod.Post,
            $"/api/operations/tracking/{manifestId}/sync", new { }, syncKey);
        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);
        var sync = await syncResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(sync.GetProperty("isMock").GetBoolean());
        Assert.Equal(4, sync.GetProperty("eventsReceived").GetInt32());
        Assert.Equal(4, sync.GetProperty("newEventsStored").GetInt32());
        var firstEvent = sync.GetProperty("events").EnumerateArray().First();
        Assert.Equal("Draft", firstEvent.GetProperty("reviewState").GetString());

        var replayResponse = await SendJsonAsync(client, HttpMethod.Post,
            $"/api/operations/tracking/{manifestId}/sync", new { }, syncKey);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.True((await replayResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("replayed").GetBoolean());

        var eventId = firstEvent.GetProperty("id").GetGuid();
        var reviewedResponse = await SendJsonAsync(client, HttpMethod.Post,
            $"/api/operations/tracking/{manifestId}/events/{eventId}/review", new
            {
                approve = true, publicTitle = "Container accepted at port",
                publicDescription = "MSC confirmed the latest container movement.",
                place = "Apapa, Lagos", estimatedAt = (DateTime?)null,
                reason = "Approved for customer tracking"
            }, ifMatch: firstEvent.GetProperty("rowVersion").GetInt64());
        Assert.Equal(HttpStatusCode.OK, reviewedResponse.StatusCode);
        var reviewed = await reviewedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var publishedResponse = await SendJsonAsync(client, HttpMethod.Post,
            $"/api/operations/tracking/{manifestId}/events/{eventId}/publish", new
            {
                confirmation = "PUBLISH", reason = "Publish checked MSC movement"
            }, Guid.NewGuid(), reviewed.GetProperty("rowVersion").GetInt64());
        Assert.Equal(HttpStatusCode.OK, publishedResponse.StatusCode);
        var publicTracking = await client.GetFromJsonAsync<JsonElement>(
            $"/api/public/tracking/{publicToken}");
        Assert.Single(publicTracking.GetProperty("events").EnumerateArray());
        Assert.Equal("Container accepted at port", publicTracking.GetProperty("events")
            .EnumerateArray().First().GetProperty("title").GetString());
        Assert.False(publicTracking.TryGetProperty("externalReference", out _));
        Assert.False(publicTracking.TryGetProperty("containerNumber", out _));

        var lineKey = Guid.NewGuid();
        var lineResponse = await SendJsonAsync(client, HttpMethod.Post,
            $"/api/operations/manifests/{manifestId}/lines",
            new { invoiceNumber, senderName = $"Integration Sender {suffix}", actualWeightKg = 45.2m,
                chargeableWeightKg = 50m }, lineKey);
        Assert.Equal(HttpStatusCode.Created, lineResponse.StatusCode);
        var line = await lineResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(invoiceNumber, line.GetProperty("invoiceNumber").GetString());
        Assert.Equal(50m, line.GetProperty("chargeableWeightKg").GetDecimal());

        var manifestList = await client.GetFromJsonAsync<JsonElement>(
            "/api/operations/manifests?includeLines=true&page=1&pageSize=25");
        var listedManifest = manifestList.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == manifestId);
        Assert.Single(listedManifest.GetProperty("lines").EnumerateArray());
        Assert.Equal(invoiceNumber, listedManifest.GetProperty("lines").EnumerateArray()
            .Single().GetProperty("invoiceNumber").GetString());

        var publicByInvoice = await client.GetFromJsonAsync<JsonElement>(
            $"/api/public/tracking/invoice/{invoiceNumber}");
        Assert.Equal(invoiceNumber, publicByInvoice.GetProperty("trackingNumber").GetString());
        Assert.Single(publicByInvoice.GetProperty("events").EnumerateArray());
        Assert.False(publicByInvoice.TryGetProperty("externalReference", out _));
        Assert.False(publicByInvoice.TryGetProperty("containerNumber", out _));

        var replay = await SendJsonAsync(client, HttpMethod.Post,
            $"/api/operations/manifests/{manifestId}/lines",
            new { invoiceNumber, senderName = $"Integration Sender {suffix}", actualWeightKg = 45.2m,
                chargeableWeightKg = 50m }, lineKey);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(line.GetProperty("id").GetGuid(),
            (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());

        var print = await client.GetStringAsync($"/api/operations/manifests/{manifestId}/print");
        Assert.StartsWith("%PDF", print);
        Assert.Contains(invoiceNumber, print);
        Assert.Contains("45.2", print);
        Assert.DoesNotContain("Integration test machinery shipment", print);
        Assert.DoesNotContain("125.50", print);
        Assert.DoesNotContain("50.0", print);
        Assert.DoesNotContain("Payment", print, StringComparison.OrdinalIgnoreCase);

        var controlledReport = await client.GetByteArrayAsync(
            $"/api/operations/manifests/{manifestId}/report?format=xlsx");
        Assert.Equal("PK", Encoding.ASCII.GetString(controlledReport, 0, 2));
        var documentPrint = await client.GetStringAsync(
            $"/api/documents/manifests/{manifestId}/print");
        Assert.StartsWith("%PDF", documentPrint);
        var csvReport = await client.GetByteArrayAsync(
            $"/api/documents/manifests/{manifestId}/report?format=csv");
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, csvReport.Take(3).ToArray());
    }

    [Fact]
    public async Task DirectInflow_PostsOnceWithReceiptAndJournal()
    {
        using var client = await CreateStaffClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var key = Guid.NewGuid();
        var body = new
        {
            counterparty = $"Capital Partner {suffix}",
            amount = 2500.75m,
            currency = "GBP",
            method = "BankTransfer",
            reference = $"CAP-{suffix}",
            description = "Approved capital contribution",
            type = "CapitalIntroduced",
            occurredAt = DateTime.UtcNow.AddMinutes(-1),
            freightClassification = "Other"
        };

        var created = await SendJsonAsync(client, HttpMethod.Post, "/api/finance/inflows",
            body, key);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var result = await created.Content.ReadFromJsonAsync<JsonElement>();
        var transaction = result.GetProperty("transaction");
        var receipt = result.GetProperty("receipt");
        Assert.Equal("Credit", transaction.GetProperty("direction").GetString());
        Assert.Equal("DirectInflow", transaction.GetProperty("sourceType").GetString());
        Assert.Equal(2500.75m, transaction.GetProperty("amount").GetDecimal());
        Assert.Equal("GBP", transaction.GetProperty("currency").GetString());
        Assert.NotEqual(Guid.Empty, transaction.GetProperty("journalEntryId").GetGuid());
        Assert.Matches("^BGL[0-9]+RCT$", receipt.GetProperty("documentNumber").GetString()!);

        var replay = await SendJsonAsync(client, HttpMethod.Post, "/api/finance/inflows",
            body, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(transaction.GetProperty("id").GetGuid(),
            (await replay.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("transaction").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task ShippingPrices_AreBranchScopedVersionedAndReplaySafe()
    {
        using var client = await CreateStaffClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var key = Guid.NewGuid();
        var request = new
        {
            code = $"SEA-BOX-{suffix}", category = "Sea cargo", group = "Boxes",
            name = $"Large box {suffix}", description = "Approved large sea-cargo box",
            serviceArea = "Manchester to Lagos", method = "Sea", unit = "box",
            priceKind = "Fixed", lowerPrice = 85m, upperPrice = (decimal?)null,
            handlingFee = 5m, minimumCharge = 85m, dispatchSchedule = "Weekly",
            estimatedDelivery = "6-8 weeks", reason = "Authoritative contract test"
        };
        var created = await SendJsonAsync(client, HttpMethod.Post, "/api/sales/shipping-prices",
            request, key);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var price = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("manchester", price.GetProperty("branch").GetString());
        Assert.Equal("GBP", price.GetProperty("currency").GetString());
        Assert.Equal("Active", price.GetProperty("state").GetString());

        var replay = await SendJsonAsync(client, HttpMethod.Post, "/api/sales/shipping-prices",
            request, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(price.GetProperty("id").GetGuid(),
            (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task DocumentsAndReports_UseReadableNumbersAndRealOfficeFiles()
    {
        using var client = await CreateStaffClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var created = await SendJsonAsync(client, HttpMethod.Post, "/api/customer-shipments",
            ShipmentRequest(suffix, paid:true), Guid.NewGuid());
        created.EnsureSuccessStatusCode();
        var shipment = await created.Content.ReadFromJsonAsync<JsonElement>();
        var invoiceNumber = shipment.GetProperty("invoiceNumber").GetString()!;

        var invoice = await client.GetFromJsonAsync<JsonElement>(
            $"/api/finance/invoices/{invoiceNumber}");
        Assert.Equal(invoiceNumber, invoice.GetProperty("invoiceNumber").GetString());

        var pdf = await client.GetByteArrayAsync(
            $"/api/finance/invoices/{invoiceNumber}/document?format=pdf");
        Assert.StartsWith("%PDF-1.4", Encoding.ASCII.GetString(pdf, 0, 8));
        var xlsx = await client.GetByteArrayAsync(
            $"/api/finance/invoices/{invoiceNumber}/document?format=xlsx");
        Assert.Equal((byte)'P', xlsx[0]);
        Assert.Equal((byte)'K', xlsx[1]);
        var docx = await client.GetByteArrayAsync(
            $"/api/finance/invoices/{invoiceNumber}/document?format=docx");
        Assert.Equal((byte)'P', docx[0]);
        Assert.Equal((byte)'K', docx[1]);
        var csv = await client.GetByteArrayAsync(
            $"/api/finance/invoices/{invoiceNumber}/document?format=csv");
        Assert.True(csv.Length > 20);
        Assert.Equal(0xEF, csv[0]);
        Assert.Equal(0xBB, csv[1]);
        Assert.Equal(0xBF, csv[2]);

        var receipts = await client.GetFromJsonAsync<JsonElement>(
            "/api/finance/receipts?includeTotal=true");
        var receipt = receipts.GetProperty("items").EnumerateArray().Single();
        var receiptNumber = receipt.GetProperty("documentNumber").GetString()!;
        var receiptPdf = await client.GetByteArrayAsync(
            $"/api/finance/receipts/{receiptNumber}/document?format=pdf");
        Assert.StartsWith("%PDF-1.4", Encoding.ASCII.GetString(receiptPdf, 0, 8));
        var receiptXlsx = await client.GetByteArrayAsync(
            $"/api/finance/receipts/{receiptNumber}/document?format=xlsx");
        Assert.Equal((byte)'P', receiptXlsx[0]);
        Assert.Equal((byte)'K', receiptXlsx[1]);
        var receiptDocx = await client.GetByteArrayAsync(
            $"/api/finance/receipts/{receiptNumber}/document?format=docx");
        Assert.Equal((byte)'P', receiptDocx[0]);
        Assert.Equal((byte)'K', receiptDocx[1]);

        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/reports/catalog");
        Assert.Contains(catalog.EnumerateArray(), x => x.GetProperty("key").GetString() == "sales");
        var previewResponse = await client.PostAsJsonAsync("/api/reports/preview",
            new { reportKey = "sales", previewRows = 25 });
        previewResponse.EnsureSuccessStatusCode();
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEmpty(preview.GetProperty("sheets").EnumerateArray());

        var exportResponse = await client.PostAsJsonAsync("/api/reports/exports",
            new { reportKey = "sales", previewRows = 25 });
        Assert.Equal(HttpStatusCode.Created, exportResponse.StatusCode);
        var export = await exportResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Completed", export.GetProperty("status").GetString());
        var jobId = export.GetProperty("jobId").GetGuid();
        var workbook = await client.GetByteArrayAsync($"/api/reports/exports/{jobId}/download");
        Assert.True(workbook.Length > 1000);
        Assert.Equal((byte)'P', workbook[0]);
        Assert.Equal((byte)'K', workbook[1]);

        var completeness = await client.GetFromJsonAsync<JsonElement>(
            "/api/clients/summary/contact-completeness");
        Assert.True(completeness.GetProperty("totalClients").GetInt32() >= 1);
    }

    private async Task<HttpClient> CreateStaffClientAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, HandleCookies = true });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new
            { email = "admin@integration.test", password = "StrongAdminPassword!2026" });
        login.EnsureSuccessStatusCode();
        var session = await login.Content.ReadFromJsonAsync<SessionResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        client.DefaultRequestHeaders.Add("X-Active-Branch", "manchester");
        return client;
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(HttpClient client, HttpMethod method,
        string uri, object? body, Guid? idempotencyKey = null, long? ifMatch = null)
    {
        using var request = new HttpRequestMessage(method, uri)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
        if (idempotencyKey.HasValue) request.Headers.Add("Idempotency-Key", idempotencyKey.Value.ToString());
        if (ifMatch.HasValue) request.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatch.Value}\"");
        return await client.SendAsync(request);
    }

    private static object ShipmentRequest(string suffix, bool paid = false) => new
    {
        client = new
        {
            existingClientId = (Guid?)null, name = $"Contract Client {suffix}",
            phone = $"+44770{suffix[..7]}", email = $"contract.{suffix}@example.test",
            duplicateReviewId = (Guid?)null, possibleMatchOverrideReason = (string?)null
        },
        receiverName = $"Receiver {suffix}", method = "Air",
        destination = new { countryCode = "NG", cityOrState = "Lagos", address = (string?)null },
        handoverMethod = "Pickup", goodsType = "NonFragile",
        description = "Integration test machinery shipment", contents = (string?)null, pieceCount = 1,
        pricing = new
        {
            method = "NegotiatedPrice", actualWeightKg = (decimal?)null,
            chargeableWeightKg = (decimal?)null, boxItems = Array.Empty<object>(), agreedAmount = 125.50m,
            negotiatedReason = "Approved authoritative contract verification"
        },
        invoiceLines = Array.Empty<object>(),
        payment = paid
            ? new
            {
                state = "Paid", amountPaid = 125.50m,
                method = (string?)"BankTransfer", reference = (string?)$"PAY-{suffix}"
            }
            : new { state = "Unpaid", amountPaid = 0m, method = (string?)null, reference = (string?)null },
        creditApplications = Array.Empty<object>(), referredByClientId = (Guid?)null,
        customerNote = "Authoritative workflow verification"
    };

    private sealed record SessionUser(string Role);
    private sealed record SessionResponse(string AccessToken, SessionUser User);
}

public sealed class BigsamApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"bigsamgl_tests_{Guid.NewGuid():N}";
    private readonly string _adminConnectionString = Environment.GetEnvironmentVariable("BIGSAMGL_TEST_POSTGRES")
        ?? "Host=127.0.0.1;Port=55432;Database=postgres;Username=bigsamgl;Password=bigsamgl-test-only;Include Error Detail=true";

    public BigsamApiFactory() => CreateDatabase();

    private string TestConnectionString
    {
        get
        {
            var builder = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = _databaseName };
            return builder.ConnectionString;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString,
                ["Jwt:Secret"] = "integration-test-secret-that-is-longer-than-thirty-two-bytes",
                ["Seed:ReferenceData"] = "true",
                ["BootstrapAdmin:FullName"] = "Integration Administrator",
                ["BootstrapAdmin:Email"] = "admin@integration.test",
                ["BootstrapAdmin:Password"] = "StrongAdminPassword!2026",
                ["ExchangeRates:BaseUrl"] = "http://127.0.0.1:1",
                ["ExchangeRates:AllowConfiguredFallback"] = "true",
                ["ExchangeRates:FallbackRates:GBP_NGN"] = "2200",
                ["ExchangeRates:FallbackRates:GBP_CNY"] = "9.3",
                ["ExchangeRates:FallbackRates:GBP_USD"] = "1.28",
                ["ExchangeRates:FallbackRates:GBP_EUR"] = "1.17",
                ["Integrations:Mode"] = "Local",
                ["MscTracking:Mode"] = "Local"
            }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) DropDatabase();
    }

    private void CreateDatabase()
    {
        using var connection = new NpgsqlConnection(_adminConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE {new NpgsqlCommandBuilder().QuoteIdentifier(_databaseName)}";
        command.ExecuteNonQuery();
    }

    private void DropDatabase()
    {
        NpgsqlConnection.ClearAllPools();
        using var connection = new NpgsqlConnection(_adminConnectionString);
        connection.Open();
        using (var terminate = connection.CreateCommand())
        {
            terminate.CommandText = "SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
                "WHERE datname = @databaseName AND pid <> pg_backend_pid()";
            terminate.Parameters.AddWithValue("databaseName", _databaseName);
            terminate.ExecuteNonQuery();
        }
        using var drop = connection.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS {new NpgsqlCommandBuilder().QuoteIdentifier(_databaseName)}";
        drop.ExecuteNonQuery();
    }
}
