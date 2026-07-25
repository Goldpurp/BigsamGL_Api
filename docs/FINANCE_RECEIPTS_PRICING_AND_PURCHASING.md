# Receipts, transaction control, additional pricing, and purchasing

## Financial document control

Invoice identifiers remain PostgreSQL-sequence numbers in the `BGL0000INV` family and have a database unique index. Every new/imported invoice also has a separate 128-bit verification code. The number is the business identifier; the verification code is the authenticity check and must not be treated as a login secret.

Sales invoices record a preferred payment method: `Cash`, `BankTransfer`, or `Card`. Credit notes do not have a payment method. Cash posts to ledger account `1000` (Cash on Hand). Bank transfer and card remain distinct transaction methods but both post to `1010` (Bank and Card Clearing). An optional active bank-account record can identify the particular account used; its currency must match the transaction.

Recording an invoice payment performs one short serializable PostgreSQL transaction. It locks the invoice, prevents overpayment and duplicate references, updates the payment status, posts the balanced journal, creates a money-in transaction, and issues a receipt. Sequence gaps are allowed after rolled-back work; numbers are never reused.

Endpoints:

- `POST /api/finance/payments` records the payment and returns payment, transaction, receipt, and print URL.
- `GET /api/finance/transactions` is the paginated money-in/money-out register.
- `GET /api/finance/receipts` lists receipts and payment vouchers.
- `GET /api/finance/receipts/{id}` returns one financial document.
- `GET /api/finance/receipts/{id}/print` returns print-ready HTML.
- `GET /api/finance/invoices/{id}/print` returns a print-ready invoice.
- `GET /api/finance/documents/verify?documentNumber=&verificationCode=` verifies an invoice, receipt, or voucher with minimal public data and public-read rate limiting.

Transaction filters include direction, type, status, payment method, currency, branch, date range, reference/counterparty search, and pagination. Receipt filters include document type/status, currency, dates, search, and pagination.

Existing invoices receive verification codes during migration. Existing payments are backfilled into the transaction register and receive generated receipts, so upgrading does not discard earlier payment history.

## Additional Service Pricing

The dashboard label is **Additional Service Pricing**. It covers controlled add-on charges such as battery/dangerous-goods handling, NAFDAC or other regulatory processing, boxes and packaging, same-day delivery, storage, handling, and insurance.

API: `/api/operations/additional-service-pricing`.

Each price has code, name, description, category, unit, amount, currency, branch, effective dates, and active state. Branch prices use that branch's operational currency. A code can have many historical versions but only one active version per branch. Replacing a price closes the old effective period and creates a linked new record; past invoices and quotations can therefore retain the price that was valid at the time.

Categories: Packaging, Regulatory, DangerousGoods, Delivery, Handling, Storage, Insurance, and Other. Units: Each, PerKilogram, PerBox, FlatRate, and Percentage.

The API supports category/unit/branch/currency/effective-date/search filters and bounded pagination. Deleting a price deactivates it; historical versions are retained.

## Purchasing and payables

The dashboard label is **Purchasing & Payables**. This covers maintenance, professional and logistics services, supplies, equipment, utilities, rent, regulatory costs, and other company purchases.

API: `/api/purchasing`.

Workflow:

1. Create a draft purchase with supplier/vendor, category, branch, dates, currency, reference, description, and itemized lines.
2. Edit while draft.
3. Submit for review.
4. A finance approver approves or rejects with a mandatory reason.
5. Approval posts operating expense and input tax against Accounts Payable.
6. Record one or more payouts. Partial payouts retain the remaining balance; the final payout marks the purchase paid.
7. Every payout posts Accounts Payable against cash or bank/card clearing, creates a money-out transaction, and issues a uniquely numbered printable payment voucher.

Purchase numbers use `BGL000001PUR`; payouts use `BGL000001PAY`; vouchers use `BGL000001PV`; transactions use `BGL00000001TRX`. PostgreSQL sequences and unique indexes enforce non-reuse. Reference, amount, currency, bank-account, and outstanding-balance rules are validated before posting.

List endpoints are server-filtered and paginated. Purchases filter by status, category, supplier, branch, currency, dates, and search. Payouts filter by purchase, payment method, currency, dates, and reference/number search.

## Staff administration

Administrators can create and edit staff, suspend/reactivate accounts, or delete/archive them. Delete is intentionally a soft deletion: the account becomes Disabled, the staff profile becomes Terminated, refresh sessions and attendance PIN are revoked, and the archive reason/time/administrator are retained. Financial, attendance, shipment, communication, and audit history are never cascaded away.

The API prevents self-suspension/deletion and prevents the final active SuperAdmin from being suspended, archived, or demoted. Lifecycle actions require a reason. Reactivation restores account and staff status but does not reveal or restore the old attendance PIN.

Endpoints:

- `POST /api/staff`
- `PUT /api/staff/{userId}` for non-lifecycle profile and role changes
- `PATCH /api/staff/{userId}/status`
- `POST /api/staff/{userId}/suspend`
- `POST /api/staff/{userId}/reactivate`
- `DELETE /api/staff/{userId}?reason=` for archive/termination

