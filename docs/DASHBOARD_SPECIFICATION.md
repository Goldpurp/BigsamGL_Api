# BigsamGL staff and management dashboard specification

## 1. Purpose

Build a separate internal dashboard application for Bigsam Global Logistics. It is not part of the public/customer website. The dashboard is the daily operating workspace for sales, customer service, warehouse teams, freight operations, finance, HR, and management.

The dashboard must present BigsamGL as a freight forwarder and consolidator that books external carrier capacity. It must distinguish BigsamGL's internal consolidation batch from an airline/ocean-carrier movement, vessel, flight or container. See `BUSINESS_OPERATING_MODEL.md`.

Recommended working name: **BigsamGL Control Centre**.

The dashboard must use this API as its only source of business truth. It must never duplicate rates, shipment states, financial totals, permissions, or staff records in browser storage.

## 2. Suggested frontend foundation

- React and TypeScript with a production router and query cache.
- A generated API client from `/swagger/v1/swagger.json`.
- Access token kept in memory; refresh through the `HttpOnly` `bigsam_refresh` cookie using credentialed requests.
- Role-aware navigation, but all authorization remains enforced by the API.
- Desktop-first responsive layout with usable tablet views for warehouse staff.
- UTC API timestamps rendered in the branch timezone.
- GBP, NGN, USD, EUR, and CNY rendered with `Intl.NumberFormat`; never add unlike currencies together.

## 3. Roles and default workspaces

| Role | Default workspace | Main access |
|---|---|---|
| SuperAdmin | Executive overview | Everything, configuration, audit, integrations |
| Administrator | People and controls | Staff, leave, customers, configuration, audit |
| OperationsManager | Operations board | Bookings, warehouse, cargo, batches, manifests, tracking |
| SalesManager | Sales command centre | Leads, activities, quotes, targets, customer conversion |
| FinanceManager | Finance command centre | Invoices, payments, expenses, journals, suppliers, approvals |
| Staff | My work | Assigned operational/sales work, attendance, leave |
| Customer | Never use this dashboard | Customer portal only |

## 4. Global application shell

### Left navigation

1. Overview
2. Sales
   - Quotations
   - Invoices and credit notes
   - Receipts
   - Shipping and item prices
3. Contacts
   - Clients and suppliers
   - Directors and shareholders
   - Staff, providers and partners
   - Interaction history
4. Calendar
   - My schedule
   - Team appointments
5. Bookings
6. Operations
   - Simple manifests
   - Cargo register
   - Warehouse receiving
   - Consolidation batches
   - External carrier movements
   - Unallocated cargo
   - Shipment milestones
   - Manifests and documents
7. Customers
8. Finance
   - Invoices
   - Payments
   - Receipts and payment vouchers
   - Transaction register
   - Expenses and approvals
9. Accounting
   - Chart of accounts
   - Journals
   - Trial balance
   - Suppliers and bills
   - Purchase orders
   - Bank accounts
10. Purchasing & Payables
   - Company purchases
   - Approval queue
   - Stock receiving
   - Payouts
11. Company Inventory
   - Stock overview
   - Items and barcodes
   - Branch locations
   - Movements and low-stock warnings
12. Reports
   - Executive summary
   - Sales, purchases, inventory and appointments
13. Workforce
   - Staff directory
   - Staff administration
   - Attendance
   - Leave and approvals
14. Integrations
15. Audit log
16. Settings
   - Legacy data migration
   - Additional Service Pricing

Settings includes company identity, backend-managed rate cards, universal branch controls, and communication templates.

### Top bar

- Global search for customer, booking, tracking number, marker code, invoice, quote, and staff employee number.
- Branch selector. Persist preference, not permission.
- Currency selector for reporting cards.
- Date-period selector.
- Approval inbox badge.
- Integration mode badge: display a permanent amber **MOCK PROVIDERS** badge while `/api/integrations/status` returns `Mode: Mock`.
- User menu with profile, password change, and logout.

## 5. Overview page

Primary source: `GET /api/dashboard/overview?from=&to=&currency=&countryCode=`. If currency is omitted, `NG`/`NGA` resolves to NGN and `CN`/`CHN` resolves to CNY. An explicitly selected currency always wins.

