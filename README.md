# BiGSAM Global Logistics API

Backend for the BiGSAM customer portal and a separate internal logistics management dashboard. It includes freight operations, customer accounts, staff administration, CRM, bookings, warehouse receipt, invoices, payments, expenses, double-entry journals, isolated local integration adapters, auditing, and dashboard reporting.

## Projects

- `Domain` — business entities, enums, value objects, and rules.
- `Application` — use cases, DTOs, validation pipelines, and service contracts.
- `Infrastructure` — EF Core PostgreSQL persistence, identity, notifications, migrations, and initialization.
- `Api` — HTTP controllers, authentication/authorization, pagination, compression, timeouts, traffic controls, CORS, health, and Swagger.
- `Tests` — automated domain and identity tests.

## Run locally

Requirements: the SDK selected by `global.json` and PostgreSQL 16.

```bash
cp .env.local.example .env.local
# Fill the ignored local values, then use hot reload:
cd Api
dotnet watch run
```

`dotnet watch run` loads the repository's ignored `.env.local` file only in the
Local/Development environment. Hosting environment variables take precedence,
and Production never loads this file. The release-style local launcher remains
available as `bash scripts/run-local.sh`.

The Local API listens on `http://127.0.0.1:5120`. Swagger is at the site
root, liveness is `/health/live`, and database readiness is `/health/ready`.
PostgreSQL is the only supported database provider. Local startup applies
checked-in migrations and initializes required reference and operational data in
the single `bigsamgl` application database. Production never changes the schema or
creates local records during API startup.

`bash scripts/run-local.sh` now starts an isolated project-local PostgreSQL
instance on port `55432` when Docker is unavailable. Its files stay under the
ignored `.local` folder. Use `bash scripts/local-db.sh stop` when you want to
stop it. The launcher binds to `127.0.0.1` only and does not use production data.

The dashboard runs on port `4173` and the customer website on port `3001`.
Both use a local `/api` proxy and send `X-BiGSAM-App-Environment: local`. The API
returns `X-BiGSAM-API-Environment` and rejects a local/production mismatch.

## Verify

```bash
bash scripts/verify.sh
```

## Production configuration

Supply every production credential through the hosting platform's secret
manager. `appsettings.json` contains placeholders and safe behavior only.
Production startup validates PostgreSQL TLS/private-network policy, JWT
strength, explicit HTTPS origins and hosts, trusted proxy settings, persistent
certificate-protected Data Protection keys, disabled Swagger, and disabled
automatic migrations/seeding.

Build and review the EF migration bundle, back up the database, execute the
bundle once as a release step, and only then start the API replicas. See
`PRODUCTION_DEPLOYMENT.md` and `.env.production.example`.

Docker Compose is provided for an isolated local PostgreSQL environment:

```bash
POSTGRES_PASSWORD='replace-me' \
JWT_SECRET='replace-with-at-least-32-random-bytes' \
LOCAL_CUSTOMER_PASSWORD='replace-with-a-local-password' \
LOCAL_STAFF_PASSWORD='replace-with-a-local-password' \
CARRIER_WEBHOOK_SECRET='replace-with-a-local-secret' \
docker compose up --build
```

Production uses `compose.production.yml`, a reviewed release image, an external
production database and live-provider secrets. It has no database service, no
seed flags and no shared local volume.

The solution remains on the latest serviced .NET 8 line for compatibility with
the current codebase. Because .NET 8 support ends in November 2026, upgrading
the solution and container images to .NET 10 LTS is a scheduled release task,
not an unreviewed framework jump inside a logistics feature change.

## Main API areas

- `/api/auth` — register, login, rotate/revoke sessions, current account.
- `/api/customer-portal` — the signed-in customer's profile, shipment summary, receiver data, and detailed tracking.
- `/api/tracking/{reference}` — public cargo timeline with current movement, location, ETA, and delay/issue state.
- `/api/public/tracking/invoice/{invoiceNumber}` — customer-safe sea/air manifest tracking by the immutable BiGSAM invoice, containing only staff-approved updates and no MSC reference.
- `/api/customer-shipments` — one shipment and one immutable system-generated invoice per customer package.
- `/api/public/shipping-services` — published dashboard-managed services, schedules,
  destination coverage and server-calculated Air/Sea estimates from PostgreSQL.
