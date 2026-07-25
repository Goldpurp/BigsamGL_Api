# BigsamGL Control Centre — backend integration contract

**Audience:** the team building the separate staff/management dashboard  
**Backend:** Bigsam Global Logistics API v1  
**Contract date:** 2026-07-24  
**Local API:** `http://127.0.0.1:5120`  
**Interactive API console:** `http://127.0.0.1:5120/`  
**OpenAPI JSON:** `http://127.0.0.1:5120/swagger/v1/swagger.json`

This document is the dashboard handoff contract. The OpenAPI document is the final authority for field nullability, request schemas and generated types. The dashboard must use the API as the only business-data source; it must not reproduce invoice calculations, freight rates, access rules, tracking state transitions or financial totals in frontend code.

## 1. Frontend foundation

- Generate a TypeScript client from `/swagger/v1/swagger.json` during CI. Commit either the generated client or a locked generation artifact so contract changes are visible in review.
- Keep the short-lived access token in memory, not `localStorage`.
- Send `credentials: "include"` on authentication calls so the `HttpOnly` refresh cookie works.
- Send `Authorization: Bearer <accessToken>` on protected calls.
- Use server pagination and filters. Do not download an entire register and filter it in the browser.
- Treat all API dates as UTC ISO-8601 values. Render in the selected branch timezone.
- Render currencies independently with `Intl.NumberFormat`. Never add NGN, CNY, GBP, USD or EUR values without using a returned exchange-rate conversion.
- Display the integration mode from `GET /api/integrations/status`. Development currently uses mock email, storage, payment, MSC and carrier behavior.

A minimal request wrapper should provide:

```ts
type ApiError = {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  errors?: Record<string, string[]>;
};

async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      "X-Correlation-ID": crypto.randomUUID(),
      "X-Device-ID": getStableDeviceId(),
      ...init.headers,
    },
  });
  if (!response.ok) throw await response.json() as ApiError;
  if (response.status === 204) return undefined as T;
  return await response.json() as T;
}
```

## 2. Authentication and session lifecycle

### Login

`POST /api/auth/login`

```json
{
  "email": "admin@example.com",
  "password": "strong-password"
}
```

Successful response:

```json
{
  "accessToken": "jwt",
  "accessTokenExpiresAt": "2026-07-16T12:30:00Z",
  "user": {
    "id": "uuid",
    "fullName": "Staff Name",
    "email": "admin@example.com",
    "role": "Administrator",
    "customerId": null,
    "emailVerified": true,
    "mustChangePassword": false
  }
}
```

The response also sets the `bigsam_refresh` cookie. It is `HttpOnly`, scoped to `/api/auth`, lasts up to 30 days and is `Secure` outside development.

### Refresh and logout

- `POST /api/auth/refresh` with `{ "refreshToken": null }`, or an empty JSON body, rotates the session using the cookie.
- `POST /api/auth/logout` revokes the refresh session and removes the cookie.
- `GET /api/auth/me` restores the signed-in identity after a page refresh.
- On one `401`, call refresh once, replace the in-memory access token and replay the original request once. If refresh also returns `401`, clear dashboard state and return to sign-in.
- Never create an infinite refresh loop.

### Password and verification

| Method | Path | Use |
|---|---|---|
| POST | `/api/auth/password/change` | Authenticated password change |
| POST | `/api/auth/password-reset/request` | Start password reset |
| POST | `/api/auth/password-reset/confirm` | Confirm reset with token |
| POST | `/api/auth/email-verification/request` | Request verification for current user |
| POST | `/api/auth/email-verification/confirm` | Confirm verification token |

Development may return a `mockToken` in account-action responses. Never show or depend on this field in production UI.

## 3. Roles and policy map

Enum values are serialized as strings.

`UserRole`: `SuperAdmin`, `Administrator`, `OperationsManager`, `SalesManager`, `FinanceManager`, `Staff`, `Customer`.

| API policy | Allowed roles |
|---|---|
| `StaffOnly` | Every role except `Customer` |
| `AdminOnly` | `SuperAdmin`, `Administrator` |
| `OperationsTeam` | `SuperAdmin`, `Administrator`, `OperationsManager`, `Staff` |
| `SalesTeam` | `SuperAdmin`, `Administrator`, `SalesManager`, `Staff` |
| `FinanceTeam` | `SuperAdmin`, `Administrator`, `FinanceManager`, `Staff` |
| `FinanceApprover` | `SuperAdmin`, `Administrator`, `FinanceManager` |
| `InventoryTeam` | `SuperAdmin`, `Administrator`, `OperationsManager`, `FinanceManager`, `Staff` |
| `AttendanceManager` | `SuperAdmin`, `Administrator`, `OperationsManager` |

Use the role to hide impossible actions, but always treat a backend `403` as authoritative. A customer account must never enter this dashboard.

## 4. Shared HTTP behavior

### JSON and identifiers

- JSON uses camel-case property names.
- Enums are string names, not numeric values.
- Entity IDs are UUID strings.
- Money is a decimal amount plus an ISO three-letter currency. Do not use JavaScript floating-point arithmetic to recalculate accounting totals.
- Empty successful mutations commonly return `204 No Content`.
- Creation commonly returns `201 Created`; queued work returns `202 Accepted`.

