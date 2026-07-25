# Pagination and traffic control

This contract keeps the customer website and internal dashboard responsive as records and staff usage grow. PostgreSQL is the only data store; list filtering, ordering, paging, totals, and aggregation stay on the server.

## Dashboard list contract

Growing list endpoints retain their original array response for the current web application. New dashboard screens should opt into paging:

```http
GET /api/staff?paged=true&page=1&pageSize=50&includeTotal=false
```

```json
{
  "items": [],
  "page": 1,
  "pageSize": 50,
  "hasPrevious": false,
  "hasNext": true,
  "total": null,
  "totalPages": null
}
```

- `page` starts at 1.
- `pageSize` is clamped to 1–200. Use 25 for compact/mobile views and 50 for normal dashboard tables.
- Leave `includeTotal=false` during ordinary browsing. The API reads one extra row to determine `hasNext` without running an additional count query.
- Use `includeTotal=true` only when the interface must show an exact record/page count.
- Keep filters in the URL and reset `page` to 1 whenever a filter or sort changes.
- Existing array responses are bounded for compatibility and must not be used to implement an unlimited client-side table.

Opt-in page envelopes are available for staff, clients/customers, guests, leads, invoices, cargo search and batches, containers, warehouse exceptions, customer communications, integration messages, attendance sessions, leave requests, sales activities, journals, suppliers, customer wallet history, and the signed-in customer's quotations and bookings. Swagger is the source of truth for each route's available filters.

## Cursor feeds

Audit history is append-heavy and can change while an administrator is browsing. Use cursor paging to avoid offset drift:

```http
GET /api/audit/staff-activity?paging=cursor&pageSize=50
GET /api/audit/staff-activity?paging=cursor&pageSize=50&cursor={nextCursor}
GET /api/audit?paging=cursor&pageSize=50
```

```json
{
  "items": [],
  "pageSize": 50,
  "hasMore": true,
  "nextCursor": "opaque-value"
}
```

Treat `nextCursor` as opaque. Do not decode, construct, or store it as a permanent bookmark. Reset it when filters change. The database indexes the same timestamp/ID pair used for ordering so continuation remains efficient as audit data grows.

## Frontend loading behaviour

- Fetch page 1 on entry and render table skeletons rather than a full-page spinner.
- Keep the previous page visible with a subtle loading state during refresh or page navigation.
- Debounce text search by approximately 250 milliseconds and cancel superseded requests with `AbortController`.
- Prevent duplicate requests for the same route, filters, page, and user session.
- Prefetch at most the next page, and only after the current page succeeds.
- Do not retry validation failures or writes automatically. A write may only be retried when its contract is explicitly idempotent.
- Virtualize rendered rows if the UI intentionally displays more than 200 already-fetched records.
- On `429`, respect `Retry-After`, disable the triggering control for that interval, and show a calm retry message.
- On a request timeout, keep the user's filters and offer a manual retry. Never turn a timeout into an unlimited retry loop.

## Server protection

The API applies gzip/Brotli response compression, a 30-second default request deadline, a 60-second report deadline, and a 15-second carrier-webhook deadline. It also separates traffic by workload:

- Up to 200 active application requests per API instance, with a small oldest-first queue.
- Authenticated traffic is partitioned by account ID; anonymous traffic is partitioned by IP address.
- A token bucket smooths request bursts instead of allowing unbounded work to enter the application.
- Authentication allows a shared-office burst while account lockout remains the credential-abuse control.
- Attendance permits a large shift-change burst from one branch network while station, token, PIN, device, geofence, and anti-replay checks remain enforced.
- Public tracking, anonymous guest creation, provider webhooks, and expensive reports have separate limits.
- Reports allow only three concurrent requests per caller, preventing exports from starving ordinary operational work.
- Rejected traffic returns HTTP `429`; a request that cannot enter the global concurrency queue returns quickly instead of exhausting PostgreSQL connections.

These controls are in-process and therefore apply per API instance. In a multi-replica production deployment, enforce a second shared limit at the load balancer/API gateway when a company-wide quota is required.

## PostgreSQL capacity

Each API instance uses a bounded Npgsql pool. Defaults are a minimum of 2 and maximum of 50 connections in production, and a maximum of 30 in development. Connection acquisition has a short deadline; commands have a bounded deadline and transient failures are retried as a complete unit of work.

Set pool size from measured database capacity:

```text
API instance count × Database:MaxPoolSize ≤ PostgreSQL connections reserved for the API
```

Leave capacity for migrations, monitoring, backups, and administration. Introduce PgBouncer and a load balancer before increasing replica count significantly. Do not solve slow queries only by increasing pool size; inspect query plans and indexes first.

## Production verification

Before each high-traffic release:

1. Apply migrations once before starting multiple replicas.
2. Run the automated Release build and PostgreSQL integration tests.
3. Load-test representative reads, writes, logins, shift attendance, tracking, and reports in staging with production-like data volume.
4. Observe p50/p95/p99 latency, HTTP `429`/`5xx`, timeout count, database pool wait time, active PostgreSQL connections, CPU, memory, and slow queries.
5. Increase limits only from measured evidence; keep the database pool below its safe connection budget.