- `/api/admin/staff` — administrator-managed staff accounts, roles, sessions, suspension, reactivation and archival.
- `/api/sales` — quotations, invoices, credit notes, receipts, payment-method summaries, freight rates, and additional item/service prices without an enquiry pipeline.
- `/api/contacts` — separated client, supplier, director, shareholder, staff, service-provider, partner and carrier directories with duplicate suggestions and interaction history.
- `/api/appointments` — pickup/delivery schedules, executive and staff meetings, maintenance, inspections, training, compliance timing, availability checks and reminders.
- `/api/finance` — verified `BGL0000INV`-series invoices, routed payments, automatic printable receipts, money-in/out transaction register, expense approvals, provider-backed exchange rates, and auditable currency conversion.
- `/api/finance/currency` — latest rates, conversion, country-to-currency resolution, and stored rate history.
- `/api/dashboard/overview`, `/api/dashboard/sales-performance` — management totals plus weekly and historical sales reporting.
- `/api/operations` — shipment manifests, routes, rates, consolidation, later carrier/container allocation, editable tracking, batch client updates, carrier sync, and documents.
- `/api/operations/manifests` — shipment headers identified by unique Shipment ID and Shipment Booking ID, with customer-name suggestions, automatic new-client contact creation, separate receiver details, Air weight/dimension pricing, Sea box/item pricing, negotiated special-cargo pricing, one automatically paid and immutable invoice per package entry, later movement/container placement, invoice release checks, safe printing, and complete CSV reporting.
- `/api/loyalty` — existing-customer referral rewards, append-only points history, balances, and leaderboard; each paid referral invoice can award points only once.
- `/api/operations/containers` — register and rename MSC containers, maintain a friendly display name, link container cargo, synchronize carrier events, inspect raw event history, and add audited staff corrections.
- `/api/operations/tracking` — connect an MSC container, bill of lading or booking reference, automatically ingest official DCSA events as private drafts in live mode, and review/edit/publish the BiGSAM wording customers see.
- `/api/operations/shipment-movements`, `/api/operations/shipment-allocations` — plan later air/sea carrier decisions, assign invoiced cargo in bulk, select physical container types, reassign safely, and retain placement history.
- `/api/legacy-imports` — download the Excel template, preview historical clients/invoices/cargo, review row errors and duplicates, and commit an idempotent PostgreSQL import.
- `/api/workforce/directory` — the active staff directory used by approved operational workflows.
- `/api/accounting` — chart of accounts, journals, trial balance, suppliers, bills, purchase orders, and bank accounts.
- `/api/purchasing` — itemized maintenance/service/company purchases, approval decisions, partial/full payouts, accounting postings, stock receiving, and automatic payment vouchers.
- `/api/inventory` — basic company stock, locations, stock in/out, transfers and low-stock warnings backed by a protected movement ledger.
- `/api/reports` — clearly separated sales, purchasing, basic inventory, contact, schedule and executive reports plus safe CSV stock export.
- `/api/operations/additional-service-pricing` — versioned branch prices for batteries, regulatory/NAFDAC work, boxes, delivery, storage, handling, insurance, and other add-ons.
- `/api/integrations` — configured-provider status, message log, and idempotent carrier webhooks.
- `/api/clients` — normalized unique client identities, match suggestions, auditable duplicate-review decisions and merge history.
- `/api/company` — company identity and universal branch operating controls.
- `/api/communications` — audited single-client email plus administrator-controlled targeted broadcasts with audience preview, consent filtering, image/PDF attachments, scheduling, per-recipient delivery state, cancellation and retry.
- `/api/audit` — entity-change history, complete staff request activity, staff timelines, branch summaries, and consented location history.

Growing list routes support an opt-in `paged=true` response while preserving existing array clients. Audit feeds additionally support cursor pagination. PostgreSQL query indexes, bounded connection pools, response compression, request deadlines, per-account traffic limits, shift-safe attendance limits, and report concurrency protection keep routine staff usage from overwhelming the service.

Quotation, manifest, and invoice controllers expose explicit email actions. Attachments are provider-owned document records linked to the selected client; the API accepts at most 10 image/PDF attachments and 25 MB combined per email.

Frontend implementation starts with the authoritative [active dashboard API contract](docs/DASHBOARD_API_CONTRACT_2026-07-23.md) and [active customer website API contract](docs/CUSTOMER_WEBSITE_API_CONTRACT_2026-07-25.md). Historical workflow documents are retained as design history and do not override the active contract. Team and deployment rules are in [CONTRIBUTING.md](CONTRIBUTING.md), [the architecture guide](docs/ARCHITECTURE.md), and [the production deployment runbook](PRODUCTION_DEPLOYMENT.md).
