# Legacy data continuation and shipment planning

## Purpose

BigsamGL can begin with the company's existing Excel records instead of an empty system. Importing and shipment planning are deliberately separate: an invoice and its cargo can exist before an airline, vessel, or container is known, and staff can make or revise that operational decision later without rewriting the invoice.

PostgreSQL is the only database used. Import files are staging input, not a second datastore.

## Excel continuation workflow

Administrator endpoints:

- `GET /api/legacy-imports/template` downloads the supported workbook template.
- `POST /api/legacy-imports/preview` accepts one `.xlsx` or `.csv` file as multipart field `file`.
- `POST /api/legacy-imports/{batchId}/commit` applies a reviewed preview.
- `GET /api/legacy-imports` lists batches with status, date, file name, and search filters.
- `GET /api/legacy-imports/{batchId}/rows` lists row-level results with entity, sheet, status, and search filters.

The workbook supports `Clients`, `Invoices`, and `Cargo` sheets. Process order is clients, then invoices, then cargo so later rows can reference preserved client identities and old invoice numbers. Common historical header variants are accepted, but the supplied template should be preferred. Convert old `.xls` files to `.xlsx` before upload.

Preview is mandatory and makes no customer, invoice, cargo, journal, or payment changes. Each row is classified as valid, duplicate, or error. Staff must correct errors and preview a new file before committing. Import limits are 20 MB and 20,000 business rows per workbook.

Every file is identified by SHA-256. Re-uploading the same bytes returns the existing batch, and committing an already completed batch returns its prior result. Existing normalized client identity, invoice numbers, and cargo tracking numbers are checked to prevent silent duplication.

Historical invoices preserve their source invoice number, dates, amount, paid amount, payment state, client snapshot, and issuer branch. They do not create old accounting journal entries automatically; finance should reconcile imported opening balances separately. Newly created invoices continue the `BGL0000INV` sequence.

## Invoice rules

Invoice types are `Sales` and `CreditNote`. Payment state is independent and is one of `Unpaid`, `PartiallyPaid`, `Paid`, or `NotApplicable`.

The issuer branch controls currency: a Nigeria issuer uses NGN, a China issuer uses CNY, and a United Kingdom issuer uses GBP. The API derives this value and rejects a conflicting submitted currency.

Draft invoices can be edited with `PUT /api/finance/invoices/{id}`. This can change the client, dates, lines, discount, notes, type, original-invoice reference, and issuer branch while retaining the generated invoice number. Once issued, the financial document is immutable; corrections use a credit note linked to the original sales invoice. Issuing the credit note posts a reversing journal and reduces the original balance.

Invoice list filters include document status, invoice type, payment status, client, issuer branch, currency, issue-date range, invoice number/client-name search, and pagination.

## Late shipment assignment

1. Import or create the client, invoice, and cargo.
2. Leave cargo unallocated while the carrier decision is unknown.
3. Create an external transport movement with mode `Air` or `Sea`, route, schedule, `ThirdPartyCarrier`/`PartnerForwarder`, gateways, and the provider-issued master reference when known.
4. Move it to `OpenForAllocation`.
5. For sea freight, register a physical container type and attach it to that movement.
6. Bulk-assign one or more invoiced cargo items through `POST /api/operations/shipment-allocations`.
7. Reassign later if the airline, sailing, or container changes. The former allocation is closed; it is never overwritten.

Air allocation requires a flight number and cannot use a container. Sea allocation requires a container belonging to the selected movement. Supported physical types include 20/40-foot dry, 40-foot high-cube, refrigerated, open-top, flat-rack, tank, shared LCL, other, and not specified.

The linked movement represents capacity arranged with an external carrier or partner forwarder. The BigsamGL consolidation batch is an internal grouping and does not mean BigsamGL operates the airline, vessel or container. Dispatch requires a carrier-issued master AWB, bill of lading or booking reference; the system does not invent one.

Only one current allocation may exist for a cargo item. PostgreSQL enforces this with a partial unique index, while the allocation service uses a serializable transaction and retry handling so concurrent staff cannot create two active placements. A cargo item keeps its invoice link when unassigned.

Movement filters include mode, status, carrier, origin, destination, departure range, and search. Cargo filters include invoice, movement, container, assigned/unassigned state, transport mode, client, status, and search. Allocation history filters by cargo, invoice, movement, container, mode, active/history state, and date range. All growing results are paginated and capped.

`GET /api/operations/shipment-movements/{id}/clients` produces the affected-client view for a shipment, including each client's invoice/cargo totals and contact information. Tracking updates continue through the existing milestone, container, carrier, and batch-notification services.

## Dashboard acceptance flow

The internal dashboard should provide a Data Migration screen with template download, drag-and-drop upload, preview counters, an error/duplicate grid, filtered row export/display, and a guarded Commit action. Commit must be disabled while any error rows remain.

Shipment Planning should have an unallocated-cargo queue and a movement detail board. Staff select invoiced cargo, choose an open movement and, for sea freight, a compatible container. A reassignment confirmation must show the current and proposed placements and require a reason. The cargo detail displays the complete allocation timeline.

Never store import workbooks or assignment decisions only in browser state. The API and PostgreSQL records are the source of truth.
