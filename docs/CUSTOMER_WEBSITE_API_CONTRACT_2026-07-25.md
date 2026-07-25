# BiGSAM customer website active API contract

Last aligned: 25 July 2026

This is the implementation contract for `/Users/mac/Desktop/bigsam-global-logistics`.
It lists only the API surface used by the current customer-facing application.
The longer historical integration guide remains design history and must not be
used to add retired booking, warehouse, wallet, attendance, or staff workflows
to the public website.

## Environment and transport

- Local website: `http://127.0.0.1:3000`.
- Local API: `http://127.0.0.1:5120`.
- Production uses the configured HTTPS API origin.
- Every call sends `Accept: application/json`.
- Every call sends `X-BiGSAM-App-Environment: local|production`.
- The API returns `X-BiGSAM-API-Environment`; an environment mismatch is an
  application error.
- Customer access tokens are held in memory. Refresh tokens are secure
  `HttpOnly` cookies and are never read by JavaScript.
- Authenticated calls send `Authorization: Bearer <accessToken>` and
  `credentials: include`.
- A `401` may trigger one refresh and one retry. Never create an infinite retry
  loop.
- Render RFC 7807 `detail`, field errors, or `title`; do not expose stack traces.

## Public prices

### `GET /api/public/shipping-services`

Returns the services and prices published from the staff dashboard:

```ts
type PublicShippingService = {
  serviceCode: string;
  originHubId: string;
  originHubCode: string;
  originHubName: string;
  originCountryCode: string;
  serviceArea: string;
  destinationZone: string;
  destinationCountryCodes: string[];
  method: "Air" | "Sea" | "Other";
  currency: string;
  dispatchSchedule?: string | null;
  estimatedDelivery?: string | null;
  prices: Array<{
    id: string;
    code: string;
    name: string;
    category: string;
    group: string;
    description: string;
    unit: string;
    priceKind: "Fixed" | "From" | "Range" | "CustomQuote";
    price?: number | null;
    upperPrice?: number | null;
    minimumQuantity?: number | null;
    maximumQuantity?: number | null;
    handlingFee?: number | null;
    documentationFee?: number | null;
    minimumCharge?: number | null;
    displayOrder: number;
  }>;
};
```

Do not keep a second hard-coded price list in the website. `CustomQuote` means
the customer must contact BiGSAM; it is not a zero-price service.

### `POST /api/public/shipping-services/estimate`

```json
{
  "serviceCode": "UK-NG-AIR",
  "actualWeightKg": 12.5,
  "lengthCm": 60,
  "widthCm": 40,
  "heightCm": 40,
  "selections": [
    { "code": "BATTERY", "quantity": 1 }
  ]
}
```

For Air, the backend compares actual and volumetric weight. For Sea, selections
represent managed boxes/items. Negotiated machines and unusual cargo do not
receive a fabricated automatic total.

## Public tracking

The website selects one of these routes from the submitted reference:

- Invoice matching `BGL<digits>INV`:
  `GET /api/public/tracking/invoice/{invoiceNumber}`.
- Public token with 32–128 hexadecimal characters:
  `GET /api/public/tracking/{publicToken}`.
- Other BiGSAM tracking reference:
  `GET /api/tracking/{trackingNumber}`.

An invoice lookup is the preferred customer journey. The public response uses
only staff-approved BiGSAM wording and never exposes the MSC container, bill of
lading, provider credentials, raw payload, internal note, cost, or payment data.
`404` means “not found”; other failures mean tracking is temporarily
unavailable.

The manifest tracking response is:

```ts
type PublicManifestTracking = {
  trackingNumber: string;
  shipmentName: string;
  method: "Air" | "Sea";
  origin: string;
  destination: string;
  serviceName: string;
  status: string;
  lastKnownPlace?: string | null;
  estimatedAt?: string | null;
  events: Array<{
    title: string;
    description: string;
    place?: string | null;
    occurredAt: string;
    estimatedAt?: string | null;
  }>;
};
```