### Pagination

New list screens should request `page`, `pageSize` and usually `includeTotal=true`. Maximum `pageSize` is 200; the recommended table size is 25 or 50.

```json
{
  "items": [],
  "page": 1,
  "pageSize": 50,
  "hasPrevious": false,
  "hasNext": true,
  "total": 187,
  "totalPages": 4
}
```

Some backward-compatible routes require `paged=true` before returning this shape. Without it they return a bounded array. New dashboard screens should always select paged mode when available.

### Errors

| HTTP | Meaning | UI treatment |
|---|---|---|
| `400` | Invalid input or domain operation | Show field errors or `detail` |
| `401` | Missing/expired/invalid session | Attempt one refresh |
| `403` | Role does not allow action | Show access denied; do not retry |
| `404` | Record not found or not owned | Show not found |
| `409` | State or unique-data conflict | Refresh detail and show conflict |
| `422` | Logistics rule violation | Show the returned business explanation |
| `429` | Traffic limit reached | Honor `Retry-After`, back off and retry reads only |
| `500` | Unexpected backend error | Show correlation ID and generic support message |

Validation errors use `errors: { fieldName: [messages] }`. Other problem responses use `title`, `status` and usually `detail`.

### Audit headers

All authenticated staff calls are recorded automatically. Send a stable `X-Device-ID` and a unique `X-Correlation-ID`. If the staff member explicitly enables location auditing, also send:

- `X-Staff-Location-Consent: true`
- `X-Staff-Latitude`
- `X-Staff-Longitude`
- `X-Staff-Location-Accuracy`
- `X-Staff-Location-Captured-At`

Never collect location without clear consent. The API rejects malformed, future or more-than-24-hour-old location observations from audit capture.

## 5. Recommended dashboard navigation and APIs

### Overview

| Method | Path | Notes |
|---|---|---|
| GET | `/api/dashboard/overview?from=&to=&currency=&countryCode=` | KPIs, revenue trend, recent activity |
| GET | `/api/dashboard/sales-performance?from=&to=&currency=&countryCode=` | Weekly/historical sales performance |
| GET | `/api/integrations/status` | Provider-mode badge |

Keep each reporting currency separate. `countryCode=NG` resolves local calculations to NGN and China to CNY; an explicit `currency` wins.

### Sales document centre