### KPI cards

- Collected revenue
- Invoiced amount
- Outstanding receivables
- Approved expenses
- Operating surplus
- Active shipments
- Delivered shipments
- Confirmed/active bookings
- New leads and won leads
- Pending expense approvals
- Pending leave approvals

### Visual sections

- Revenue trend using `revenueTrend`.
- Lead funnel using `leadPipeline`.
- Shipment state distribution.
- Upcoming departures: Tuesday sea, Thursday air, Friday Nigeria dispatch, driven by service-route data rather than hard-coded labels.
- Recent activity stream.
- Exceptions panel for delayed shipments, overdue invoices, failed integrations, and approvals.

Every card links to its filtered detail page. Empty states must explain the action required to create data.

## 6. Sales workspace

### Commercial document centre

API: `/api/sales`.

- Separate tabs for quotations, invoices/credit notes, receipts, and shipping/item prices.
- Do not display enquiries, lead stages, next actions, activity targets or a CRM pipeline.
- Sales summary cards show quotation value, accepted quotation value, invoiced value, collected value and outstanding value by currency.
- Creation actions route to the focused quotation and finance APIs so calculations and accounting rules are not duplicated.

### Retired enquiry workflow

Guest enquiries, prospect pipelines and follow-up task screens are not part of the active dashboard. Historical data remains dormant during the transition period.

### Quotes

API: `/api/operations/routes`, route rates, and `/api/operations/quotes`.

- Select route and cargo category.
- Capture dimensions, weight, declared value, and contact.
- Show actual, volumetric, and billable weight separately.
- Display base cost, fuel surcharge, customs estimate, total, currency, and expiry.
- For a destination in Nigeria or China, display the original quotation total and the returned `localTotal` in NGN or CNY. Show the rate, provider, and effective timestamp in the conversion detail; never silently replace the contractual source amount.
- Rates are managed records; dashboard users must never type calculated totals directly.
- Email a quotation to the linked client with optional owned image/PDF attachments. The communication and provider result appear in client history and audit history.

### Weekly and historical performance

API: `GET /api/dashboard/sales-performance?from=&to=&currency=&countryCode=`.

- Default to the latest 12 weeks and allow a history range up to two years.
- Plot quotations, accepted quotations, sales invoices, credit notes, receipts, quoted value, invoiced revenue, collected revenue and outstanding value per week.
- Do not infer staff performance from document creators unless a later commission/ownership model is explicitly approved.
- Never combine different currencies; changing the currency reloads all monetary series.
- Country shortcuts select NGN for Nigeria and CNY for China. The consolidated view must still require an explicit reporting currency.

## 7. Booking and operations workspace

### Simple manifest register

This is the default Operations screen. Keep advanced warehouse, batch, movement and container tools behind **More operations tools** so routine staff are not overwhelmed.

