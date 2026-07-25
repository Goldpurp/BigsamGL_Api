# BigsamGL customer website — backend integration contract

**Audience:** the team building the public website and signed-in customer portal  
**Backend:** Bigsam Global Logistics API v1  
**Contract date:** 2026-07-24  
**Local API:** `http://127.0.0.1:5120`  
**OpenAPI JSON:** `http://127.0.0.1:5120/swagger/v1/swagger.json`

This is the customer-facing handoff. Use only the public and `Customer`-authorized endpoints listed here. The internal dashboard APIs contain private company, staff, finance, supplier and audit data and must not be called from the customer website.

## 1. What the customer website can deliver now

### Public, no account required

- List active branches.
- List active service routes and live effective freight-rate cards.
- Calculate and submit a shipping quotation.
- Track a cargo item using its BigsamGL tracking number.
- Verify an invoice, receipt or payment-voucher number using its security code.
- Register, sign in, reset a password and verify an email address.

### Signed-in customer

- View and update the customer’s own profile.
- Create and list the customer’s own collection/delivery addresses.
- Create a quotation that is permanently linked to the account.
- List the customer’s linked quotations.
- Convert a valid linked quotation into a booking.
- List the customer’s bookings.
- See active/delivered shipment counts and recent shipments.
- See detailed tracking and receiver information only for cargo owned by that customer.
- Control email/SMS/WhatsApp, operational and marketing preferences.

### Not yet a customer-site feature

- The backend does not currently expose a customer-owned invoice/receipt register. Invoices are delivered by the staff email workflow and can be minimally verified through the public verification endpoint.
- Real online payment is not connected. Booking payment sessions are mock data and staff can confirm them only through an internal route.
- Price administration is internal. Public rate displays must use
  `/api/public/shipping-services` and its estimate endpoint; never copy monetary values
  into frontend source code.
- Email broadcasts are an administrator dashboard feature. The customer website only supplies notification preferences used to decide whether a client is eligible.

Do not silently expose internal APIs to fill any of these gaps. Add a dedicated customer-owned backend route in a later version.

## 2. Base URL, JSON and browser configuration

- Production must set its own HTTPS API base URL through environment configuration.
- JSON fields are camel case.
- Enums are returned and accepted as strings.
- IDs are UUID strings.
- Dates are UTC ISO-8601.
- Amounts always include a currency; render with `Intl.NumberFormat`.
- Send `credentials: "include"` so the `HttpOnly` refresh cookie is included.
- Keep the access JWT in application memory. Do not put access or refresh tokens in local/session storage.
- The frontend origin must be listed in the backend `Cors:AllowedOrigins` production setting.

Suggested environment variable:

```env
NEXT_PUBLIC_BIGSAM_API_URL=https://api.example.com
```

Suggested request wrapper:

```ts
async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, {
    ...init,
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      ...init.headers,
    },
  });
  if (!response.ok) throw await response.json();
  if (response.status === 204) return undefined as T;
  return await response.json() as T;
}
```

## 3. Customer authentication

### Register

`POST /api/auth/register`

```json
{
  "fullName": "Ada Customer",
  "email": "ada@example.com",
  "password": "a-strong-password",
  "countryCode": "NG"
}
```

The backend normalizes the identity, creates the linked customer record and enforces unique client data. Do not provide a “continue anyway” option when the API reports an existing/conflicting identity. Public registration must not reveal another customer’s details.

Successful registration returns:

```json
{
  "accessToken": "jwt",
  "accessTokenExpiresAt": "2026-07-16T12:30:00Z",
  "user": {
    "id": "uuid",
    "fullName": "Ada Customer",
    "email": "ada@example.com",
    "role": "Customer",
    "customerId": "customer-uuid",
    "emailVerified": false,
    "mustChangePassword": false
  }
}
```

The response sets the `HttpOnly` `bigsam_refresh` cookie.

### Sign in

`POST /api/auth/login`

```json
{
  "email": "ada@example.com",
  "password": "a-strong-password"
}
```

If `user.role !== "Customer"`, do not load the customer portal; direct staff to the separate dashboard.

### Restore, refresh and log out

| Method | Path | Use |
|---|---|---|
| GET | `/api/auth/me` | Restore current user from access token |
| POST | `/api/auth/refresh` | Rotate session using refresh cookie |
| POST | `/api/auth/logout` | Revoke session and clear refresh cookie |