Do not build enquiry, lead-pipeline or next-action pages. The supported sales workspace is documents and prices.

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/sales/summary` | Currency-separated commercial summary |
| GET | `/api/sales/quotations` | Filtered/paginated quotation register |
| GET | `/api/sales/invoices` | Invoice and credit-note register |
| GET | `/api/sales/receipts` | Receipt/payment-method register |
| GET | `/api/sales/shipping-prices` | Freight and additional service prices |
| POST | `/api/operations/quotes` | Create calculated quotation |
| POST | `/api/operations/quotes/{quoteId}/email` | Email linked-client quotation |
| POST | `/api/finance/invoices` | Create draft sales invoice or credit note |
| POST | `/api/finance/invoices/{id}/issue` | Issue and post invoice |
| POST | `/api/finance/invoices/{id}/email` | Email invoice with owned document attachments |
| POST | `/api/finance/payments` | Record payment and auto-create receipt |
| GET | `/api/finance/invoices/{id}/print` | Server-calculated printable invoice HTML |
| GET | `/api/finance/receipts/{id}/print` | Printable receipt/voucher HTML |

Invoice creation:

```json
{
  "customerId": "uuid",
  "issueDate": "2026-07-16T00:00:00Z",
  "dueDate": "2026-07-30T00:00:00Z",
  "currency": "NGN",
  "discountAmount": 0,
  "notes": "Optional",
  "type": "Sales",
  "originalInvoiceId": null,
  "issuerBranchCode": "LOS",
  "preferredPaymentMethod": "BankTransfer",
  "lines": [
    { "description": "Air freight", "quantity": 12.5, "unitPrice": 4200, "taxRate": 0.075 }
  ]
}
```

Invoice identifiers are generated by PostgreSQL in the `BGL0000INV` sequence and are never supplied by the browser. Valid invoice states: `Draft`, `Issued`, `PartiallyPaid`, `Paid`, `Overdue`, `Void`; types: `Sales`, `CreditNote`; payment states: `Unpaid`, `PartiallyPaid`, `Paid`, `NotApplicable`.

Payment methods: `Cash`, `BankTransfer`, `Card`, `Wallet`, `Other`. Cash posts to cash; bank transfer/card remain distinct methods but settle to the configured banking/clearing path.

#### Managed service catalogue and shipment pricing

Use [`MANAGED_SHIPPING_SERVICES.md`](MANAGED_SHIPPING_SERVICES.md) for the complete
catalogue, public display, estimator and customer-shipment contract. The dashboard can
create/edit/activate/deactivate `/api/sales/shipping-prices`; every service is grouped by
`serviceCode`. New standard shipments must send that service code and catalogue
selections to `/api/customer-shipments`. The backend recalculates invoice lines from
active PostgreSQL values. Only `NegotiatedPrice` accepts a staff-entered total.

### Email centre and targeted broadcasts

Single-client email is available to staff at `POST /api/communications/clients/{customerId}/email`. Broadcast management is restricted to `SuperAdmin` and `Administrator`.

#### Audience model

An audience may combine whole groups and exact IDs:

```json
{
  "allClients": false,
  "allContacts": false,
  "allSuppliers": false,
  "allActiveStaff": false,
  "customerIds": ["customer-uuid"],
  "contactIds": ["contact-uuid"],
  "supplierIds": [],
  "staffUserIds": ["staff-user-uuid"],
  "contactTypes": ["Director", "Shareholder"],
  "excludeEmails": ["exclude@example.com"]
}
```

At least one group, ID or contact type is required. Explicit ID selection is limited to 10,000 records. Emails are normalized and deduplicated across all selected sources. When the same email belongs to multiple sources, priority is `Client`, `Contact`, `Supplier`, then `Staff`.

Purpose behavior:

- `General`: selected valid emails are eligible unless email is excluded or a matching client has disabled email.
- `Operational`: a matching client must have email and operational updates enabled.
- `Marketing`: a matching client must have explicitly enabled marketing updates. No preference record means no marketing consent.

Always preview before enabling **Create draft**:

`POST /api/communications/broadcasts/audience/preview`

```json
{
  "purpose": "Operational",
  "audience": {
    "allClients": true,
    "allContacts": false,
    "allSuppliers": false,
    "allActiveStaff": false,
    "customerIds": [],
    "contactIds": [],
    "supplierIds": [],
    "staffUserIds": [],
    "contactTypes": [],
    "excludeEmails": []
  }
}
```

Preview response:

```json
{
  "totalUniqueRecipients": 120,
  "eligibleRecipients": 114,
  "skippedRecipients": 6,
  "duplicatesRemoved": 8,
  "sampleTruncated": false,
  "sample": [
    {
      "sourceType": "Client",
      "sourceId": "uuid",
      "name": "Client Name",
      "email": "client@example.com",
      "eligible": true,
      "skipReason": null
    }
  ]
}
```

Create the immutable recipient snapshot as a draft:

`POST /api/communications/broadcasts`

```json
{
  "name": "July shipment update",
  "subject": "Your July shipment update",
  "body": "Message body. Plain text is the current supported format.",
  "replyTo": "operations@example.com",
  "purpose": "Operational",
  "audience": {
    "allClients": true,
    "allContacts": false,
    "allSuppliers": false,
    "allActiveStaff": false,
    "customerIds": [],
    "contactIds": [],
    "supplierIds": [],
    "staffUserIds": [],
    "contactTypes": [],
    "excludeEmails": []
  }
}
```

Rules: name maximum 160 characters, subject maximum 250, body maximum 20,000, and `replyTo` must be a valid email. Draft campaign numbers use a PostgreSQL sequence such as `BGL00000001EML`.

Attachment metadata is added while the campaign is still `Draft`:

`POST /api/communications/broadcasts/{id}/attachments`

```json
{
  "fileName": "warehouse-photo.jpg",
  "contentType": "image/jpeg",
  "sizeBytes": 483920
}
```

Only images and PDFs are accepted. Maximum per file is 20 MB; maximum per campaign is 10 files and 25 MB combined. In mock mode this creates mock storage metadata rather than uploading binary bytes. Replace the storage gateway for real file transfer without changing dashboard routes.

Queue now:

```http
POST /api/communications/broadcasts/{id}/queue
Content-Type: application/json