- Primary action: **New shipment manifest**.
- A manifest is the shipment header. It is not a customer invoice.
- Create fields: Shipment ID (the displayed manifest name), Shipment Booking ID, route, date and Air/Ship method.
- After creation, **Edit manifest** exposes only Shipment ID and Shipment Booking ID. Route, date and method are read-only.
- Header table actions: Open entries, Edit identity, Print and Download report.
- API: `/api/operations/manifests`.
- The header has no customer, invoice, package, payment, content, weight or delivery fields.
- Present separate **Air transactions** and **Ship transactions** tabs. Group both by manifest date using `/api/operations/manifests/date-groups`, but never combine their counts, weights or values.
- Keep **All manifests** as the first view. Add direction views for UK → Nigeria, Nigeria → UK, China → Nigeria and any later route. Each direction contains its own Air and Ship split.
- Manifest detail contains paginated **Customer and package entries** from `/api/operations/manifests/{id}/entries`. Every entry is one customer shipment and receives exactly one system-generated invoice; an invoice cannot be shared by another entry.
- The add modal starts with a free-text **Customer's name** input. Query `/api/operations/manifests/customer-name-suggestions?name=` while typing, then either select an existing customer ID or submit phone, email and country for a new customer. New customers are automatically added to the Client contact directory; similar identities return the standard duplicate-review prompt.
- Receiver name, phone and email are separate required fields because the paying customer may send to somebody else. Receiver details remain operationally editable.
- Put invoice amount and payment method in a visibly separate **Invoice** section. The backend creates the `BGL0000INV` number, exact current date/time, issuer currency, confirmed initial payment, receipt and accounting records. Do not show payment terms.
- Air pricing captures dimensions, actual weight, volumetric divisor and rate per billable kg. Sea pricing captures multiple box-size/item lines. Air or Sea special machinery can use a clearly labelled negotiated method. Load immutable calculation evidence from `/api/operations/manifests/{id}/entries/{entryId}/pricing`.
- The invoice panel is read-only and loads from `/api/operations/manifests/{id}/entries/{entryId}/invoice`. The normal Edit action changes only receiver, sender/package operations, weights, handover and later batch/movement/container placement.
- An optional referrer must be selected from existing customer suggestions. A paid referred shipment awards the recorded customer 10 points once; show loyalty history from `/api/loyalty/customers/{customerId}`.
- The safe **Print manifest** excludes contents, financial values, payment data and cost weight. **Download complete report** uses `/api/operations/manifests/{id}/report` and includes every operational and financial field.
- Hub handover requires `/api/operations/manifests/{id}/entries/{entryId}/release-check`; a failed check must show **Do not hand over**.
- Containers may have an editable friendly name in addition to the official unique number. Show the friendly name in package rows and retain the official number in detail.
- Cost weight means the chargeable/billable weight after comparing actual and volumetric weight; it cannot be below actual weight.

### Booking queue

- Status filters: Awaiting payment, Confirmed, Received, Consolidated, Dispatched, Delivered, Cancelled.
- Show mock payment state clearly until a real provider is connected.
- Actions are permission-based and status-driven.

### Warehouse receiving

API: `POST /api/operations/bookings/{id}/receive`, `GET /api/operations/warehouse/summary`, and `GET /api/operations/warehouse/inventory?flow=`.

- Scan or enter booking number.
- Verify customer and cargo description.
- Record actual weight, dimensions, warehouse location, condition, and photographs/documents.
- Generate and print piece label containing tracking and piece numbers.
- Confirmation displays created cargo tracking number.
- Dedicated filters cover `pickup`, `incoming`, `warehouse`, `in-transit`, and `issues` flows.
- Inventory rows expose only the client's name, phone number, and email; receiver data is shipment-specific.

### Warehouse reconciliation and exceptions

API: `/api/operations/warehouse/exceptions`.

- The scan screen accepts an internal tracking number, marker code, or carrier tracking number and compares the physical item with the expected client and warehouse location.
- A mismatch opens a case for client-item mismatch, physical misplacement, unidentified item, label/quantity mismatch, damage, or another controlled reason.
- The exception queue shows severity, owner, age, linked cargo, expected versus observed identity/location, client visibility, and resolution status.
- Place linked cargo on hold while an exception remains open. Unidentified physical items must be moved to a clearly marked exception holding area.
- Resolution can correct the cargo client, related booking ownership, piece location, and restored cargo status. Require a resolution summary and retain the original values in audit history.
- Notify a client only when staff explicitly marks the case client-visible. Internal placement investigations must not unnecessarily alarm clients.

### Cargo and shipment detail

- Internal status and customer-visible tracking events are separate.
- Timeline supports event type, location, timestamp, ETA, source, visibility, issue code, notification time, and correction state.
- Staff adds events through `POST /api/operations/cargo/{cargoId}/milestones`. Corrections use `PUT /api/operations/cargo/{cargoId}/milestones/{milestoneId}` and require a revision reason; audit history remains available.
- Carrier binding uses `PUT /api/operations/cargo/{cargoId}/carrier`; mock carrier sync must show a warning before execution.
- Documents show type, file name, checksum, and provider state.
- Consolidation view includes BigsamGL's internal capacity used, piece/weight totals and scheduled handover. External carrier movement details show the separately booked vessel/flight, gateways, provider reference and schedule.