Events are already filtered for customer visibility. Do not merge or display
MSC data directly in browser code.

## Customer authentication

### Register

`POST /api/auth/register`

```json
{
  "fullName": "Ada Customer",
  "email": "ada@example.com",
  "password": "StrongPassword#2026",
  "countryCode": "NG"
}
```

The backend normalizes name/email, enforces unique client identity, and does not
return another customer’s record on a duplicate. The website must not provide a
“continue anyway” option.

### Login and session lifecycle

- `POST /api/auth/login` with `{ "email", "password" }`.
- `POST /api/auth/refresh` with `{}` and cookies included.
- `POST /api/auth/logout` with `{}`.
- `GET /api/auth/sessions`.
- `DELETE /api/auth/sessions/{sessionId}`.

Login/register/refresh return:

```ts
type AuthSession = {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: {
    id: string;
    fullName: string;
    email: string;
    role: "Customer";
    customerId: string;
    emailVerified: boolean;
    mustChangePassword: boolean;
  };
};
```

Reject a session whose role is not `Customer` or which has no `customerId`.

### Password and verification

- `POST /api/auth/change-password`.
- `POST /api/auth/forgot-password`.
- `POST /api/auth/reset-password`.
- `POST /api/auth/email-verification/request`.
- `POST /api/auth/email-verification/confirm`.

Passwords use the server policy shown by the UI. Reset/verification tokens are
handled only on the account action route and must not be logged.

## Customer portal

### `GET /api/customer-portal`

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
    companyName?: string | null;
    isBusiness: boolean;
    createdAt: string;
    accountType: string;
  };
  activeShipments: number;
  deliveredShipments: number;
  recentShipments: Array<{
    id: string;
    trackingNumber: string;
    invoiceNumber?: string | null;
    markerCode: string;
    description: string;
    status: string;
    billableWeightKg: number;
    outstandingShippingCost: number;
    currency: string;
    createdAt: string;
    method?: "Air" | "Sea" | null;
    origin?: string | null;
    destination?: string | null;
    handoverMethod?: "Pickup" | "Delivery" | null;
    pieceCount: number;
  }>;
};
```

Canonical invoice-linked customer shipments are returned first. Orphaned legacy
cargo is merged only for continuity and never duplicated. `trackingNumber` is
the immutable invoice number for canonical shipments. The summary is limited to
20 recent shipments.

### Profile

`PATCH /api/customer-portal/profile`

```json
{
  "fullName": "Ada Customer",
  "phone": "+2348000000000",
  "companyName": "Ada Trading Ltd",
  "isBusiness": true
}
```

Email is not editable from this endpoint.

### Notification preferences

- `GET /api/customer-portal/notification-preferences`.
- `PUT /api/customer-portal/notification-preferences`.

```ts
type NotificationSettings = {
  emailEnabled: boolean;
  smsEnabled: boolean;
  whatsAppEnabled: boolean;
  operationalUpdates: boolean;
  marketingUpdates: boolean;
};
```

The server is authoritative for consent. The customer website never sends an
administrator broadcast.

## News

`GET /api/news?limit=48` returns `{ items, updatedAt, isStale, sourceCount }`.
The website accepts article/image URLs only from its explicit HTTPS allow-list.
News failure must not affect prices, tracking, or customer login.

## Production acceptance

- TypeScript, local build, and production build must pass.
- Public pages render with the API unavailable and show clear temporary states.
- No public bundle contains staff endpoints, secrets, MSC credentials, database
  strings, customer lists, or attachment bytes.
- Prices come from the public catalogue, not local constants.
- Tracking by invoice shows only published BiGSAM movements.
- A signed-in customer sees only the customer ID carried by the validated token.
- Multi-currency values keep the API currency; the browser never invents a rate.
- Images and non-critical views are lazy-loaded; requests are cancellable and
  bounded by the shared timeout.