{ "scheduledFor": null }
```

Schedule in UTC by setting `scheduledFor` to a future timestamp. The PostgreSQL-backed worker claims 50 recipients at a time, so the queue HTTP request stays fast. Poll campaign detail every 2–5 seconds while `Queued` or `Sending`, then slow or stop polling at a terminal state.

Campaign states: `Draft`, `Scheduled`, `Queued`, `Sending`, `Sent`, `PartiallyFailed`, `Cancelled`. Recipient states: `Pending`, `Processing`, `Sent`, `Failed`, `Skipped`.

| Method | Path | UI action |
|---|---|---|
| POST | `/api/communications/broadcasts/audience/preview` | Preview/de-duplicate/consent-check audience |
| POST | `/api/communications/broadcasts` | Create draft |
| PUT | `/api/communications/broadcasts/{id}` | Edit draft and replace recipient snapshot |
| GET | `/api/communications/broadcasts` | Filtered campaign register |
| GET | `/api/communications/broadcasts/{id}` | Counts, attachments and provider mode |
| GET | `/api/communications/broadcasts/{id}/recipients` | Recipient delivery table |
| POST | `/api/communications/broadcasts/{id}/attachments` | Add image/PDF metadata |
| DELETE | `/api/communications/broadcasts/{id}/attachments/{attachmentId}` | Remove draft attachment |
| POST | `/api/communications/broadcasts/{id}/queue` | Queue now or schedule |
| POST | `/api/communications/broadcasts/{id}/retry-failed` | Retry a `PartiallyFailed` campaign |
| POST | `/api/communications/broadcasts/{id}/cancel` | Cancel draft/scheduled/queued campaign with reason |

The `GET` list supports `status`, `purpose`, `search`, `from`, `to`, `page`, `pageSize`, `includeTotal`. Recipients support `status`, `sourceType`, `search` and pagination. Client deliveries also appear in `GET /api/communications/clients/{customerId}`.

Current provider mode is deliberately `Mock`. The workflow, recipient snapshots, audit records, delivery states, retries and communication history are real PostgreSQL data; no external mailbox receives a message until a live email gateway is configured.

### Contacts and clients

Separate directory views are required:

| View | Endpoint |
|---|---|
| Clients | `GET /api/contacts/clients` and `GET /api/clients` |
| Suppliers | `GET /api/contacts/suppliers` |
| Directors | `GET /api/contacts/directors` |
| Shareholders | `GET /api/contacts/shareholders` |
| Staff contacts | `GET /api/contacts/staff` |
| Service providers | `GET /api/contacts/service-providers` |
| Typed/all contacts | `GET /api/contacts` |

Before staff creates or changes a client, call `POST /api/clients/match-check` with `fullName`, `email`, `phone` and optional `invoiceNumber`. If matches are returned, show a blocking comparison dialog and resolve using `POST /api/clients/duplicate-reviews/{reviewId}/resolve`. Exact normalized name, phone, email and invoice ownership are protected by backend rules.

Contact types: `Client`, `Supplier`, `PartnerAgency`, `Prospect`, `Carrier`, `GovernmentAgency`, `Other`, `Director`, `Shareholder`, `Staff`, `ServiceProvider`. Contact states: `Active`, `Inactive`, `DoNotContact`, `Archived`.

### Purchases and payables

The simplified purchase register separates `GoodsInput`, `Service`, `Asset`, and `Other`.

| Method | Path | Purpose |
|---|---|---|
| GET/POST | `/api/purchasing/purchases` | Filter register/create draft |
| GET/PUT | `/api/purchasing/purchases/{id}` | Detail/edit draft |
| POST | `/api/purchasing/purchases/{id}/submit` | Submit for approval |
| PATCH | `/api/purchasing/purchases/{id}/decision` | Approve/reject with reason |
| POST | `/api/purchasing/purchases/{id}/payouts` | Record payment and voucher |
| POST | `/api/purchasing/purchases/{id}/receive` | Receive goods into inventory |
| GET | `/api/purchasing/payouts` | Payout register |
| GET | `/api/purchasing/summary` | Type/category/payment/outstanding summary |

States: `Draft`, `Submitted`, `Approved`, `PartiallyPaid`, `Paid`, `Rejected`, `Cancelled`. Receiving physical goods requires inventory item links, a location and an idempotency key.

### Basic company inventory

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/inventory/basic-stock` | Minimal stock list |
| GET/POST | `/api/inventory/items` | Item register/create |
| GET/PUT | `/api/inventory/items/{id}` | Detail/update |
| GET/POST | `/api/inventory/locations` | Location register/create |
| PUT | `/api/inventory/locations/{id}` | Update location |
| POST | `/api/inventory/stock-in` | Basic stock receipt |
| POST | `/api/inventory/stock-out` | Basic stock issue |
| POST | `/api/inventory/transfers` | Transfer between locations |
| POST | `/api/inventory/adjustments` | Controlled correction/write-off |
| GET | `/api/inventory/transactions` | Immutable movement ledger |
| GET | `/api/inventory/summary` | Stock and low-stock totals |

Every stock mutation requires an `idempotencyKey`; generate a new UUID for one intended operation and reuse it only when retrying that exact operation. Do not optimistically increase quantities before the server confirms.

### Schedule and appointments

Use `/api/appointments` for pickup, delivery, executive/client/staff/supplier meetings, maintenance, inspection, training, customs and compliance schedules.

Types: `ExecutiveMeeting`, `ClientMeeting`, `Pickup`, `Delivery`, `WarehouseInspection`, `StaffMeeting`, `SupplierMeeting`, `CustomsAppointment`, `Other`, `Maintenance`, `Training`, `ComplianceDeadline`.

States: `Tentative`, `Scheduled`, `Confirmed`, `InProgress`, `Completed`, `Cancelled`, `NoShow`.

| Method | Path | Purpose |
|---|---|---|
| GET/POST | `/api/appointments` | Calendar register/create |
| GET/PUT | `/api/appointments/{id}` | Detail/update |
| GET | `/api/appointments/availability` | Staff time-conflict check |
| PATCH | `/api/appointments/{id}/status` | Lifecycle change |
| PATCH | `/api/appointments/{id}/attendees/{attendeeId}/response` | Attendee response |
| POST | `/api/appointments/{id}/reminder` | Send reminder through provider gateway |

### Freight operations and tracking

#### Default operations experience: shipment manifests