### MSC container tracking

API: `/api/operations/containers`.

- Register a valid MSC container number, select its physical container type, and attach it to a planned sea movement. Cargo is assigned explicitly through shipment allocation; never silently link every batch item.
- Container detail shows booking/BOL references, last synchronization, current location/status, ETA, linked client cargo, and the stored carrier event stream.
- `POST /api/operations/containers/{id}/sync` retrieves the carrier snapshot. The interface must label mock results clearly until live MSC credentials are enabled.
- MSC/DCSA events are normalized into customer-visible cargo milestones. One container event may update many cargo items, while duplicate provider event IDs remain idempotent.
- Staff can add an audited manual container event for an operational correction or issue. Manual events use the same propagation path as carrier events so the public and signed-in tracking pages remain consistent.
- A sync notification is sent at most once to each affected client during that synchronization, even when the client owns several cargo items in the container.

### Shipment planning and late allocation

API: `/api/operations/shipment-movements` and `/api/operations/shipment-allocations`.

- The unallocated queue contains invoiced cargo whose airline, sailing, or container has not been confirmed.
- Create an air or sea movement with `ThirdPartyCarrier` or `PartnerForwarder`, route, schedule, carrier-issued master reference, gateways, and flight/vessel details, then open it for allocation. Never describe this as a BigsamGL-owned vessel or airline.
- Air movements require a flight number and no container. Sea movements require a container belonging to that movement.
- Allow multi-select assignment from the queue. Show the linked invoice, client, cargo measures, current location, and operational hold state before confirmation.
- Reassignment requires a reason and shows current versus proposed movement/container. The former placement remains visible in allocation history.
- Movement detail includes cargo totals, distinct clients, weight, container use, and a client-contact list for delay or issue communication.
- Every table uses server filters and pagination. Do not load the full cargo or movement register to filter in the browser.

### Shipment client updates

API: `GET /api/operations/shipments/{batchId}/clients`, `POST /api/operations/shipments/{batchId}/updates`, and `GET /api/operations/shipments/{batchId}/timeline`.

- The client list shows only customer name, number, email, and tracking numbers.
- Receiver name, phone, email, invoice number, delivery method, and delivery address/hub belong to the booking and may differ from the client.
- Address is required for address delivery; a valid branch is required for hub pickup.
- A batch update creates one tracking event per cargo item but sends one notification per affected client.
- Delay and issue updates appear in the exception panel and can include a revised ETA and issue code.

## 8. Customers workspace

- Searchable customer directory.
- Account type filter: Individual, Business, and Partner Agency. A partner agency is a BigsamGL client account and is distinct from the external main-line carrier.
- Profile: account identity, addresses, shipments, bookings, quotes, invoices, payments, wallet ledger, and audit activity.
- General client controls: account manager, preferred branch, tax/business registration numbers, billing currency, credit limit, payment terms, risk notes, and internal notes.
- Communication history includes general messages, quotations, manifests, invoices, tracking notices, and attachment IDs.
- Staff must not see password hashes, refresh tokens, action-token hashes, or full bank/payment secrets.
- Customer merge and deletion are future controlled operations; do not implement client-only merges.

### Duplicate identity prompt

API: `POST /api/clients/match-check` and `POST /api/clients/duplicate-reviews/{reviewId}/resolve`.

- Run the match check before staff creates, converts, or edits a client. Compare normalized name, email, phone, and optional invoice number.
- When matches exist, open a blocking modal showing each suggested client, the matching fields, exact/similar status, and confidence score.
- Primary action: **Use existing client**. This records the selected client and passes the review ID into the conversion workflow.
- Secondary action: **Create separate client** is available only when `canCreateNew` is true. Require a written reason, resolve the review, then submit the same details with `duplicateReviewId` within 30 minutes.
- Exact normalized name, email, phone, or invoice ownership cannot be ignored. Never present a bypass button when `hasExactConflict` is true.
- Public registration receives a generic existing-record message rather than client suggestions, preventing disclosure of private client data.

## 9. Finance workspace

### Currency and local reporting

API: `/api/finance/currency`.