On one `401`, call `/api/auth/refresh` once, replace the in-memory token and replay once. If refresh fails, clear customer state and send the user to sign-in. Never loop refresh requests.

### Email verification and password reset

| Method | Path | Body |
|---|---|---|
| POST | `/api/auth/email-verification/request` | none; requires customer token |
| POST | `/api/auth/email-verification/confirm` | `{ "token": "..." }` |
| POST | `/api/auth/password-reset/request` | `{ "email": "ada@example.com" }` |
| POST | `/api/auth/password-reset/confirm` | `{ "token": "...", "newPassword": "..." }` |
| POST | `/api/auth/password/change` | `{ "currentPassword": "...", "newPassword": "..." }` |

Development can return `mockToken` to help local testing. Do not display it in production.

## 4. Public service and pricing pages

The current customer-facing catalogue contract is documented in
[`MANAGED_SHIPPING_SERVICES.md`](MANAGED_SHIPPING_SERVICES.md). Use
`GET /api/public/shipping-services` for the Rates page and
`POST /api/public/shipping-services/estimate` for its calculator. These endpoints group
the dashboard-managed PostgreSQL catalogue by service code and expose only active,
published records.

### Active branches

`GET /api/operations/branches`

Use this for public office/hub selectors. The branch object includes ID, code, name, country code, timezone, address and active state. Do not hard-code branch IDs.

### Active service routes

`GET /api/operations/routes`

Each route includes:

```ts
type PublicRoute = {
  id: string;
  code: string;
  name: string;
  mode: "Air" | "Sea" | "Road" | "Courier";
  transitDaysMin: number;
  transitDaysMax: number;
  weeklyDepartureDay: string | null;
  origin: string;
  destination: string;
  acceptingBookings: boolean;
  operationalNotice: string | null;
};
```

Disable the quote/booking CTA when `acceptingBookings` is false and display `operationalNotice` when present.

### Effective freight rates for a route

`GET /api/operations/routes/{routeId}/rates`

```ts
type PublicRateCard = {
  id: string;
  cargoCategory: string;
  minimumWeightKg: number;
  ratePerKg: number;
  minimumCharge: number;
  fuelSurchargeRate: number;
  customsRate: number;
  currency: string;
  effectiveFrom: string;
  effectiveTo: string | null;
};
```

These rates are informational. Always use the response from quotation creation as the binding calculated result. Do not duplicate the billable-weight formula in the browser as the final price.

## 5. Quotation flow

### Request

`POST /api/operations/quotes`

This route is public, but it behaves differently based on the bearer token:

- With a signed-in `Customer` token, the quotation is linked to that customer and appears in `/quotes/my`.
- Without a token, the quotation is anonymous and emailed through the mock integration, but it cannot later be booked through the customer endpoint.

For a smooth quote-to-booking flow, ask the user to sign in or register before final quotation submission. A public visitor may still request a standalone quote.

```json
{
  "customerId": null,
  "serviceRouteId": "route-uuid",
  "salesLeadId": null,
  "contactName": "Ada Customer",
  "contactEmail": "ada@example.com",
  "contactPhone": "+2348000000000",
  "cargoDescription": "Two cartons of clothing",
  "cargoCategory": "General",
  "actualWeightKg": 18.5,
  "lengthCm": 60,
  "widthCm": 45,
  "heightCm": 40,
  "declaredValue": 250000
}
```

All weight and dimensions must be greater than zero. `cargoCategory` must match an active rate card for the selected route.

### Response

The response is `201 Created`:

```json
{
  "quote": {
    "id": "quote-uuid",
    "quoteNumber": "BGL-QT-20260716-ABC123",
    "customerId": "customer-uuid-or-null",
    "serviceRouteId": "route-uuid",
    "status": "Sent",
    "contactName": "Ada Customer",
    "cargoDescription": "Two cartons of clothing",
    "cargoCategory": "General",
    "actualWeightKg": 18.5,
    "volumetricWeightKg": 21.6,
    "billableWeightKg": 21.6,
    "baseAmount": 100000,
    "fuelSurchargeAmount": 10000,
    "customsAmount": 5000,
    "totalAmount": 115000,
    "currency": "NGN",
    "expiresAt": "2026-07-30T12:00:00Z"
  },
  "localTotal": null
}
```