A manifest is one shipment, not one customer's invoice. The manifest register therefore shows shipment headers. Put one primary **New shipment manifest** button above the table and keep warehouse, tracking, batches, movements and containers under **More operations tools**.

The create modal has only shipment-level fields:

```json
{
  "shipmentId": "UK-NG-SEA-2026-0042",
  "shipmentBookingId": "MSC-BKG-009812",
  "serviceRouteId": "route-uuid",
  "manifestDate": "2026-07-16T00:00:00Z",
  "logisticsMethod": "Ship"
}
```

`shipmentId` is the name shown for the manifest. `shipmentBookingId` is the shipment/carrier booking reference. Both are required and unique. The backend also generates an immutable internal number such as `BGL00000001MNF`.

The normal **Edit manifest** modal contains exactly two editable fields:

```json
{
  "shipmentId": "UK-NG-SEA-2026-0042-UPDATED",
  "shipmentBookingId": "MSC-BKG-009812-UPDATED"
}
```

Route, direction, manifest date and Air/Ship method are read-only after creation. Customer, invoice, sender, package, money, weight, handover and container information must never be edited on the shipment header.

The backend snapshots `routeCode`, `routeName`, `originCountryCode`, and `destinationCountryCode` from the selected route. `UK-NG-AIR` and `UK-NG-SEA` belong to `GB->NG`, while reverse routes belong to `NG->GB`.

| Method | Path | Screen action |
|---|---|---|
| POST | `/api/operations/manifests` | Create shipment manifest |
| PUT | `/api/operations/manifests/{id}` | Edit Shipment ID and Shipment Booking ID only |
| GET | `/api/operations/manifests` | Paginated/filterable shipment register |
| GET | `/api/operations/manifests/date-groups` | Air/Ship shipment and entry totals |
| GET | `/api/operations/manifests/{id}` | Shipment header and calculated totals |
| GET | `/api/operations/manifests/{id}/entries` | Paginated customer/package rows |
| POST | `/api/operations/manifests/{id}/entries` | Add a customer/package row |
| PUT | `/api/operations/manifests/{id}/entries/{entryId}` | Edit the selected row |
| GET | `/api/operations/manifests/customer-name-suggestions?name=` | Existing-customer suggestions for name/referrer inputs |
| GET | `/api/operations/manifests/{id}/entries/{entryId}/invoice` | Separate read-only generated invoice/payment/receipt panel |
| GET | `/api/operations/manifests/{id}/entries/{entryId}/pricing` | Read-only Air/Sea/negotiated calculation evidence |
| DELETE | `/api/operations/manifests/{id}/entries/{entryId}` | Remove only that row |
| POST | `/api/operations/manifests/{id}/entries/{entryId}/release-check` | Verify that row's invoice |
| POST | `/api/operations/manifests/{id}/entries/{entryId}/email` | Email that row's customer |
| GET | `/api/operations/manifests/{id}/print` | Safe shipment-manifest print |
| GET | `/api/operations/manifests/{id}/report` | Complete shipment CSV report |

The register returns calculated `entryCount`, `customerCount`, `packageCount`, actual/cost weights and `amountsByCurrency`. Filters include `search`, `serviceRouteId`, direction, Air/Ship, customer, invoice number, payment type, dates and pagination. An unfiltered request is **All manifests**. Direction screens contain separate Air and Ship tabs.

`date-groups` counts shipment headers separately from customer/package entries. Never call an entry a shipment, never count customer rows as manifests, and never combine currencies.

#### Customer and package entries inside the manifest

Open a shipment row to show its customer/package entries. Label the normal text input
**Customer's name**. After each meaningful name change, call
`GET /api/operations/manifests/customer-name-suggestions?name={typedName}&take=8`. Selecting a
suggestion fills `existingCustomerId`; otherwise collect the new customer's phone, email and
country. A new customer and their primary Client contact are saved atomically, including email.
Never allow the browser to supply an invoice number.

Create an entry with customer, invoice and operational sections:

```json
{
  "customer": {
    "name": "Ada Okafor",
    "existingCustomerId": "optional-customer-uuid",
    "phone": null,
    "email": null,
    "countryCode": null,
    "duplicateReviewId": null
  },
  "receiver": {
    "name": "Chidi Okafor",
    "phone": "+2348111111111",
    "email": "chidi@example.com"
  },
  "invoice": {
    "amount": 60000,
    "paymentMethod": "BankTransfer"
  },
  "pricing": {
    "chargeBasis": "SeaBoxesAndItems",
    "negotiationReference": null,
    "lines": [
      {
        "type": "SeaBox",
        "description": "Garment cartons",
        "boxSize": "Large",
        "quantity": 3,
        "unitPrice": 20000,
        "lengthCm": null,
        "widthCm": null,
        "heightCm": null,
        "actualWeightKg": null,
        "volumetricDivisor": null
      }
    ]
  },
  "referrerCustomerId": "optional-existing-customer-uuid",
  "cargoItemId": "optional-cargo-uuid",
  "shippingBatchId": "optional-batch-uuid",
  "shipmentMovementId": null,
  "shipmentContainerId": null,
  "senderName": "Sender for this package",
  "packageDescription": "Three cartons of garments",
  "quantity": 3,
  "actualWeightKg": 42.5,
  "costWeightKg": 48,
  "handoverMethod": "SentOut",
  "handoverAddress": "21 Delivery Avenue",
  "handoverCity": "Lagos",
  "notes": "Customer row notes"
}
```

