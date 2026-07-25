# BigsamGL Backend Architecture

This codebase is organized for a growing, multi-branch logistics company. Business areas are kept independent so a change to rates, payroll-adjacent workforce features, invoicing, or client management has a narrow impact.

## Dependency direction

```text
Api  ───────────────┐
                    v
Application ────> Domain
    ^               ^
    │               │
Infrastructure ─────┘
```

- **Domain** owns business entities, value objects, enums, and business exceptions. It has no database or web dependency.
- **Application** owns use cases, service contracts, validation, and transport-neutral DTOs.
- **Infrastructure** implements persistence, authentication, accounting, email, storage, and third-party adapters.
- **Api** owns HTTP routing, authorization, request/response contracts, middleware, health endpoints, and Swagger.

Infrastructure is replaceable. For example, the mock email or carrier implementation can later be replaced without changing controllers or domain entities.

Operations follows the forwarder/consolidator boundary in `BUSINESS_OPERATING_MODEL.md`: `ShippingBatch` owns BigsamGL's internal cargo grouping, `ShipmentMovement` owns the external carrier/partner leg, and `ShipmentContainer` owns the physical carrier container and tracking feed. Client accounts explicitly distinguish individuals, businesses and partner agencies.

## Business modules

| Module | Ownership | Primary API area |
|---|---|---|
| Identity | users, credentials, sessions, roles and staff lifecycle | `/api/auth`, `/api/admin/staff` |
| Customers | unique identity keys, duplicate reviews, client profiles and customer shipments | `/api/clients`, `/api/customer-shipments`, `/api/customer-portal` |
| Contacts | separated client, supplier, corporate, staff, provider and partner contacts | `/api/contacts` |
| Sales | quotations, invoices, credit notes, receipts and managed selling prices | `/api/sales`, `/api/operations/quotes`, `/api/finance` |
| Scheduling | pickups, deliveries, meetings, maintenance, training and compliance timing | `/api/appointments`, `/api/schedule/pickups` |
| Operations | shipment manifests, routes, rates, consolidation, allocation, carrier movement and published tracking | `/api/operations`, `/api/customer-shipments`, `/api/public/tracking` |
| Workforce | active staff directory and role-scoped administration | `/api/workforce/directory`, `/api/admin/staff` |
| Finance | invoices, payments, expenses, wallets, banks, exchange-rate snapshots | `/api/finance` |
| Accounting | ledger accounts and journals | `/api/accounting` |
| Procurement | suppliers, supplier bills, purchase orders | `/api/accounting` |
| Purchasing | company purchases, approvals, receiving, payouts and vouchers | `/api/purchasing` |
| Inventory | basic company-owned items, locations, stock in/out and protected movement ledger | `/api/inventory` |
| Reporting | cross-module read models and exports | `/api/reports`, `/api/dashboard` |
| Company | company profile and branch controls | `/api/company` |
| Communications | email history and attachments | `/api/communications` |
| Integrations | mock/live provider boundary, MSC container adapter, and message log | `/api/integrations`, `/api/operations/containers` |
| Auditing | entity changes, complete staff request trail, authentication events, consented locations | `/api/audit` |

The exact supported routes and retired compatibility routes are recorded in
`DASHBOARD_API_CONTRACT_2026-07-23.md`. Historical controllers deliberately
excluded by `Api.csproj` are migration reference only and must not be used by a
new dashboard.

## Runtime and deployment boundary

`Program.cs` is intentionally a composition root only. Service registration
lives in `Api/Extensions/ServiceCollectionExtensions.cs`, the HTTP pipeline in
`Api/Extensions/WebApplicationExtensions.cs`, runtime rules in
`Api/Configuration/RuntimeSettings.cs`, and cross-cutting request behavior in
`Api/Middleware`.

Local development uses the `Local` environment and the ignored `.env.local`
file. Production rejects local database hosts, weak or placeholder JWT secrets,
wildcard CORS/host settings, automatic migrations, Swagger, missing trusted
proxy configuration, or ephemeral Data Protection keys. The deploy workflow
applies a reviewed EF migration bundle before starting the application binary.

## Source layout rules

### Domain entities

Every entity has its own file under `Domain/Entities/<Module>`. Physical folders express ownership; the existing `Domain.Entities` namespace remains stable to prevent unnecessary EF Core model and migration churn during this reorganization.

Do not create another catch-all entity file. New entities go into the module that owns their lifecycle. If another module needs the data, it references the entity or uses an application service; it must not duplicate the table.

### Enums

Enums are grouped only when they belong to the same bounded business module under `Domain/Enums`. For example, payment states belong in `FinanceEnums.cs`, while booking and transport states belong in `OperationsEnums.cs`.

Persisted enum numeric values are a database contract. Add new values at the end and assign explicit numeric values before production data exists. Renaming is safe for numeric storage but can affect JSON clients, logs, and reports; treat it as an API change.

### HTTP APIs and contracts

Controllers are grouped by business capability. Large route groups should be split into focused controllers with explicit route templates, as demonstrated by `Api/Controllers/Operations`.

Request and response records live under `Api/Contracts/<Module>`, not at the bottom of controllers. Their current `Api.Controllers` namespace is intentionally retained for Swagger component-name compatibility. A future versioned API may move namespaces as a deliberate breaking change.

Controllers should only:

1. authorize and bind HTTP input;
2. call an application service or a narrow persistence operation;
3. translate the result into an HTTP response.

Complex pricing, accounting, notification, and workflow rules belong in Application services, not controllers.

## Compatibility boundaries

The following are treated as stable contracts:

- route paths and HTTP methods;
- request/response JSON property names and enum representations;
- entity namespaces and EF table/column mappings;
- migration history;
- authorization policy names;
- integration interfaces in Application.

A physical file move must not create a migration. Before merging structural changes, run the build, tests, EF pending-model check, and compare the generated OpenAPI paths and schemas.

## How changes stay isolated

- A pricing change normally touches Operations rate/quote services, their contracts, and focused tests.
- A new carrier touches an integration adapter and registration; shipment manifests and tracking continue to use the same interface.
- A currency provider change touches the exchange gateway/service; finance and quotation controllers continue to consume the stable conversion contract and stored snapshots.
- A client matching-rule change stays behind `IClientIdentityService`; shipment creation and client management consume the same review contract.
- Request auditing stays in middleware and `IStaffActivityService`; business controllers do not duplicate activity logging. Authentication events are added explicitly because login/refresh begins before an authenticated request principal exists.
- A new communication provider touches Infrastructure; invoice, quote, and manifest workflows remain unchanged.
- A new staff role touches Identity enums/policies and only the relevant authorization tests.
- A new accounting report reads Accounting models and does not change shipment entities.

## Growth path

Keep the current modular monolith while the company and workflows are evolving. It provides one deployment and transactional database without sacrificing clear ownership. If scale later requires separate services, the module boundaries and integration interfaces are the extraction seams. Prefer an outbox/background-worker pattern for reliable email, carrier updates, and accounting side effects before splitting deployments.
