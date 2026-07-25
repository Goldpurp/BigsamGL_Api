# Currency and MSC tracking integration

## Currency behavior

`ICurrencyExchangeService` is the single conversion boundary. It requests the latest provider-published rate, stores an immutable PostgreSQL snapshot, and reuses a fresh snapshot for the configured cache period. If the provider is temporarily unavailable it may use a recent stored snapshot. Static configured fallbacks are permitted only when `ExchangeRates:AllowConfiguredFallback` is explicitly enabled; production configuration disables them.

Nigeria maps to NGN and China maps to CNY. Local invoices and branch expenses are validated against that rule. A quotation retains its contractual source currency and returns a separate `localTotal`, preventing a conversion from silently changing the original document amount.

The Nigerian rate request selects the CBN provider through Frankfurter. The CBN publishes official daily Nigerian Foreign Exchange Market rates, so “real time” here means the latest published authoritative rate rather than a tick-by-tick trading feed.

Configuration:

```text
ExchangeRates__BaseUrl=https://api.frankfurter.dev
ExchangeRates__AllowConfiguredFallback=false
```

Endpoints:

- `GET /api/finance/currency/rates/{base}/{quote}`
- `POST /api/finance/currency/convert`
- `GET /api/finance/currency/country/{countryCode}`
- `GET /api/finance/currency/history`

## MSC container flow

MSC tracking is isolated behind `IMscTrackingGateway`, so provider changes do not alter cargo, customer, or public tracking APIs.

1. Staff registers the container through `POST /api/operations/containers` and links a shipping batch or selected cargo.
2. `POST /api/operations/containers/{id}/sync` invokes the MSC adapter.
3. The adapter parses DCSA-style equipment, transport, and shipment events and stores the raw provider payload plus normalized event data.
4. Each new provider event is propagated to every linked cargo item through the existing shipment milestone service.
5. The client sees the update through `/api/tracking/{reference}` or the authenticated customer portal. Staff can add a manual event through the same pipeline when an operational correction is necessary.

MSC event IDs are idempotent at the provider level, and cargo milestone uniqueness includes the cargo ID so a single container event can be safely applied to several customers' items. Notifications are de-duplicated per client during a sync.

Local development uses deterministic mock events. Live operation requires completing MSC Track & Trace API onboarding and supplying the production endpoint template and credential:

```text
MscTracking__Mode=Live
MscTracking__TrackAndTraceUrlTemplate=https://provider-endpoint/.../{containerNumber}
MscTracking__ApiToken=secret-from-msc
```

Do not place the token in source control. Store it in the deployment secret manager. MSC documents its Track & Trace subscription at <https://developerportal.msc.com/content/TnT_Subscription_Customer_Onboarding.pdf> and publishes its API catalogue at <https://developerportal.msc.com/api-catalogue>.

## Production operations

- Run PostgreSQL migrations once as a release job before scaled replicas start.
- Schedule container synchronization in a background worker/outbox process; the synchronous endpoint is retained for staff-triggered refresh and support diagnostics.
- Monitor provider failures, stale-rate age, last successful container sync, and notification failures.
- Keep client-visible corrections auditable; do not delete the original carrier event.
- Validate MSC UAT examples before switching `MscTracking:Mode` to `Live`.