The figures above are illustrative. Display actual, volumetric and billable weight separately. Display `localTotal` when the API converts a foreign rate to destination-local NGN or CNY; keep the source currency total visible too.

### Customer quotation list

`GET /api/operations/quotes/my?paged=true&page=1&pageSize=25&includeTotal=true`

Quote states: `Draft`, `Sent`, `Accepted`, `Rejected`, `Expired`, `Converted`.

Only quotations created while authenticated are returned. Do not let a customer type an arbitrary quote ID to claim it.

## 6. Address book

All endpoints in this section require a customer bearer token.

### List

`GET /api/operations/addresses`

### Create

`POST /api/operations/addresses`

```json
{
  "type": "Collection",
  "contactName": "Ada Customer",
  "phone": "+2348000000000",
  "line1": "10 Example Street",
  "line2": null,
  "city": "Lagos",
  "state": "Lagos",
  "postalCode": "100001",
  "countryCode": "NG",
  "isDefault": true
}
```

Address types are `Billing`, `Collection`, `Delivery`, `Warehouse`. Creating a default address unsets the previous default of the same type.

## 7. Booking flow

`POST /api/operations/bookings` requires a customer token and a valid quote owned by that customer.

```json
{
  "quoteId": "quote-uuid",
  "collectionAddressId": "customer-address-uuid",
  "deliveryAddressId": "different-customer-address-uuid-or-null",
  "requestedCollectionAt": "2026-07-18T09:00:00Z",
  "pieceCount": 2,
  "declaredValue": 250000,
  "declaredValueCurrency": "NGN",
  "prohibitedItemsConfirmed": true,
  "deliveryMethod": "AddressDelivery",
  "receiver": {
    "fullName": "Receiver Name",
    "phone": "+2348111111111",
    "email": "receiver@example.com",
    "invoiceNumber": "BGL0000INV",
    "pickupBranchId": null,
    "addressLine1": "22 Destination Road",
    "addressLine2": null,
    "city": "Abuja",
    "state": "FCT",
    "postalCode": "900001",
    "countryCode": "NG"
  }
}
```

Receiver rules:

- Receiver name, phone, valid email and invoice number are always required.
- `AddressDelivery` requires a delivery address different from the collection address, plus receiver address line 1, city and country.
- `HubPickup` requires a valid `pickupBranchId`; delivery address fields may be null.
- The collection and delivery address IDs must belong to the signed-in customer.
- The prohibited-items confirmation must be true.
- The quote must be unexpired and not rejected/expired.
- The origin branch must currently accept bookings.

The response is `201 Created` and includes the booking, a mock payment session and `mockNotice: "No real payment is processed."`. The booking begins as `AwaitingPayment`.

Booking states: `Draft`, `AwaitingPayment`, `Confirmed`, `ReceivedAtWarehouse`, `Consolidated`, `Dispatched`, `Delivered`, `Cancelled`.

Do not show a “Payment successful” page from this mock response. Show “Payment setup is in test mode” until the integration-status endpoint reports a live payment provider.

### Customer booking list

`GET /api/operations/bookings/my?paged=true&page=1&pageSize=25&includeTotal=true`

Use the server state to choose actions. The customer cannot confirm payment or receive cargo; those are internal staff actions.

## 8. Customer portal home

`GET /api/customer-portal`

Response shape:

```ts
type CustomerPortal = {
  customer: {
    id: string;
    fullName: string;
    email: string;
    walletBalance: number;
    walletCurrency: string;
    countryCode: string;
    phone: string;
    companyName: string | null;
    isBusiness: boolean;
    createdAt: string;
    accountType: "Individual" | "Business" | "PartnerAgency";
  };
  activeShipments: number;
  deliveredShipments: number;
  recentShipments: Array<{
    id: string;
    trackingNumber: string;
    markerCode: string;
    description: string;
    status: CargoStatus;
    billableWeightKg: number;
    outstandingShippingCost: number;
    currency: string;
    createdAt: string;
  }>;
};
```

`recentShipments` is limited to the latest 20. Treat this as a portal summary, not an infinite shipment archive.

