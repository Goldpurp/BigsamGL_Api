# Bigsam Global Logistics product roadmap

## Product direction

The new platform should serve two connected experiences:

1. A customer website and portal for quotes, bookings, payments, shipment history, and tracking.
2. An internal operating system for daily sales, freight operations, staff, expenses, invoicing, collections, and management reporting.

The domain is based on Bigsam's current services: weekly air and sea freight, UK/Nigeria customs clearance, same-day and next-day delivery, B2B logistics, sourcing, storage, branding, fulfilment, and logistics add-ons. The architecture is modular so payroll, purchasing, full accounting, and other Capium-like capabilities can be added without replacing the logistics core.

BigsamGL is a freight forwarder and consolidator, not the operator of the main airline or ocean vessel. Product growth should prioritize warehouse throughput, consolidation, partner capacity, carrier integrations, customer updates and route profitability instead of ship/aircraft fleet management.

## Delivery phases

### Platform backend — implemented in this API

- Clean Domain, Application, Infrastructure, and API boundaries.
- PostgreSQL-only persistence across development, testing, staging, and production.
- Versioned EF Core migration for the production schema.
- Secure customer and staff accounts with PBKDF2 password hashing, short-lived JWT access tokens, rotating refresh tokens, lockout, revocation, and role policies.
- Customer portal summary endpoint and protected staff APIs.
- Document-focused commercial sales: quotations, invoices, credit notes, receipts, shipping prices, and payment reporting.
- Branches, routes, rate cards, dimensional pricing, customer addresses, quotes, bookings, mock payment confirmation, warehouse receipt, cargo pieces, milestones, document metadata, internal consolidation batches, external carrier movements, and manifests.
- Customer profiles, notification consent, email verification, password reset, and customer portal data.
- Staff accounts, attendance, leave requests, approval decisions, roles, and status control.
- Invoicing, payment recording, expenses, approvals, suppliers, supplier bills, purchase orders, bank-account metadata, chart of accounts, balanced posted journals, and trial balance.
- Mock email/SMS/WhatsApp/payment/carrier/storage adapter boundary with a queryable integration log.
- Automatic entity-change records, complete staff request/authentication auditing, consented browser GPS history, rate limiting, CORS restrictions, structured errors, health endpoint, Swagger in development, Docker packaging, and CI verification.

### Customer website integration — next frontend milestone

- Replace the web app's browser-only demo login with `/api/auth`.
- Replace hard-coded dashboard shipments with `/api/customer-portal`.
- Connect public tracking to `/api/tracking/{reference}` and add a real milestone history table.
- Persist quotation requests as quotations or client records without introducing an enquiry pipeline; add consent and privacy records.
- Connect the implemented verification, password reset, addresses, business profiles, notification preferences, quote, booking, and tracking APIs.
- Add optional MFA and the real file-upload stream after a storage provider is selected.

### Freight operations hardening

- Replace seeded demonstration rates with approved commercial rate cards.
- Add real barcode/label printing, binary file upload, proof of delivery signatures, and carrier webhooks.
- Add formal claims, exception queues, SLA timers, and last-mile driver assignment.

### Commercial sales and staff hardening

- Connect real WhatsApp/email delivery for quotations, invoices, receipts and shipment updates.
- Add granular permission grants beyond roles, work schedules, leave balances, and document expiry alerts.
- Add configurable monetary approval limits and stronger separation-of-duties rules.

### Finance and Capium-style hardening

- Extend the implemented double-entry journal and chart of accounts with credit notes, supplier payments, bank transactions, reconciliation, cashbook, VAT/tax configuration, multi-currency revaluation, locking accounting periods, and journal reversals.
- Payroll, pensions, statutory deductions, asset register, budgets, management accounts, profit by route/branch/service, and controlled exports for accountants.
- Integrations for payment gateway, bank feeds, messaging, carrier events, identity verification, and external accounting tools.

## Decisions required before public production

- Hosting region/provider and managed PostgreSQL service.
- Primary business currencies and the legal/tax treatment for UK and Nigerian entities.
- Payment provider and refund process.
- Email, SMS, and WhatsApp providers and approved message templates.
- Branch list, staff roles, approval limits, and who can view payroll/accounting data.
- Exact rates, prohibited cargo rules, customs document requirements, and public tracking wording.
- Data retention, privacy policy, backup recovery targets, and incident contacts.
- Employee location notice/consent policy, who may view location history, retention duration, and the disciplinary-investigation approval process.

## Production acceptance gate

Public launch should wait until the web integration, end-to-end API tests, PostgreSQL integration tests, backups and restore drill, observability/alerts, vulnerability scanning, secret storage, staging sign-off, privacy/terms, and operational training are complete.
