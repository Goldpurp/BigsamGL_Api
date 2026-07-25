# Client identity and warehouse exception management

## Client identity rules

Client names, email addresses, and phone numbers are normalized at the persistence boundary, regardless of which API changes the record. PostgreSQL unique indexes are the final concurrency safeguard:

- names collapse repeated spaces and compare case-insensitively;
- emails are trimmed and compare case-insensitively;
- phones compare by digits while retaining the entered display value;
- empty phone numbers remain nullable and do not collide;
- user account full names are normalized and unique separately from client records.

Exact collisions cannot be overridden. A likely but non-exact match creates an auditable 30-minute review. Staff must resolve it as either an existing client or an approved separate client and provide a reason. The original input, suggestions, selected client, decision, actor, and timestamps remain in PostgreSQL.

Suggested dashboard flow:

1. Debounce the client form and call `POST /api/clients/match-check` once enough identity details are present.
2. If `requiresConfirmation` is false, continue normally.
3. Otherwise display matching fields and confidence in a blocking modal.
4. Resolve the review with `UseExistingClient` or, when permitted, `CreateNewClient`.
5. Supply `duplicateReviewId` to customer creation, guest conversion, or client update.
6. If the review expired or details changed, run a new check.

Invoice numbers are allocated by a PostgreSQL sequence and formatted `BGL{number:D4}INV`. The first identifier is `BGL0000INV`; formatting grows beyond four digits without rollover. Sequence gaps are normal when a transaction is abandoned and numbers are never recycled.

## Warehouse exception lifecycle

`POST /api/operations/warehouse/exceptions/reconcile-scan` compares a scanned tracking/marker/carrier code with the expected client and location. It can produce one or more cases without losing the scan result.

Open cases retain:

- expected and observed client, tracking, marker, and location;
- exception type, severity, assignment, description, and document evidence IDs;
- linked cargo and client-visible state;
- original report, subsequent changes, resolution summary, and timestamps.

Linked cargo is placed on hold. Resolution may update the correct cargo client, associated booking ownership, all piece locations, and the restored cargo status. If staff makes the case client-visible, the same shipment milestone pipeline updates public/customer tracking and optionally sends the notification.

## Scaling path

The implementation remains a modular monolith so operational changes can be delivered quickly while transaction boundaries are still evolving. Growth should happen behind the existing interfaces:

1. Add barcode/QR label printing and mobile scanner clients to the reconciliation API; later add RFID portal readings as another observation source.
2. Move fuzzy identity matching to PostgreSQL `pg_trgm` or a dedicated search index when client volume makes the current bounded candidate search insufficient.
3. Add an outbox and background workers for client notifications, carrier synchronization, document processing, and exception escalations.
4. Introduce configurable severity SLAs, automatic supervisor escalation, branch-specific holding areas, and stock-count sessions.
5. Partition high-volume audit, milestone, and carrier-event tables by time; keep operational lookup indexes small.
6. Send reporting queries to a read replica or warehouse while PostgreSQL remains the authoritative transaction store.
7. Integrate a dedicated WMS/accounting service through application interfaces and idempotent events instead of coupling provider fields to cargo or client tables.
8. Add tenant/company keys only if the product becomes multi-company; do not introduce them prematurely into every query.

These seams allow faster growth without rewriting invoice, client, cargo, or public tracking contracts.