### Update profile

`PATCH /api/customer-portal/profile`

```json
{
  "fullName": "Ada Customer",
  "phone": "+2348000000000",
  "companyName": "Ada Trading Ltd",
  "isBusiness": true,
  "accountType": "Business"
}
```

Account types: `Individual`, `Business`, `PartnerAgency`. Email changes are not supported by this profile route; do not render an editable email field.

## 9. Tracking

### Public tracking

`GET /api/Tracking/{trackingNumber}`

No account is required. The response is deliberately customer-safe:

```ts
type PublicTrackingResponse = {
  trackingNumber: string;
  markerCode: string;
  description: string;
  destinationCountry: string;
  customerFacingStatus: string;
  publicMilestoneDetails: string;
  lastUpdated: string;
  currentLocation: string;
  estimatedDeliveryAt: string | null;
  hasException: boolean;
  issueCode: string | null;
  deliveryMethod: "HubPickup" | "AddressDelivery";
  milestones: Array<{
    id: string;
    occurredAt: string;
    status: string;
    eventType: ShipmentEventType;
    title: string;
    description: string;
    location: string;
    source: string;
    estimatedDeliveryAt: string | null;
    isException: boolean;
    issueCode: string | null;
    isCorrected: boolean;
  }>;
};
```

Show only this response. Do not call internal milestone or warehouse endpoints from the public tracking page. Keep corrected events visible as history or mark them as corrected; do not rewrite the timeline in the browser.

Cargo statuses used internally are: `Logged`, `ReceivedAtWarehouse`, `AssignedToBatch`, `InTransit`, `ArrivedAtDestinationHub`, `OutForDelivery`, `Delivered`, `Cancelled`, `PickupScheduled`, `PickedUp`, `IncomingToWarehouse`, `Delayed`, `OnHold`. Prefer `customerFacingStatus` for the main public label.

For an invoiced air/sea manifest, customers use their immutable BiGSAM invoice:

`GET /api/public/tracking/invoice/{invoiceNumber}`

```ts
type PublicManifestTrackingResponse = {
  trackingNumber: string; // the BiGSAM BGL…INV number
  shipmentName: string;
  method: "Air" | "Sea";
  origin: string;
  destination: string;
  serviceName: string;
  status: string;
  lastKnownPlace: string | null;
  estimatedAt: string | null;
  events: Array<{
    title: string;
    description: string;
    place: string | null;
    occurredAt: string;
    estimatedAt: string | null;
  }>;
};
```

This response contains only events reviewed and published by BiGSAM staff. It
never exposes the MSC container, bill-of-lading, booking reference, provider
status, raw payload or draft event. Do not ask customers for the MSC reference.

### Signed-in shipment tracking

`GET /api/customer-portal/shipments/{cargoId}/tracking`

The cargo ID must belong to the signed-in customer. This response combines:

- minimal client identity: name, number and email;
- the same customer-visible tracking model;
- receiver details, invoice number, delivery method and address or pickup branch.

Never use a cargo ID supplied from another account. A non-owned ID returns not found.

## 10. Notification preferences and broadcast consent

### Read

`GET /api/customer-portal/notification-preferences`

### Update

`PUT /api/customer-portal/notification-preferences`

```json
{
  "emailEnabled": true,
  "smsEnabled": true,
  "whatsAppEnabled": false,
  "operationalUpdates": true,
  "marketingUpdates": false
}
```

Recommended settings UI:

- **Email notifications** — master eligibility for client email.
- **Operational shipment updates** — tracking/delay/service messages.
- **Marketing and offers** — optional and off until the customer explicitly opts in.
- SMS and WhatsApp toggles — stored now for future provider integrations.

Administrator broadcasts consult these preferences. A client without a preference record is eligible for ordinary/operational email by default but is not eligible for marketing email. Store the complete preference response after each update; do not infer consent locally.

## 11. Public financial-document verification

`GET /api/finance/documents/verify?documentNumber=BGL0000INV&verificationCode=ABC123`

Success returns only minimal authenticity data:

```json
{
  "valid": true,
  "kind": "Sales",
  "number": "BGL0000INV",
  "status": "Issued",
  "amount": 115000,
  "currency": "NGN",
  "date": "2026-07-16T00:00:00Z"
}
```

