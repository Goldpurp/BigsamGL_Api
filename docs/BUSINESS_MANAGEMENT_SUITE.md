# BigsamGL business management suite

## Purpose

This release turns the backend into a connected daily operating system for a freight-forwarding and consolidation agency. Sales, contacts, appointments, purchasing, finance, and company-owned stock share one PostgreSQL source of truth. Client cargo remains in the existing freight warehouse module; packaging, office supplies, equipment, spares, and resale items belong to the company inventory module.

## Operating boundaries

| Area | Owns | Does not own |
|---|---|---|
| Sales | leads, priorities, owners, follow-ups, stages, lost reasons, linked activity | invoice settlement or physical cargo |
| Contacts | people at customers, suppliers, agencies, prospects and carriers; interaction history | sign-in credentials |
| Scheduling | appointments, attendees, availability, reminders and outcomes | attendance check-in/out |
| Purchasing | vendor request, approval, payable, payout and receiving state | silent stock changes |
| Company inventory | stock catalogue, locations, balances, receipts, transfers, issues and valuation | customer freight or shipment tracking |
| Freight inventory | booked client goods, pieces, warehouse status, allocation and exceptions | company consumables |
| Reports | read-only management projections | independent copies of financial truth |

## Dashboard information architecture

Add these items to **BigsamGL Control Centre**:

1. **Sales**
   - Quotations
   - Invoices and credit notes
   - Receipts
   - Shipping and item prices
2. **Contacts**
   - Directory
   - Due follow-ups
   - Interaction timeline
3. **Calendar**
   - My schedule
   - Team calendar
   - Appointment list
4. **Purchasing & Payables**
   - Purchase register
   - Approval queue
   - Warehouse receiving
   - Payouts
5. **Company Inventory**
   - Stock overview
   - Items
   - Locations
   - Movements
   - Low-stock report
6. **Reports**
   - Executive summary
   - Sales performance
   - Purchases
   - Inventory
   - Appointments

The existing **Warehouse** menu must remain labelled **Client Cargo Warehouse** so staff do not confuse a client's shipment with company-owned stock.

## Sales implementation

Base route: `/api/sales`.

- Sales is a read model for quotations, invoices, credit notes, receipts and active freight/additional-service prices.
- Enquiries, lead stages, next actions, sales activities and lead targets are retired from active APIs.
- Currency values are always grouped separately and never added across currencies.
- Document creation remains in Operations and Finance so quotation pricing, invoice posting and receipt generation each have one source of truth.

## Contact implementation

Base route: `/api/contacts`.

- Contact types cover customers, suppliers, prospects, partner agencies, carriers, government agencies and other relationships.
- A contact may link to a customer, supplier, sales lead, preferred branch and staff owner.
- Create and update run normalized name, email and phone matching. A `409` response returns suggested records and `canOverride`; the dashboard must show those suggestions before resubmitting with `ignorePotentialDuplicate: true`.
- `GET /match-check` supports an explicit preflight duplicate prompt.
- Interactions record calls, email, WhatsApp, meetings, visits, complaints, notes and follow-ups. Adding one updates the contact and linked lead follow-up dates.
- Deactivation and archive are lifecycle states; do not hard-delete business history.

## Scheduling implementation

Base route: `/api/appointments`.

- Appointments use unique `BGL00000001APT`-style references and UTC storage with an explicit display timezone.
- They can link to a branch, contact, customer, sales lead or booking.
- Attendees are staff, managed contacts or external people. Staff and contact attendees must reference existing records.
- Create and update reject overlapping appointments for organizers, assigned staff and staff attendees.
- `GET /availability` checks a proposed staff time window.
- Status changes follow a controlled lifecycle: tentative/scheduled/confirmed, in progress, completed, cancelled or no-show.
- Cancellation requires a reason. Completed and cancelled appointments are not directly edited.
- `POST /{id}/reminder` sends the reminder through the configured integration boundary. It is visibly mocked in the current environment and records the provider message.

The calendar should fetch only the visible date range. Use branch timezone labels and render conflicts returned by the API rather than hiding them.

## Purchasing and inventory implementation

Purchase lines optionally link to an inventory item. Service, rent and utility lines normally remain unlinked and their receiving state is `NotRequired`. A purchase containing stock items starts as `Pending` and can be received only after approval.

`POST /api/purchasing/purchases/{id}/receive` accepts one or more line quantities and an idempotency key. It prevents over-receipt, validates that the stock location belongs to the purchase branch, updates weighted-average cost, and changes the purchase to partially or fully received.

Company inventory base route: `/api/inventory`.

- Items have a unique SKU, optional unique barcode, type, category, unit, currency, cost, supplier and active state.
- Locations belong to a branch and represent a warehouse, office, vehicle, transit or quarantine area.
- Stock balances track on-hand, reserved, available, average cost, reorder level and target level.
- Adjustments support opening balance, return, issue, increase/decrease, write-off and stock-count correction.
- Transfers move stock between locations in one transaction.
- Every movement has an immutable company number, actor, time, reason, reference and idempotency key.
- Serializable PostgreSQL transactions and advisory locks prevent lost updates and negative stock when staff post concurrently.
- Items or locations holding stock cannot be deactivated.

The dashboard must generate a fresh idempotency key per user action and reuse it when retrying that same action. It must not reuse a key for a different movement.

## Reports

Base route: `/api/reports`.

- `/executive-summary` combines the selected period's lead pipeline, invoice and purchase totals by currency, appointments, active cargo, inventory warning counts and overdue contact follow-ups.
- `/purchases` groups purchases by currency, category, workflow status and receiving status.
- `/inventory` returns valuation by currency/category and a replenishment list.
- `/appointments` shows status/type counts and completion rate.
- `/inventory/export.csv` creates a spreadsheet-safe UTF-8 CSV. Cells that could be interpreted as formulas are neutralized.

Report periods are limited to two years, requests use the existing report concurrency limit and timeout, and unlike currencies remain separate.

## Reliability and security rules

- PostgreSQL is the only database. The migration `AddInventoryContactsSchedulingAndBusinessReports` is the schema authority for this release.
- Every entity change flows through the existing audit pipeline; staff requests and locations remain available in the audit module.
- Access is role-based. Inventory accepts administrators, operations, finance and staff. Finance approvals remain finance-approver only.
- Lists are server-paginated and indexed. Dashboard screens debounce search and cancel obsolete requests.
- Mutations return structured validation/conflict responses. The frontend should keep form data when a request fails and never silently repeat a non-idempotent operation.
- Mock reminders and provider calls must display the existing **MOCK PROVIDERS** badge.

## Recommended dashboard delivery order

1. Generate the typed client from `/swagger/v1/swagger.json`.
2. Add Contacts and Calendar because Sales workspaces link to both.
3. Add Company Inventory catalogue and location setup.
4. Add purchase-line inventory selection and approved receiving.
5. Add consolidated reports and CSV export.
6. Add saved filters, role-aware shortcuts and dashboard notification badges.

Each page should include loading, empty, error, forbidden and conflict states. Test optimistic updates by forcing a `409`, timeout and `429` before release.