Receiver name, phone and valid email are required and stay separate from the paying customer.
`AirWeightAndDimensions` accepts only Air lines with dimensions, actual weight, volumetric divisor,
and rate per billable kg. `SeaBoxesAndItems` accepts `SeaBox`/`SeaItem` lines and never calculates
price from weight. `Negotiated` works for Air or Sea machinery/special cargo and may carry an
agreement reference. The invoice amount must equal the pricing-line total.

- For a new customer, `existingCustomerId` is null and `phone`, `email`, and `countryCode` are required. For a selected existing customer, the entered name must exactly match that suggestion and the stored contact details are reused.
- A `409 Conflict` is the duplicate-review prompt. Resolve it through `/api/clients/duplicate-reviews/{reviewId}/resolve`, then resubmit the same customer identity with `duplicateReviewId`.
- `referrerCustomerId` is optional but must identify an existing customer and cannot equal the package customer. Never accept a typed/free-form referrer ID.
- Creating the entry atomically creates one unique `BGL0000INV` invoice, records the exact server date/time, sets due time equal to issue time (no payment terms), derives currency from the route's origin/issuer branch, confirms full initial payment, posts the accounting records, and generates a receipt.
- The response contains immutable `invoiceId` and `invoiceNumber`. Load the full read-only invoice panel from the entry invoice endpoint; it includes payment reference/time and receipt ID/number. There is no invoice update endpoint.
- One manifest can contain many customer shipments, but every entry owns exactly one invoice and the database prevents the invoice from being reused by another entry.
- Every row is clickable. Show **Edit package**, **View invoice**, **Email customer**, **Verify invoice**, and **Remove from manifest** actions.
- The Edit payload intentionally excludes customer, invoice, amount, currency, payment method, referrer and referral points:

```json
{
  "cargoItemId": "optional-cargo-uuid",
  "shippingBatchId": "optional-batch-uuid",
  "shipmentMovementId": "optional-flight-or-sailing-uuid",
  "shipmentContainerId": "optional-container-uuid",
  "senderName": "Corrected sender",
  "packageDescription": "Corrected operational description",
  "quantity": 3,
  "actualWeightKg": 42.5,
  "costWeightKg": 48,
  "handoverMethod": "SentOut",
  "handoverAddress": "21 Delivery Avenue",
  "handoverCity": "Lagos",
  "notes": "Operational note"
}
```

- Save the package once before assigning an airline movement or sea container. Later placement is handled by Edit, matching the real workflow where carrier/container decisions occur after invoicing.
- Different rows may use different containers. A Ship row can receive its sailing first and its container later.
- Complete Air placement or Ship movement+container placement synchronizes the cargo allocation history when a cargo record and invoice are present.
- Removing a row does not delete or silently unassign the customer, invoice, cargo or allocation history.
- `handoverLabel` is backend-provided: **Collection** means hub pickup and **Sent out** means dispatch to the row address.
- Before handover, post the presented invoice number to that entry's `release-check`. A false result means **Do not hand over**.

#### Referral loyalty