- Resolve the operational currency from a country code before creating local invoices or expenses: Nigeria uses NGN and China uses CNY.
- Display source amount, converted amount, base/quote currencies, rate, provider, effective time, and fetch time.
- Rate history is immutable and identifies live, last-known, and configured development fallback results. Production must never present a configured fallback as live.
- Allow staff to refresh after the cache interval, but preserve the snapshot used by each calculation for audit and reconciliation.

### Invoices

- Document type is Sales or Credit note. Sales lifecycle is Draft → Issued → Partially paid/Paid/Overdue/Void; credit-note payment state is Not applicable.
- Invoice identifiers use the PostgreSQL-backed sequence `BGL0000INV`, `BGL0001INV`, and onward. Gaps after a cancelled transaction are acceptable; identifiers must never be reused.
- The issuer branch is selected before creation and the API derives its local currency. Never infer currency in frontend-only code.
- Sales invoices select Cash, Bank transfer, or Card as the preferred method. Credit notes show Not applicable.
- Display the document verification code and provide Print and Verify actions. The verification code is not an authentication credential.
- Invoice content and numbers are immutable after creation. Never show an Edit invoice action; corrections use a linked credit note and remain auditable.
- Invoice issue action creates a posted accounting journal.
- Display client-name snapshot, invoice number, type, issuer, currency, document status, payment status, subtotal, tax, discount, total, paid, applied credits, and balance.
- Filter by document status, type, payment status, client, issuer, currency, date range, invoice number/client name, and page.
- Email issued invoices with client-owned image/PDF attachments through the communication service.
- Prevent currency mismatch when applying payment.

### Payments

- Record reference, amount, currency, method, date, and note.
- Applying payment creates one posted money-in transaction and automatically issues a uniquely numbered receipt.
- Cash posts to Cash on Hand. Card and bank transfer remain separate methods in reports but both post to Bank and Card Clearing; optionally select the exact active bank account.
- The payment result opens the receipt view with Print, client-email, invoice, transaction, and journal links.
- Mock checkout/payment actions must never be presented as real settlement.

### Receipts and transaction register

- Receipts and payment vouchers share one paginated document section with type, number, party, amount, currency, method, source, issue date, status, and verification code.
- Print uses the server-generated print view; the browser must not independently recalculate financial totals.
- The transaction register separates Money in and Money out and filters by type, method, status, branch, currency, date, counterparty, reference, and page.
- Each row links to its source invoice/payment or purchase/payout, printable document, and posted journal.
- Public verification returns only minimal authenticity information and must never expose private client or staff details.

### Purchasing & Payables

- Dedicated pages: Purchase register, New/edit draft, Approval queue, Purchase detail, and Payout register.
- Categories cover maintenance, professional/logistics services, supplies, equipment, utilities, rent, regulatory work, and other purchases.
- Purchase detail shows item lines, supplier/vendor, branch, dates, local currency, subtotal, input tax, total, paid, balance, approval reason, payouts, vouchers, and journals.
- Workflow buttons are state-driven: Draft → Submit → Approve/Reject → Partially paid/Paid. Approve/reject and payout require finance-approver permission.
- Payout confirmation shows the destination method/account and outstanding balance. It returns a printable payment voucher and money-out transaction.

### Expenses

- Staff submits; FinanceManager/Admin approves or rejects.
- Approval posts Operating Expense debit and Accounts Payable credit.
- Approval screen includes submitter, branch, category, payee, date, and prior audit activity.

## 10. Accounting workspace

API: `/api/accounting`.

- Chart of accounts grouped into Asset, Liability, Equity, Revenue, Expense.
- Journal entry form must enforce at least two lines and equal debits/credits before submission.
- Posted journals are immutable in the UI; corrections use reversing journals in a later API iteration.
- Trial balance displays debit, credit, and balance by account and a prominent balanced/unbalanced status.
- Supplier bills and purchase orders have separate approval flows.
- Never treat dashboard totals as statutory accounts until tax settings, periods, reconciliation, and accountant sign-off are complete.

## 11. Workforce workspace

