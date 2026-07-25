# BiGSAM Control Centre active API contract

Last aligned: 25 July 2026

This is the active backend contract for the staff dashboard. It replaces the older dashboard gap list and all documents that describe attendance, leave, warehouse, guest, wallet, refund, old booking, Capium invoice-import, or legacy manifest-entry workflows.

## Request rules

- Base path: `/api`.
- Staff calls use `Authorization: Bearer <access token>`.
- Send `X-Active-Branch: manchester|lagos|guangzhou|all`.
- Only Super Admin may read `all`; every mutation requires one branch.
- Manchester financial records use GBP, Lagos uses NGN, and Guangzhou uses CNY.
- Create operations that document idempotency use `Idempotency-Key: <uuid>`.
- Update operations that return an `ETag` use `If-Match` with the current row version.
- Collection responses are paginated. Respect `page`, `pageSize`, `hasNext`, and cursors where supplied.
- Invoice, receipt, credit-note, transaction, manifest, and client numbers are immutable.

## Authentication

- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`
- `GET /api/auth/me`
- `GET /api/me`
- `GET /api/me/branch-scope`
- `PATCH /api/me/active-branch`
- `GET /api/auth/sessions`
- `DELETE /api/auth/sessions/{sessionId}`

After login or refresh, call `/api/auth/me`. It returns the user, employee number, branch memberships, active branch, all-branch flag, and permissions. Do not show placeholder branch or employee data.

`/api/me` is the compatibility alias. Branch selection is stateless: the
dashboard validates a selection with `PATCH /api/me/active-branch`, stores the
returned header value, and sends it as `X-Active-Branch` on later requests.

## Managed service routes and pricing

- `GET/POST /api/service-routes`
- `GET/PATCH /api/service-routes/{routeId}`
- `POST /api/service-routes/{routeId}/archive`
- `GET/POST /api/pricing`
- `GET /api/pricing/categories`
- `PATCH /api/pricing/{priceId}`
- `GET /api/pricing/{priceId}/history`

Routes originate at the active BiGSAM hub and can end at a dynamic country,
city or region. Air and Sea are separate records. New manifests send the
selected `serviceRouteId`; shipping prices expose their linked
`serviceRouteId`. `/api/sales/shipping-prices` remains a compatibility alias
over the same catalogue, not a second price store.

## Clients and customer shipments

- `GET /api/clients?search=&page=&pageSize=&addedFrom=&addedTo=`
- `POST /api/clients/match-check`
- `POST /api/clients`
- `GET /api/clients/{clientId}`
- `PATCH /api/clients/{clientId}`
- `POST /api/clients/duplicate-reviews/{reviewId}/resolve`
- `POST /api/clients/{clientId}/merge`
- `GET /api/clients/{clientId}/history?cursor=&type=&from=&to=`
- `GET /api/clients/{clientId}/summary`
- `GET /api/clients/summary/contact-completeness`

The active client directory contains name, phone, optional email, added date, client number, and hub. Merging retains the old audit record and moves operational history to the selected client.

- `POST /api/customer-shipments`
- `GET /api/customer-shipments`
- `GET /api/customer-shipments/summary`
- `GET /api/customer-shipments/recent-updates`
- `GET /api/customer-shipments/manifest-warnings`
- `GET /api/customer-shipments/{invoiceNumber}`
- `PATCH /api/customer-shipments/{invoiceNumber}`
- `POST /api/customer-shipments/{invoiceNumber}/cancel`
- `GET /api/customer-shipments/{invoiceNumber}/timeline`

One customer shipment creates one issued sales invoice. Normal shipment invoices must never be created from a finance invoice form.
`POST /api/customer-shipments` is rejected with `409 BRANCH_BOOKINGS_PAUSED`
when the active branch has paused new shipment bookings.

## Company and branch controls

- `GET/PATCH /api/company/profile`
- `GET /api/company/branches/controls`
- `PUT /api/company/branches/{branchId}/control`

The branch-control request contains `isBookingEnabled`, `isReceivingEnabled`,
`isDispatchEnabled`, `urgentNotice`, and `reason`. The dashboard confirms each
change. Public route discovery includes the three effective operating flags so
the customer website can disable an unavailable service without hard-coded
configuration.

## Manifests

- `GET/POST /api/operations/manifests`
- `GET/PATCH/DELETE /api/operations/manifests/{manifestId}`
- `POST /api/operations/manifests/{manifestId}/lines`
- `GET/PATCH/DELETE /api/operations/manifests/{manifestId}/lines/{lineId}`
- `POST /api/operations/manifests/{manifestId}/reassign-line`
- `PUT /api/operations/manifests/{manifestId}/carrier-connection`
- `GET /api/operations/manifests/{manifestId}/print`
- `GET /api/operations/manifests/{manifestId}/report?format=pdf|xlsx|docx|csv`
- `POST /api/operations/manifests/{manifestId}/email`
- `GET /api/operations/manifests/{manifestId}/history`

The API rejects method, origin, destination and route mismatches when a line is
attached or reassigned. A manifest can be deleted only while empty, with
`If-Match` and a reason. The print response is an operations-safe PDF and
excludes costs, payment details, chargeable weight, private package contents
and commercial notes. The controlled report includes all permitted data and
returns a real PDF, XLSX, DOCX or CSV file.

Document compatibility routes:

- `GET /api/documents/manifests/{manifestId}/print`
- `GET /api/documents/manifests/{manifestId}/report?format=pdf|xlsx|csv`
- `GET /api/documents/invoices/{invoiceId}/pdf`
- `GET /api/documents/receipts/{receiptId}/pdf`
- `GET /api/documents/credit-notes/{creditNoteId}/pdf`

For the manifest workspace, request
`GET /api/operations/manifests?includeLines=true&page=&pageSize=`. The backend
loads every line for the selected page in one batch and returns it inside each
manifest. Do not issue one detail request per table row. Use `includeLines=false`
for lightweight selectors and summary cards.

## Sales documents

- `GET /api/sales/summary`
- `GET/POST /api/sales/quotations`
- `PATCH /api/sales/quotations/{quotationId}/status`
- `GET /api/sales/quotations/{quotationNumber}/document?format=pdf|xlsx|docx`

Quotation creation requires an `Idempotency-Key`, an existing client in the
active branch, a future validity date of no more than 90 days, and positive
lines. The backend owns the quotation number, branch currency, totals, status
rules, and document.

## Finance documents

- `POST /api/finance/inflows`
- `GET /api/finance/invoices`
- `GET /api/finance/invoices/{invoiceNumber}`
- `PATCH /api/finance/invoices/{invoiceNumber}`
- `POST /api/finance/invoices/{invoiceNumber}/payments`
- `GET /api/finance/receipts`
- `GET /api/finance/receipts/{receiptNumber}`
- `POST /api/finance/receipts/{receiptNumber}/void`
- `GET /api/finance/transactions`
- `GET /api/finance/credit-notes`
- `POST /api/finance/credit-notes`
- `POST /api/finance/invoices/{invoiceNumber}/apply-credit`

Receipt voiding posts a reversing transaction and journal. It does not delete the receipt or original payment.

Direct inflow creation is restricted to finance approvers and requires an
`Idempotency-Key`. It accepts supported incoming-money types and Cash, Card, or
BankTransfer, then atomically creates the transaction, receipt, and balanced
journal in the active branch currency.

Document downloads:

- `GET /api/finance/invoices/{invoiceNumber}/document?format=pdf|xlsx|docx`
- `GET /api/finance/receipts/{receiptNumber}/document?format=pdf|xlsx|docx`
- `GET /api/finance/credit-notes/{creditNoteId}/document?format=pdf|xlsx|docx`
- `GET /api/sales/quotations/{quotationNumber}/document?format=pdf|xlsx|docx`

## Provider and BiGSAM-controlled tracking

- `POST /api/tracking/providers/msc/lookup`
- `POST /api/tracking/providers/msc/sync`
- `GET /api/operations/manifests/{manifestId}/tracking`
- `PATCH /api/operations/manifests/{manifestId}/client-tracking-update`

MSC lookup/sync accepts a container number or bill of lading. Imported provider
events are internal drafts. They never become customer-visible automatically.
Staff can review/publish individual provider events or create a controlled
BiGSAM customer update through `client-tracking-update`.

## Reports

- `GET /api/reports/catalog`
- `POST /api/reports/preview`
- `POST /api/reports/exports`
- `GET /api/reports/exports/{jobId}`
- `GET /api/reports/exports/{jobId}/download`
- `DELETE /api/reports/exports/{jobId}`

Report keys are `executive-summary`, `sales`, `operations`, `finance`,
`purchases`, `inventory`, `schedule`, `clients`, and `staff`. Export downloads
are genuine `.xlsx` workbooks generated from the complete filtered server query,
not the currently loaded dashboard page. Currency values remain separated.

## Accounting

- `GET /api/accounting/accounts`
- `GET /api/accounting/accounts/{code}/entries`
- `GET /api/accounting/journals`
- `GET /api/accounting/journals/{id}`
- `POST /api/accounting/journals`
- `POST /api/accounting/journals/adjustments`
- `POST /api/accounting/journals/{id}/reverse`
- `GET /api/accounting/trial-balance`
- `GET /api/accounting/reconciliation`
- `POST /api/accounting/periods/{yyyy-MM}/close`
- `POST /api/accounting/periods/{yyyy-MM}/reopen`

Posted journals are never edited. Corrections use adjustment or reversal. A closed branch/currency period blocks new postings.

## Notifications and overview

- `GET /api/notifications`
- `GET /api/notifications/unread-count`
- `POST /api/notifications/{notificationId}/read`
- `POST /api/notifications/read-all`
- `GET /api/notifications/settings`
- `PUT /api/notifications/settings`
- `GET/PATCH /api/notification-preferences`
- `GET /api/dashboard/overview`
- `GET /api/dashboard/sales-performance`
- `GET /api/dashboard/overview/shipments`
- `GET /api/dashboard/overview/manifest-warnings`
- `GET /api/dashboard/overview/finance`
- `GET /api/overview/summary`
- `GET /api/overview/sales-performance`
- `GET /api/overview/operations`
- `GET /api/overview/alerts`

Overview endpoints are compact and branch-scoped; use them instead of loading full finance and shipment tables for dashboard cards.

## Staff administration

- `GET/POST /api/admin/staff`
- `GET/PATCH /api/admin/staff/{userId}`
- `PATCH /api/admin/staff/{userId}/status`
- `POST /api/admin/staff/{userId}/suspend`
- `POST /api/admin/staff/{userId}/reactivate`
- `POST /api/admin/staff/{userId}/archive?reason=`
- `POST /api/admin/staff/{userId}/revoke-sessions`
- `GET /api/admin/roles`
- `GET /api/admin/permissions`
- `GET /api/workforce/directory`

Delete archives the staff profile and revokes sessions; it does not remove audit or financial history.

## Legacy data import

- `GET /api/imports/template`
- `POST /api/imports`
- `POST /api/imports/preview`
- `GET /api/imports/{batchId}/sheets`
- `PUT /api/imports/{batchId}/mapping`
- `POST /api/imports/{batchId}/validate`
- `GET /api/imports/{batchId}/rows`
- `POST /api/imports/{batchId}/commit`
- `GET /api/imports/{batchId}/status`
- `GET /api/imports/{batchId}/reconciliation`
- `GET /api/imports/{batchId}/errors.xlsx`

Preview and validation are staged. Commit is audited and retains row-level duplicate/error reconciliation.
The dashboard sends the original `.xlsx` or `.csv` file as multipart field
`file`; it never converts a workbook into browser-generated records. A commit is
blocked while the preview contains error rows.

## Integrations and tracking

- `GET /api/integrations/status`
- `GET /api/integrations/messages?paged=true&page=&pageSize=&includeTotal=true`
- `GET /api/integrations/exchange-rates/latest?baseCurrency=GBP`
- `GET /api/finance/currency/rates/{baseCurrency}/{quoteCurrency}`
- `GET /api/operations/tracking/{manifestId}`
- `PUT /api/operations/tracking/{manifestId}/connection`
- `POST /api/operations/tracking/{manifestId}/sync`
- `POST /api/operations/tracking/{manifestId}/manual-events`
- `POST /api/operations/tracking/{manifestId}/events/{eventId}/review`
- `POST /api/operations/tracking/{manifestId}/events/{eventId}/publish`
- `POST /api/operations/tracking/{manifestId}/notify-affected-clients`
- `GET /api/public/tracking/{publicToken}`
- `GET /api/public/tracking/invoice/{invoiceNumber}`

There is no Capium invoice-import endpoint. Shipment creation owns invoice creation.
In live mode, the backend periodically imports due MSC/DCSA movements as drafts.
MSC/carrier data is normalized for staff review; only approved BiGSAM updates
become public. The invoice lookup never exposes the MSC reference or raw payload.

## Targeted email broadcasts

- `GET/POST /api/communications/broadcasts`
- `POST /api/communications/broadcasts/audience/preview`
- `GET /api/communications/broadcasts/{campaignId}`
- `POST /api/communications/broadcasts/{campaignId}/attachments`
- `DELETE /api/communications/broadcasts/{campaignId}/attachments/{attachmentId}`
- `POST /api/communications/broadcasts/{campaignId}/queue`
- `POST /api/communications/broadcasts/{campaignId}/cancel`
- `POST /api/communications/broadcasts/{campaignId}/retry-failed`

Audience selection supports whole client, contact, supplier, or active-staff
groups and explicit record IDs. Always preview before queueing. Attachments are
PNG, JPEG, GIF, WebP, or PDF; maximum 10 files, 20 MB each and 25 MB combined.
The upload request sends `fileName`, `contentType`, `sizeBytes`, and
`contentBase64`. The API validates declared size and file signature, stores the
bytes durably, and never returns those bytes from campaign reads or duplicates
them into the generic integration log.

## Retired contract

The following return `410 Gone` and are excluded from Swagger:

- `/api/attendance...`
- `/api/workforce/attendance...`
- `/api/workforce/leave...`
- `/api/guests...`
- `/api/customers...`
- `/api/operations/warehouse...`
- `/api/operations/bookings...`
- `/api/legacy...`
- `/api/staff...`
- `/api/cargo/refund`
- `/api/contacts/clients`
- `/api/contacts/shareholders`
- `/api/contacts/service-providers`
- `POST /api/finance/invoices`

The database keeps historical rows required for migration and audit, but these workflows are not part of the active product.
