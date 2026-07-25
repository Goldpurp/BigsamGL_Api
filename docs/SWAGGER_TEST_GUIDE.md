# Swagger testing guide

Start the API and open `http://localhost:5100`. Swagger UI is served at the root in Development.

## Administrator session

Set the bootstrap administrator environment values before the first run. Call `POST /api/auth/login`. Swagger stores the refresh cookie automatically; copy the returned access token into **Authorize** as `Bearer <token>`.

## Representative workflow

1. `GET /api/integrations/status` confirms that third-party providers are mocked.
2. `GET /api/operations/routes` obtains seeded route identifiers.
3. `POST /api/auth/register` creates a customer session.
4. Request and confirm mock email verification with `/api/auth/email-verification/*`.
5. Add two customer addresses.
6. Create a shipping quote and then a booking.
7. Sign in as an administrator and invoke mock payment confirmation.
8. Receive the booking at the warehouse to generate cargo and tracking.
9. Add a milestone or run mock carrier sync.
10. Test the tracking number using the anonymous `/api/tracking/{trackingNumber}` endpoint.
11. Create and issue an invoice, record a payment, then inspect `/api/accounting/trial-balance`.

Legacy continuation and shipment-planning checks:

1. Download `GET /api/legacy-imports/template`, add unique client, invoice, and cargo rows, then upload it to `POST /api/legacy-imports/preview` as multipart field `file`.
2. Filter `/api/legacy-imports/{batchId}/rows`, confirm there are no errors, then commit. Repeat the commit and confirm the imported count is unchanged.
3. Find the preserved old invoice number with `/api/finance/invoices?paged=true&search=...`; confirm currency comes from its issuer branch and the cargo is initially unallocated.
4. Create and edit a draft sales invoice, issue it, and confirm another direct edit returns `409`. Create an associated credit note and issue it to reduce the original balance.
5. Create an air movement, open it, and allocate the cargo. Then create/open a sea movement and an MSC container with a physical `containerType`, and reassign the same cargo.
6. Filter `/api/operations/shipment-allocations` by cargo and confirm both historical placements exist. Open `/api/operations/shipment-movements/{id}/clients` to see affected clients.

Currency and MSC checks:

1. Call `GET /api/finance/currency/country/NGA` and `/country/CHN` to confirm NGN and CNY resolution.
2. Call `POST /api/finance/currency/convert` and inspect `provider`, effective/fetched timestamps, `isFallback`, and `rateSnapshotId`.
3. Register a container with `POST /api/operations/containers` and run `POST /api/operations/containers/{id}/sync`.
4. Open the container detail to inspect the stored carrier events. Repeating the sync should report zero newly stored events.
5. Link cargo to the container, synchronize again or add a staff event, then open `/api/tracking/{trackingNumber}` to confirm the client-visible milestone.

Client identity and warehouse checks:

1. Submit name, email, phone, and an optional invoice number to `POST /api/clients/match-check`.
2. Resolve a similar match as `CreateNewClient` with a reason, or select a suggested client with `UseExistingClient`.
3. Confirm an exact name/email/phone collision has `canCreateNew: false` and cannot be bypassed.
4. Create an invoice and confirm the number matches `BGL0000INV`-style sequencing.
5. Submit an unknown label to `/api/operations/warehouse/exceptions/reconcile-scan`, inspect the generated case, assign it, and resolve it.

Staff audit checks:

1. Sign in as staff with an `X-Device-ID` header and confirm login appears as `Authentication.Login` in `/api/audit/staff-activity`.
2. Report a consented browser position through `POST /api/audit/location`.
3. Open several read and write endpoints, including one request that returns `403` or a validation error.
4. Filter staff activity by route, outcome, branch, and location; confirm duration, IP, device, session, and correlation ID are populated.
5. Add a sensitive query such as `token=test` and confirm its stored value is `[REDACTED]`.
6. Inspect `/api/audit/staff/{staffUserId}/timeline`, `/staff-activity/summary`, and `/locations/latest`.

Secure attendance checks:

1. As an attendance manager, create a station with `POST /api/workforce/attendance/stations` using a seeded branch ID and the physical branch coordinates. Copy `plaintextStationCode` because it is returned only on creation or rotation.
2. Create/select a staff profile assigned to that branch and enroll a six-digit PIN through `PUT /api/workforce/attendance/staff/{staffProfileId}/pin`.
3. Without an access token, call `POST /api/attendance/kiosk/stations/{stationPublicId}/challenge` with the station code, matching coordinates, acceptable GPS accuracy, current capture time, and a stable test device ID.
4. Confirm the returned dropdown masks email addresses and marks the enrolled staff member `attendanceReady: true`.
5. Use the one-time token with `POST /api/attendance/kiosk/record` and `type: ClockIn`. Confirm a second use of that token returns `401`.
6. Obtain a new challenge and submit `ClockOut`; confirm both events share one `sessionId`.
7. Submit a challenge with coordinates outside the radius and confirm `401`. Test wrong email/PIN, device change, duplicate check-in, and check-out without an open session.
8. Open `/api/workforce/attendance/report`, `/report/export.csv`, and `/report/print` for the staff profile. Correct a test session with a written reason and inspect the audit history.

Mock checkout URLs, carrier events, stored-document URLs, and messages do not represent real external actions.

Additional management checks:

1. Create a guest with `POST /api/guests`, assign/contact it, then convert it to a client.
2. Register an image document for that client and send it through `/api/communications/clients/{customerId}/email`.
3. Inspect the client communication history and integration message log.
4. Replace a rate through `PUT /api/operations/rates/{rateId}` and confirm the old rate remains historically dated.
5. Close a branch to bookings through `/api/company/branches/{branchId}/control` and verify booking creation returns a conflict with the operational notice.

Pagination and traffic checks:

1. Call `GET /api/staff` and confirm the compatibility response is still an array.
2. Call `GET /api/staff?paged=true&page=1&pageSize=1&includeTotal=true` and confirm the page envelope contains `items`, `hasNext`, `total`, and `totalPages`.
3. Request `pageSize=500` and confirm the returned `pageSize` is capped at 200.
4. Call `GET /api/audit/staff-activity?paging=cursor&pageSize=1`, copy `nextCursor`, and request the next page. The first record must not repeat.
5. Send `Accept-Encoding: gzip` when requesting the OpenAPI document and confirm `Content-Encoding: gzip`.
6. Repeatedly request a limited endpoint only in a disposable local/staging environment; confirm excess traffic returns `429` and a `Retry-After` header rather than destabilizing the API.

Receipts, pricing, purchasing, and staff lifecycle checks:

1. Create a sales invoice with `preferredPaymentMethod: Card`, issue it, and record a `BankTransfer` payment. Confirm the response includes a transaction, receipt, and print URL and uses settlement account `1010`.
2. Print the invoice and receipt, then verify each number/code using `/api/finance/documents/verify`.
3. Filter `/api/finance/transactions` for `MoneyIn` and the payment reference; confirm the source payment and journal are linked.
4. Create a branch price in `/api/operations/additional-service-pricing`, replace it with a later effective date, and confirm only the replacement remains active while both versions remain queryable.
5. Create a maintenance purchase, edit it, submit it, approve it with a reason, and make a partial Card payout. Confirm a `MoneyOut` transaction, payment voucher, Accounts Payable journal, and remaining balance.
6. Create a staff account, suspend it with a reason and confirm login fails, reactivate it and confirm login succeeds, then delete/archive it and confirm login fails while the staff record remains filterable as Terminated.
