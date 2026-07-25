# BiGSAM dashboard API alignment audit

Last checked: 24 July 2026

This note records the API check against the latest BiGSAM Global Logistics dashboard flow.

## Confirmed active API areas

These areas are available in the backend and match the current dashboard direction:

- Authentication and logged-in staff context:
  - `/api/me`
  - `/api/me/branch-scope`
  - `/api/me/active-branch`
- Branch-aware dashboard summary:
  - `/api/dashboard/overview`
  - `/api/overview/summary`
- Customer shipments:
  - `/api/customer-shipments`
  - Uses the protected invoice number as the shipment ID.
  - Creates the shipment and invoice together.
  - Supports paid, partial and unpaid payment states.
  - Supports client duplicate checks through the client flow.
- Sales and finance:
  - `/api/sales`
  - `/api/sales/quotations`
  - `/api/sales/shipping-prices`
  - `/api/finance/invoices/{invoiceNumber}`
  - `/api/finance/invoices/{invoiceNumber}/payments`
  - `/api/finance/receipts`
  - `/api/finance/credit-notes`
  - `/api/finance/clients/{clientId}/available-credit`
  - `/api/finance/invoices/{invoiceNumber}/apply-credit`
- Business documents:
  - `/api/finance/invoices/{invoiceNumber}/document`
  - `/api/finance/receipts/{receiptNumber}/document`
  - `/api/finance/credit-notes/{creditNoteId}/document`
  - `/api/sales/quotations/{quotationNumber}/document`
- Clients:
  - `/api/clients`
  - `/api/clients/match-check`
  - `/api/clients/{clientId}/history`
  - `/api/clients/{clientId}/summary`
  - `/api/clients/summary/contact-completeness`
- Contacts:
  - `/api/contacts`
  - `/api/contacts/clients`
  - `/api/contacts/suppliers`
  - `/api/contacts/directors`
  - `/api/contacts/staff`
  - `/api/contacts/summary`
- Schedule pickups:
  - `/api/schedule/pickups`
  - Supports create, edit, cancel/delete-style lifecycle, driver assignment and list filtering.
- Current manifest workflow:
  - `/api/operations/manifests`
  - `/api/operations/manifests/summary`
  - `/api/operations/manifests/invoice-lookup`
  - `/api/operations/manifests/{manifestId}/lines`
  - `/api/operations/manifests/{manifestId}/print`
  - `/api/operations/manifests/{manifestId}/report`
  - `/api/operations/manifests/{manifestId}/history`
- Manifest tracking and MSC provider integration:
  - `/api/operations/tracking`
  - `/api/operations/manifests/{manifestId}/tracking`
  - `/api/operations/manifests/{manifestId}/client-tracking-update`
  - `/api/tracking/providers/msc/lookup`
  - `/api/tracking/providers/msc/sync`
  - `/api/public/tracking/{publicToken}`
  - `/api/public/tracking/invoice/{invoiceNumber}`
- Global search:
  - `/api/search`
  - `/api/search/shipment-by-invoice-fragment/{digits}`
  - This supports searching the four invoice digits between `BGL` and `INV`.
- Reports:
  - `/api/reports`
  - `/api/reports/exports`
  - `/api/reports/exports/{jobId}`
  - `/api/reports/exports/{jobId}/download`
- Legacy import:
  - `/api/legacy-imports`
  - `/api/imports`
  - Supports `.xlsx` and `.csv` migration from old company data.
- Staff management:
  - `/api/admin/staff`
  - `/api/admin/roles`
  - `/api/admin/permissions`

## API changes completed in this pass

### 1. Removed retired callable backend workflows

The following old source controllers were removed:

- `Api/Controllers/WorkforceController.cs`
- `Api/Controllers/AttendanceManagementController.cs`
- `Api/Controllers/AttendanceKioskController.cs`
- `Api/Controllers/Operations/BookingsController.cs`
- `Api/Controllers/Operations/WarehouseController.cs`
- `Api/Controllers/Operations/WarehouseExceptionsController.cs`
- `Api/Controllers/Operations/ShipmentsController.cs`

The following obsolete request contracts were also removed from active API contract files:

- Attendance and leave requests
- Old operation booking requests
- Old warehouse summary and warehouse exception requests

The obsolete domain entities, DbSet registrations, service interfaces and seeded local test data were also removed. A forward cleanup migration was added:

- `Infrastructure/Persistence/Migrations/20260725093000_RemoveRetiredTestOperationalApis.cs`

That migration drops the retired test-era tables/columns for:

- Attendance records, sessions, stations and kiosk challenges
- Leave requests
- Old operation bookings
- Old shipment recipients
- Old warehouse exception cases
- Attendance PIN fields on staff profiles
- Old booking links on documents and appointments
- Old accepting-bookings/warehouse-open branch flags

### 2. Corrected Sea cargo pricing validation

The customer shipment API no longer blocks Sea shipment creation with `DIMENSIONS_REQUIRED` just because a managed Sea service contains a CBM base-rate row.

Current rule:

- Air cargo can use actual weight and chargeable/cost weight.
- Sea cargo can use selected approved box sizes and item prices.
- Negotiated Sea items can use negotiated amount and reason.
- Dimensions are optional.
- If any dimension is supplied, all three dimensions must be supplied so a CBM helper calculation is not incomplete.
- If no CBM quantity is supplied, the API must not force dimensions.

Updated file:

- `Api/Controllers/CustomerShipmentsController.cs`

## Retired endpoint protection removed

The retired endpoint middleware and Swagger document filter were removed after approval to delete the unused APIs completely. Old URLs now behave as non-existent routes unless a new active controller is later introduced for that path.

Removed:

- `Api/Middleware/RetiredEndpointMiddleware.cs`
- `Api/OpenApi/ActiveContractDocumentFilter.cs`

## Build verification note

Source reference checks passed for the deleted controllers and contracts. No active source references remain for the removed request/response types. Historical migration snapshots may still contain old table names as migration history, but the newest cleanup migration removes those physical tables/columns from the database.

`dotnet build` could not complete in this session because the local build process hung with no compiler output, even after `dotnet build-server shutdown` cleared MSBuild. I did not force-kill dotnet because that could stop the running local API server.
