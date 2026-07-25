# MSC Direct Tracking

BiGSAM keeps the complete tracking experience inside its dashboard and customer
website. Staff connect an MSC container, bill of lading, or booking number to a
manifest; the backend then imports MSC movements, removes duplicates, and stores
them as reviewable events. No user is redirected to MSC.

## Supported workflow

1. Connect the reference with
   `PUT /api/operations/tracking/{manifestId}/connection`.
2. Fetch MSC movements immediately with
   `POST /api/operations/tracking/{manifestId}/sync`. In live production mode,
   the background synchronizer also refreshes due connections automatically.
3. Review the new draft movements and prepare customer-safe wording.
4. Publish approved movements.
5. The customer website reads only published movements using the immutable
   BiGSAM invoice number at
   `GET /api/public/tracking/invoice/{invoiceNumber}`. A private share link can
   instead use `GET /api/public/tracking/{publicToken}`.

The customer response deliberately identifies the shipment as a BiGSAM service.
It never returns the MSC container, bill-of-lading, booking reference, raw
carrier response, or draft events.

Every mutating staff request requires a UUID `Idempotency-Key`. Updating an
existing connection also requires its current `ETag` value in `If-Match`.

Example connection body:

```json
{
  "provider": "MSC",
  "shippingCompanyName": "Mediterranean Shipping Company",
  "externalReference": "MSCU1234567",
  "referenceType": "ContainerNumber",
  "reason": "Attach the carrier reference supplied for this sea shipment"
}
```

`referenceType` accepts `ContainerNumber`, `BillOfLading`, or `BookingNumber`.
The same MSC container can be connected to more than one BiGSAM manifest because
consolidated cargo can serve multiple internal shipment records.

## Local and production modes

`MscTracking:Mode=Mock` returns deterministic DCSA-shaped movements so the
dashboard can be developed and demonstrated without third-party credentials.

Production must use `MscTracking:Mode=Live`. Configure the MSC-issued URL as
`MscTracking:EventsUrlTemplate`. It must contain `{reference}` or the matching
typed placeholder:

- `{containerNumber}`
- `{transportDocumentReference}`
- `{carrierBookingReference}`

Authentication supports either an MSC-issued bearer `ApiToken` or OAuth client
credentials (`OAuthTokenUrl`, `ClientId`, `ClientSecret`, and optional `Scope`).
If the MSC onboarding package requires a subscription key, configure
`SubscriptionKeyHeader` and `SubscriptionKey`.

Set `AutomaticSyncEnabled=true` in live production. `WorkerScanSeconds` controls
how often the service finds due connections, `WorkerBatchSize` bounds each scan,
and `FailureRetryMinutes` controls the backoff after an MSC outage. Each
successful connection is next checked in 30 minutes. Imported events are drafts;
automatic synchronization never publishes customer updates.

Do not store live secrets in `appsettings.json`. Supply them through the
production secret store or environment variables.

## Data integrity and security

- Provider event IDs are unique per manifest connection, so repeated syncs do
  not duplicate movements.
- A PostgreSQL advisory lock serializes concurrent sync writes for the same
  manifest, while the final event batch is saved atomically.
- The carrier pull endpoint is limited to four requests per second, matching
  MSC's documented integration limit.
- Raw carrier payloads are encrypted with ASP.NET Data Protection before being
  stored and are never returned through staff or public tracking responses.
- Imported movements remain drafts until a staff member reviews and publishes
  them. Customer tracking never exposes internal notes or unapproved carrier
  data.

MSC provides UAT and production credentials only after its Integration Suite
onboarding. Live mode deliberately returns `503 Service Unavailable` until those
issued values are configured; the application does not scrape MSC web pages.