Use the same existing-customer suggestion list for the optional referrer selector. A successfully created and paid referred shipment gives the referrer 10 points exactly once. The immutable points transaction is tied to the generated invoice.

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/loyalty/customers/{customerId}` | Account balance plus paginated points history |
| GET | `/api/loyalty/leaderboard` | Paginated lifetime-points ranking |

Do not calculate points in the dashboard. Use `referralPointsAwarded`, the loyalty account response, and ledger rows returned by the API.

#### Printing versus complete reports

`GET /api/operations/manifests/{id}/print` is the safe operational shipment sheet. Its header contains Shipment ID, Shipment Booking ID, internal manifest number, date and route. Its rows contain owner, sender, invoice, tracking, Collection/Sent out, container, quantity, actual weight and handover location. It excludes package contents, amount, currency, payment method and cost weight.

`GET /api/operations/manifests/{id}/report` downloads a CSV containing the complete record, including contents/descriptions, actual and cost weights, amount, currency, payment type, addresses, shipment placement, container, notes and audit timestamps. Use **Print manifest** for operations and **Download complete report** for authorized reporting; do not turn the print page into a financial report.

#### Editable container names

Container records have both an official `containerNumber` and an optional friendly `displayName` such as `Shared Container B`. Create can include `displayName`; edit through `PUT /api/operations/containers/{containerId}`. The edit payload includes the current `containerNumber`, `provider`, `containerType`, optional batch/movement and carrier references, `displayName`, and `isActive`. The provider/official-number pair remains unique, MSC number format is validated, and changes are audited. Package rows return both `containerNumber` and `containerName`; show `containerName` first and keep the official number available in the detail.

The original batch manifest remains an advanced consolidation report. Do not require a shipping batch or container before creating the shipment manifest header or customer rows.

Core flow:

1. Staff creates/configures branches, routes and effective rate cards.
2. A quote is calculated by the API from route, cargo category, dimensions and weight.
3. Customer creates a booking from a customer-owned quote.
4. Finance confirms payment; operations receives the booking and creates cargo/tracking.
5. Cargo may later be allocated to an internal consolidation batch and a separate external movement/container.
6. Milestones, batch updates and carrier/container sync feed the customer-visible tracking timeline.

Important API groups:

| Group | Endpoints |
|---|---|
| Branches and controls | `/api/operations/branches`, `/api/company/branches/controls`, `/api/company/branches/{branchId}/control` |
| Routes/rates | `/api/operations/routes`, `/api/operations/rates`, `/api/operations/routes/{routeId}/rates` |
| Item/service pricing | `/api/operations/additional-service-pricing` |
| Bookings | `/api/operations/bookings`, `/api/operations/bookings/{id}/receive` |
| Warehouse | `/api/operations/warehouse/summary`, `/inventory`, `/exceptions` |
| Cargo milestones | `/api/operations/cargo/{cargoId}/milestones` |
| Carrier binding/sync | `/api/operations/cargo/{cargoId}/carrier`, `/carrier-sync` |
| Consolidation | `/api/operations/consolidation-batches` |
| External movement | `/api/operations/shipment-movements` |
| Late cargo allocation | `/api/operations/shipment-allocations` |
| MSC containers | `/api/operations/containers` |
| Batch client updates | `/api/operations/shipments/{batchId}/clients`, `/updates`, `/timeline` |
| Simple day-to-day manifests | `/api/operations/manifests` |
| Advanced batch manifest | `/api/Manifest/{batchId}/pdf`, `/excel`, `/email` |
| Documents | `POST /api/operations/documents` |

Cargo states: `Logged`, `ReceivedAtWarehouse`, `AssignedToBatch`, `InTransit`, `ArrivedAtDestinationHub`, `OutForDelivery`, `Delivered`, `Cancelled`, `PickupScheduled`, `PickedUp`, `IncomingToWarehouse`, `Delayed`, `OnHold`.

Keep BigsamGL’s internal consolidation batch separate from the third-party airline, vessel, carrier movement or MSC container. Do not describe external vessels or aircraft as BigsamGL-owned.

### Workforce, attendance and staff administration

| Area | Endpoints |
|---|---|
| Staff lifecycle | `/api/staff`, `/api/staff/{userId}`, `/suspend`, `/reactivate`, `/status` |
| My attendance | `POST /api/workforce/attendance`, `GET /api/workforce/attendance/my` |
| Leave | `/api/workforce/leave`, `/my`, `/{id}/decision` |
| Stations | `/api/workforce/attendance/stations`, rotate code, staff PIN |
| Kiosk | `/api/attendance/kiosk/stations/{stationPublicId}/challenge`, `/api/attendance/kiosk/record` |
| Reports | `/api/workforce/attendance/report`, `/export.csv`, `/print` |
| Corrections | `/api/workforce/attendance/sessions/{sessionId}/correct` |

The anonymous kiosk is the only no-login attendance flow. The frontend must request geolocation at check-in/out time, send captured accuracy/time/device data, and never fake or manually substitute coordinates. Staff selects their server-returned profile, confirms matching email and enters their private attendance PIN. Do not cache PINs.

### Finance, accounting and reports

| Area | API prefix |
|---|---|
| Invoices/payments/expenses | `/api/finance` |
| Receipts, vouchers, transaction register | `/api/finance/receipts`, `/api/finance/transactions` |
| Exchange rates and conversion | `/api/finance/currency` |
| Accounts, journals, trial balance | `/api/accounting` |
| Separated management reports | `/api/reports` |

Reports are deliberately separated: `/sales`, `/purchases`, `/inventory`, `/contacts`, `/schedule`, `/appointments`, and `/executive-summary`. Do not merge unlike currencies or infer a number when the API omits it. Reports have a 60-second request deadline and concurrency limits; cancel stale requests when filters change.

### Audit and integrations

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/audit` | Entity change log |
| GET | `/api/audit/staff-activity` | Request-level staff activity |
| GET | `/api/audit/staff-activity/{activityId}` | Activity detail |
| GET | `/api/audit/staff-activity/summary` | Activity summary |
| GET | `/api/audit/staff/{staffUserId}/timeline` | One staff timeline |
| GET | `/api/audit/locations/history` | Consented location audit history |
| GET | `/api/audit/locations/latest` | Latest staff location observations |
| GET | `/api/integrations/status` | Provider modes and readiness |
| GET | `/api/integrations/messages` | Provider message attempts |

## 6. Endpoint inventory for the dashboard

This is the route-level inventory. Use Swagger for the exact parameter schema.

### Company, identity and management