An invalid pair returns `404` with `{ "valid": false }`. Do not treat the verification code as a login credential, and do not reveal customer identity on this public screen.

## 12. Error, traffic and loading behavior

### Problem response

```ts
type ApiProblem = {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  errors?: Record<string, string[]>;
};
```

| HTTP | Customer-site response |
|---|---|
| `400` | Show input/field error |
| `401` | One refresh attempt, then sign-in |
| `403` | Do not expose the page/action |
| `404` | Friendly not-found/invalid tracking result |
| `409` | Refresh state and explain quote/booking conflict |
| `422` | Show the logistics business explanation |
| `429` | Honor `Retry-After`; disable rapid resubmission |
| `500` | Generic message; do not show stack details |

The API has a global per-caller traffic limit, plus stricter named limits on authentication, public tracking, public contact writes and attendance. Do not hard-code those server limits into UI behavior: handle `429` and its `Retry-After` header. Disable quote/register/booking buttons while pending and do not auto-retry writes. Debounce tracking input and cancel stale GET requests.

Paginated responses:

```ts
type PagedResponse<T> = {
  items: T[];
  page: number;
  pageSize: number;
  hasPrevious: boolean;
  hasNext: boolean;
  total: number | null;
  totalPages: number | null;
};
```

Use `paged=true&page=1&pageSize=25&includeTotal=true` for `/quotes/my` and `/bookings/my`.

## 13. Recommended customer-site routes

| Frontend route | Backend calls |
|---|---|
| `/services` | `GET /api/operations/routes`, branch list |
| `/rates` | `GET /api/public/shipping-services`; `POST /api/public/shipping-services/estimate` |
| `/quote` | Managed services for rate discovery, then `POST /api/operations/quotes` for the existing quotation workflow |
| `/track` | For `BGL…INV`: `GET /api/public/tracking/invoice/{invoiceNumber}`; otherwise `GET /api/Tracking/{trackingNumber}` |
| `/verify` | `GET /api/finance/documents/verify` |
| `/register` | `POST /api/auth/register` |
| `/login` | `POST /api/auth/login` |
| `/forgot-password` | Reset request/confirm routes |
| `/account` | `GET /api/customer-portal` |
| `/account/profile` | Portal GET and profile PATCH |
| `/account/addresses` | Address GET/POST |
| `/account/quotes` | `GET /api/operations/quotes/my` |
| `/account/bookings` | Booking GET/POST |
| `/account/shipments/{cargoId}` | Signed-in shipment tracking |
| `/account/notifications` | Notification preference GET/PUT |

## 14. Security and privacy requirements

- Never bundle staff, finance, audit, supplier, warehouse-exception or broadcast endpoints into customer UI code.
- Never accept a `customerId` from the browser for signed-in ownership. Customer-owned endpoints use the JWT `customer_id` claim.
- Never store JWTs, refresh cookies, passwords, PINs or verification codes in analytics.
- Never place email, phone, address, token or password values in URLs. The verification screen is the sole exception for its document number/code contract; suppress that URL from analytics and referrer capture.
- Mask personal data in client-side error reporting.
- Use HTTPS only in production.
- Do not calculate an authoritative quote, invoice or tracking status in the browser.
- Do not claim a payment, message, SMS, WhatsApp or MSC update is live while integrations report `Mock`.

## 15. Customer-site acceptance checklist

- Registration creates a `Customer` user and restores through the cookie/session flow.
- Staff accounts cannot enter the customer portal accidentally.
- Route notices and booking suspension are respected.
- Signed-in quotation appears in `/quotes/my`; anonymous quotation is clearly standalone.
- A booking cannot use another customer’s quote or address.
- Hub-pickup and address-delivery receiver validation both work.
- Mock payment is visibly labeled test mode.
- Public tracking shows only customer-safe milestones.
- Invoiced manifest tracking accepts the BiGSAM invoice and never exposes the MSC reference.
- Signed-in tracking exposes receiver detail only for owned cargo.
- Marketing consent defaults off and updates from the server response.
- Verification does not reveal customer/staff identity.
- All user-facing list screens paginate and show skeleton/empty/error states.
- No write request is automatically duplicated after timeout or `429`.
- Production CORS, HTTPS, API URL and live provider settings are verified before launch.