- Staff directory with role, department, branch, title, and status.
- Staff rows expose `staffProfileId` and whether a personal attendance PIN is configured; hashes and PIN values are never displayed.
- Attendance has four tabs: **Live/open sessions**, **History and corrections**, **Reports**, and **Branch stations**.
- A branch station page creates and rotates the physical QR/barcode, configures exact coordinates, geofence radius, GPS accuracy, device sharing, and active state, then prints the code for the branch.
- The public `/attendance/kiosk` screen requires no normal dashboard login. It parses the station secret from the URL fragment, captures fresh browser GPS, obtains a five-minute one-time challenge, shows an active branch-staff dropdown with masked emails, and asks for the selected staff member's exact work email and six-digit PIN.
- Check-in/out uses server time and pairs actions into one session. The UI must explain expired/replayed challenges, outside-geofence or inaccurate GPS, wrong branch, recent cross-staff device reuse, PIN lockout, duplicate check-in, and check-out without an open session.
- Administrators can enroll a staff PIN; signed-in staff can change their own. PIN fields are cleared after use.
- Reports default to the current month, filter by custom period/branch/staff, show per-staff sessions and worked time, and provide CSV download and print-ready output. Convert branch-local date boundaries to UTC before requesting the report.
- Corrections require a reason and reviewer permission. Display corrected session values alongside preserved original check-in/out verification events and audit history.
- Leave calendar and personal leave list.
- Approval inbox for submitted leave.
- Sales target setup restricted to sales managers/admins.
- Temporary-password accounts display a forced-change banner.
- Staff administration supports create, profile/role editing, suspend, reactivate, and delete/archive with mandatory reasons.
- “Delete” must explain that the record is archived, login and attendance access are disabled, and historical transactions/audit data remain intact.
- Prevent administrators from disabling themselves and prevent the final active SuperAdmin from being suspended, archived, or demoted.

Full endpoint and security behavior is defined in `docs/SECURE_ATTENDANCE_KIOSK.md`.

## 12. Integrations and mock mode

API: `/api/integrations/status` and `/api/integrations/messages`.

- Provider cards: Email, SMS, WhatsApp, Payment, Carrier Tracking, Document Storage.
- Each card shows provider, live/mock state, last success, last failure, and recent correlation IDs.
- Mock mode must be visible on every page involving payment, message delivery, uploaded documents, or carrier information.
- Never show mock data as confirmed real-world delivery/payment evidence.
- Carrier webhooks use `POST /api/integrations/carriers/{provider}/webhook` with `X-Carrier-Webhook-Secret`.
- External event IDs are unique per provider, making retried GDH/DHL/carrier notifications idempotent.
- Provider status values are normalized into the internal pickup, inbound, warehouse, in-transit, delivery, delay, and issue states before clients are notified.
- MSC uses its dedicated container API and adapter. Live mode requires the provider URL template and token; mock mode remains the safe default for local and demonstration environments.

## 12.1 Company-wide logistics controls

API: `/api/company`, `/api/operations/rates`, and `/api/operations/branches`.

- Company profile controls legal/trading identity, registration/tax details, address, currency, timezone, and logo key.
- Each branch can independently accept/stop bookings, open/close warehouse receipt, enable/disable dispatch, set daily cargo limits, configure capacity warning level, and publish an emergency notice.
- Branch controls are enforced by booking and warehouse-receipt APIs, not only hidden in the interface.
- Rate changes are versioned with effective dates. Replacing a rate closes the old effective period and creates a new record so historical quotations remain auditable.

### Additional Service Pricing

API: `/api/operations/additional-service-pricing`.

- Manage versioned add-on prices for battery handling, regulatory/NAFDAC processing, boxes and packaging, same-day delivery, storage, handling, insurance, and other services.
- Each row includes code, name, category, unit, branch, currency, amount, effective period, and active state.
- Replacement creates a new version and closes the previous one. Deactivation retains history.
- Provide category, unit, branch, currency, effective-date, active/history, search, and pagination controls.

## 13. Audit and safety