- Auth: `POST /api/auth/login`, `/refresh`, `/logout`, password and verification endpoints; `GET /api/auth/me`.
- Company: `GET|PUT /api/company/profile`; `GET /api/company/branches/controls`; `PUT /api/company/branches/{branchId}/control`.
- Dashboard: `GET /api/dashboard/overview`; `GET /api/dashboard/sales-performance`.
- Staff: `GET|POST /api/staff`; `GET|PUT|DELETE /api/staff/{userId}`; lifecycle routes `/suspend`, `/reactivate`, `/status`.
- Legacy migration: `GET /api/legacy-imports/template`; `POST /preview`; `GET /api/legacy-imports`; `GET /{batchId}`; `POST /{batchId}/commit`.

### Sales, customers and communications

- Sales read model: `GET /api/sales/summary`, `/invoices`, `/quotations`, `/receipts`, `/shipping-prices`.
- Managed prices: `POST|PATCH /api/sales/shipping-prices`; `/activate`;
  `/deactivate`; `/{id}/history`. See `MANAGED_SHIPPING_SERVICES.md`.
- Client records: `GET /api/clients`; `GET|PUT /api/clients/{customerId}`; duplicate review routes.
- Contacts: `GET|POST /api/contacts`; `GET|PUT /api/contacts/{id}`; `/summary`; typed directories; interaction routes.
- Communications: `POST /api/communications/clients/{customerId}/email`; `GET /api/communications/clients/{customerId}`; all `/api/communications/broadcasts` routes above.
- Quotes: `POST /api/operations/quotes`; `POST /api/operations/quotes/{quoteId}/email`.

### Operations

- `GET|POST /api/operations/branches`.
- `GET|POST /api/operations/routes`; `GET /api/operations/routes/{routeId}/rates`.
- `GET|POST /api/operations/rates`; `PUT|DELETE /api/operations/rates/{rateId}`.
- `GET|POST /api/operations/additional-service-pricing`; `PUT|DELETE /{id}`.
- Local only: `POST /api/operations/bookings/{bookingId}/local-payment-confirmation`; then `POST /receive`.
- `GET /api/operations/warehouse/summary`; `GET /inventory`; warehouse exception create/filter/detail/update/resolve/reconcile routes.
- Cargo milestone create/list/correct and carrier bind/sync routes.
- Consolidation-batch create/list/detail, add/remove cargo, freeze and release routes.
- Shipment movement create/list/detail/update/status/clients routes.
- Shipment allocation list/create/unassign routes.
- Container create/list/detail, cargo link, sync and manual-event routes.
- Shipment client list/update/timeline routes.
- One-screen operational manifest lookup/create/edit/print/email and Air/Ship date-group routes.
- Advanced batch manifest PDF/Excel/email routes and document registration.

### Money, stock and schedule

- Finance: immutable invoice create/list/detail/issue/email; payments; expenses and decisions; receipts, transactions, print and verify.
- Currency: latest rate, convert, country resolution and history.
- Purchasing: purchase create/list/detail/update/submit/decision/payout/receive, payouts and summary.
- Accounting: accounts, journals, trial balance, suppliers, supplier bills, purchase orders and bank accounts.
- Inventory: items, locations, stock, policies, adjustments, transfers, stock-in/out, ledger and summary.
- Appointments: list/create/detail/update/availability/status/response/reminder.
- Reports: executive, sales, purchases, inventory, contacts, schedule, appointments and inventory CSV.

### Retired or compatibility surfaces

Do not use these in a new dashboard unless migrating historical behavior: `/api/guests`, `/api/Cargo`, `/api/Customers`, and the older `/api/cargo/batches` controller. The supported navigation has no enquiry/lead/next-action workflow. Use `/api/clients`, `/api/operations/*`, `/api/purchasing`, `/api/inventory`, `/api/contacts`, `/api/appointments`, and `/api/sales` instead.

## 7. Query-cache and mutation rules

- Give each query a key containing every server filter, page, page size, selected currency and branch.
- Debounce free-text search by 250–400 ms and cancel the previous request.
- After a mutation, invalidate the exact detail, affected list, summary and dashboard keys. Do not clear the entire application cache.
- Use request IDs/idempotency keys for stock and receiving operations.
- Disable state-transition buttons while their mutation is pending.
- On `409`, refetch detail before presenting the next possible action.
- For queued broadcasts, carrier sync and other background work, poll only the specific detail endpoint and stop at a terminal state.

## 8. Release checklist for the dashboard team

- Generate client successfully from current Swagger JSON.
- Verify login, cookie refresh, logout and forced password change.
- Verify each role sees only its permitted navigation and handles `403` cleanly.
- Verify all large tables use server pagination.
- Verify invoice/receipt totals are displayed from the server and print views open correctly.
- Verify client duplicate dialog blocks unsafe creation.
- Verify email audience preview, draft, attachments, scheduling, counts, cancellation and failed-recipient retry.
- Keep a visible **MOCK PROVIDERS** label until `/api/integrations/status` reports live providers.
- Verify tracking corrections retain the audit trail and customer visibility rules.
- Verify no mixed-currency KPI is displayed as a single amount.
- Verify attendance kiosk does not store PIN or location beyond the immediate request.
- Include `X-Correlation-ID` in error reporting and support logs.
