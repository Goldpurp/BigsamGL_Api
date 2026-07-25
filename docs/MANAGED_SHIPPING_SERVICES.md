# BiGSAM managed shipping services

Last aligned: 24 July 2026

This is the shared contract for dashboard-managed cargo services and the customer-facing
rates page. PostgreSQL is the only source of truth. Frontends must not keep a second rate,
schedule, route or surcharge table in TypeScript, JSON, environment variables or browser
storage.

## Operating model

- Origin hubs are Manchester (`UK-MAN`, country `GB`), Lagos (`NG-LOS`, country `NG`) and
  Guangzhou (`CN-GZ`, country `CN`).
- Every other country on a service is a destination, not a BiGSAM origin branch.
- A `serviceCode` groups every price band, box, item and surcharge belonging to one
  origin/method/destination service.
- `isPublic=true`, `state=Active` records are visible to the public website.
- Deactivating or unpublishing a record removes it from new public estimates. Existing
  invoices keep immutable price snapshots.
- A service uses one currency. The API rejects a service whose active rows mix currencies.
- `DestinationCountryCodes` is a comma-separated list of ISO 3166-1 alpha-2 codes.

## Dashboard price administration

All routes require an active staff dashboard access token and `X-Active-Branch`.
`SuperAdmin`, `Administrator`, `OperationsManager`, `SalesManager`, `FinanceManager`
and `Staff` can view and manage the booking lists, pickup bookings and managed price
catalogue. Customer accounts cannot use these routes. A mutation requires one specific
assigned branch; only Super Admin can read across all branches.

The dashboard asks for confirmation before a price change. After three successfully
confirmed price changes on the same pricing-page visit, it enables fast editing. Every
successful change restarts a 10-minute inactivity timer. If no price is changed for 10
minutes, fast editing and its counter reset, and the next three changes require
confirmation again. API concurrency checks, change reasons, price history and staff
auditing remain mandatory for every fast edit. Pickup booking creates and edits always
receive a final confirmation because they include customer, address and schedule data.

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/sales/shipping-prices?page=1&pageSize=25` | Paginated catalogue |
| GET | `/api/sales/shipping-prices/categories` | Category/group filters |
| POST | `/api/sales/shipping-prices` | Create a managed price |
| PATCH | `/api/sales/shipping-prices/{id}` | Edit a managed price |
| POST | `/api/sales/shipping-prices/{id}/activate` | Make a price active |
| POST | `/api/sales/shipping-prices/{id}/deactivate` | Stop new use |
| GET | `/api/sales/shipping-prices/{id}/history` | Immutable change history |

Create requires `Idempotency-Key`. Edit, activate and deactivate require
`If-Match: "<rowVersion>"`. Every change requires a reason and is audited.

```json
{
  "serviceCode": "UK-NG-AIR-MAINLAND",
  "code": "UK-NG-AIR-MAINLAND-BASE",
  "category": "Air freight",
  "group": "Base rate",
  "name": "Lagos Mainland air freight",
  "description": "Greater of actual or volumetric weight.",
  "serviceArea": "UK to Lagos Mainland",
  "destinationCountryCodes": "NG",
  "destinationZone": "Lagos Mainland",
  "method": "Air",
  "currency": "GBP",
  "unit": "kg",
  "priceKind": "Fixed",
  "lowerPrice": 5.50,
  "upperPrice": null,
  "handlingFee": 20,
  "documentationFee": 0,
  "minimumCharge": null,
  "minimumQuantity": 10,
  "maximumQuantity": null,
  "dispatchSchedule": "Every Thursday; 16:00 UK cut-off",
  "estimatedDelivery": "5–7 working days to Lagos",
  "isPublic": true,
  "displayOrder": 0,
  "reason": "Approved July rate update"
}
```

Supported units are `kg`, `cbm`, `box`, `item`, `pallet`, `vehicle`, `shipment`,
`service`, `surcharge` and `custom`. Supported methods are `Air`, `Sea`, `Road` and
`Other`. Customer shipment creation currently accepts Air and Sea.

Use `group="Base rate"` for the primary quantity band. Other groups are selectable
extras such as boxes, special items and regulatory surcharges. Use separate rows with the
same `serviceCode` and different minimum/maximum quantities for tiered pricing.

## Public website

### List published services

`GET /api/public/shipping-services`

Optional query parameters:

- `originCountryCode=GB|NG|CN`
- `destinationCountryCode=<ISO alpha-2>`
- `method=Air|Sea|Road|Other`

The response groups catalogue rows by service and returns hub details, destination codes,
currency, schedule, delivery estimate and ordered prices. Responses can be cached for up
to 60 seconds. The website should show loading/error states and must not fall back to stale
hardcoded money values.

### Calculate a public estimate

`POST /api/public/shipping-services/estimate`

Air example:

```json
{
  "serviceCode": "UK-NG-AIR-MAINLAND",
  "actualWeightKg": 12,
  "lengthCm": 50,
  "widthCm": 40,
  "heightCm": 30,
  "selections": [
    { "code": "UK-NG-AIR-MAINLAND-NAFDAC", "quantity": 1 }
  ]
}
```

Sea box/item example:

```json
{
  "serviceCode": "UK-NG-SEA",
  "actualWeightKg": 0,
  "lengthCm": null,
  "widthCm": null,
  "heightCm": null,
  "selections": [
    { "code": "UK-NG-SEA-BOX-M", "quantity": 2 }
  ]
}
```

The response contains `currency`, actual/volumetric/CBM values, billable quantity,
handling and documentation fees, `estimatedTotal` and invoice-style lines. Air
volumetric weight is `length × width × height / 5000`; the API bills the greater of
actual, volumetric and the configured minimum. CBM services use
`length × width × height / 1,000,000`.

The browser may preview inputs but the returned estimate is authoritative. A
`CustomQuote` price returns an error and must be handled by staff as a negotiated
shipment.

## Creating a customer shipment from the dashboard

`POST /api/customer-shipments` creates one customer shipment and its immutable
`BGL…INV` sales invoice atomically.

For a normal managed shipment:

- send `pricing.serviceCode`;
- send actual weight and dimensions where applicable;
- send selected catalogue IDs in `pricing.boxItems` for boxes, items and surcharges;
- do not calculate or trust `invoiceLines` as the final price;
- use the currency returned for the service;
- select a destination country included in the service.

```json
{
  "pricing": {
    "method": "Weight",
    "actualWeightKg": 12,
    "chargeableWeightKg": 12,
    "boxItems": [],
    "agreedAmount": null,
    "negotiatedReason": null,
    "serviceCode": "UK-NG-AIR-MAINLAND",
    "lengthCm": 50,
    "widthCm": 40,
    "heightCm": 30
  }
}
```

The API reloads active catalogue rows, checks hub, method, destination, bands and
currency, recalculates the amount, creates invoice lines and stores immutable price
components. A frontend-supplied standard total cannot override this calculation.

For a machine, oversized cargo or another specially agreed item, use
`pricing.method="NegotiatedPrice"`, omit `serviceCode`, and send a positive
`agreedAmount` with a meaningful `negotiatedReason`. This is the only manual-price path.

## Update propagation

1. An authorised staff member edits a price or schedule in Sales → Service pricing.
2. PostgreSQL stores the new row version and audit history.
3. `/api/public/shipping-services` reflects a published active change within its
   60-second cache window.
4. The dashboard New shipment form reads the same grouped service.
5. The API uses the latest active values for new estimates and invoices.
6. Historic invoices and shipment price components remain unchanged.

This separation allows the catalogue to grow without editing either frontend whenever a
country, service, band, schedule or surcharge changes.