- Keep two linked views: entity changes from `GET /api/audit`, and every staff request from `GET /api/audit/staff-activity`.
- Staff activity filters by staff, employee number, branch, outcome, HTTP action, route/path, location availability, and date. Show result status, duration, IP, device, session, and correlation ID.
- `GET /api/audit/staff/{staffUserId}/timeline` places request actions and entity changes side by side for an investigation.
- The summary page uses `/api/audit/staff-activity/summary` for actions/failures by staff, branch, and outcome. Highlight repeated denied actions and server failures without treating ordinary validation errors as security incidents.
- A staff browser reports GPS through `POST /api/audit/location` only after the user has received the company tracking notice and confirmed consent. Display GPS accuracy, capture time, source, and current/stale state.
- The location map uses `/api/audit/locations/latest`; history uses `/api/audit/locations/history`. Never display a stale position as the staff member's live location.
- Branch code and request IP are recorded independently even when browser GPS is unavailable. IP is not presented as a precise physical location.
- Do not store request/response bodies in staff activity records. Passwords, tokens, secrets, authorization values, keys, and codes in query strings are redacted.
- Hide password hashes and secrets; the API already excludes password hashes from audit change payloads.
- Destructive or financial actions require a confirmation dialog summarizing exact impact.
- Disable duplicate submissions while a request is pending.
- Use idempotency keys when the API adds provider-facing payment/webhook endpoints.

### Legacy data migration

API: `/api/legacy-imports`.

- Administrator-only screen with workbook-template download and `.xlsx`/`.csv` upload.
- Always preview first. Show valid, duplicate, and error totals plus a paginated row grid filtered by sheet, entity, status, and search.
- Commit is a separate confirmation and remains disabled while errors exist. Explain that a committed file is idempotent and cannot be used to duplicate records.
- Imported invoice and tracking numbers are preserved. Historical invoices do not back-post accounting journals; show this warning before commit.
- Provide links from imported rows to the resulting client, invoice, or cargo record.
- Full field mapping and acceptance behavior are in `LEGACY_IMPORT_AND_SHIPMENT_PLANNING.md`.

## 14. Error and loading behaviour

- Render RFC-style API errors using `title`, `detail`, and validation `errors`.
- `401`: attempt one cookie-based refresh, then return to sign-in.
- `403`: show an access-denied page, not a generic error.
- `409`: explain the conflicting workflow state and refresh the record.
- `422`: show the business-rule message beside the action.
- `429`: show a retry message and respect `Retry-After` when provided.
- Every growing table requests `paged=true`; use 25 rows on compact/mobile views and 50 on normal desktop tables. Never request more than 200.
- Leave `includeTotal=false` for normal next/previous navigation. Request an exact total only when the screen visibly needs page counts.
- Staff and entity audit feeds use `paging=cursor` and the returned opaque `nextCursor`; reset the cursor chain whenever filters change.
- Tables use skeleton loading on first entry, preserve filters and existing rows during refresh, and announce the loading state accessibly.
- Debounce search, cancel superseded requests, prevent duplicate in-flight requests, and prefetch at most one next page.
- A timed-out read keeps the current filters and offers a manual retry. Writes are not automatically retried unless the endpoint explicitly supports idempotency.
- See `PAGINATION_AND_TRAFFIC_CONTROL.md` for response shapes, overload behaviour, pool sizing, and deployment checks.

## 15. Dashboard release acceptance

- Generated client matches the committed OpenAPI document.
- All role navigation and API permissions tested.
- No customer or financial truth stored only in local storage.
- Currency and timezone tests cover UK, Nigeria, and China branches.
- End-to-end tests cover lead → quote → booking → mock payment → warehouse receipt → milestone → invoice → payment → journal.
- Accessibility: keyboard navigation, labelled controls, visible focus, sufficient contrast, and screen-reader status announcements.
- Performance: first authenticated page usable within 3 seconds on normal office broadband; large tables are paginated or virtualized.
- Traffic: the dashboard respects `429` and `Retry-After`; report/export requests do not run concurrently without a user-visible reason; staging load checks cover normal office use and shift-change attendance bursts.
- Security: dependency scan, CSP, no secrets in frontend bundles, credentialed CORS only to approved origins, and no refresh token accessible to JavaScript.
