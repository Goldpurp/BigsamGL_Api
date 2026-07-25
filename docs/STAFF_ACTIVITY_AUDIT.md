# Staff activity and location audit

## What is recorded

Every request made with an authenticated non-customer account creates an append-only staff activity record after the response completes. This includes reads, searches, reports, creations, changes, failed validation, denied actions, and rate-limited requests.

Each record contains:

- staff user/profile IDs plus name, email, role, employee number, and assigned branch snapshots;
- activity type, HTTP method, controller/action, route template, path, and a redacted query string;
- response status, normalized outcome, and duration;
- request IP, user agent, dashboard-generated device ID, JWT session ID, and correlation ID;
- available latitude/longitude, accuracy, source, capture time, and current/stale classification.

Successful and failed staff login attempts are written explicitly because the login request begins without an authenticated principal. Token refreshes are recorded in the same way. Customer requests are not written to the staff activity table.

The middleware never stores request or response bodies. Query keys containing password, token, secret, authorization, key, or code are saved with `[REDACTED]` instead of their values.

## Location behavior

The dashboard should request browser geolocation only after displaying the company's employee-location notice and receiving affirmative consent. Submit it to:

```text
POST /api/audit/location
```

with latitude, longitude, accuracy in metres, capture time, device ID, and `consentConfirmed: true`. A request may alternatively carry these headers:

```text
X-Staff-Location-Consent: true
X-Staff-Latitude: 6.524379
X-Staff-Longitude: 3.379206
X-Staff-Location-Accuracy: 12.5
X-Staff-Location-Captured-At: 2026-07-13T13:00:00Z
X-Device-ID: dashboard-generated-random-id
```

Header coordinates are accepted only with the consent header and a capture time within the last 24 hours. Without a position, the request uses the most recent consented browser ping for up to `Audit:MaximumLocationAgeHours`. Positions newer than `Audit:LocationFreshMinutes` are `Current`; older positions are clearly marked `Stale`. Branch and IP are still recorded when no GPS exists.

GPS and IP data are operational evidence, not infallible proof. Browser coordinates may be unavailable or manipulated, accuracy varies, and an IP address is not a precise physical location.

## Administrator APIs

- `GET /api/audit` — entity changes.
- `GET /api/audit/staff-activity` — filterable request activity.
- `GET /api/audit/staff-activity/{id}` — one request record.
- `GET /api/audit/staff-activity/summary` — actions and failures by staff/branch/outcome.
- `GET /api/audit/staff/{staffUserId}/timeline` — activity and entity changes together.
- `GET /api/audit/locations/latest` — last consented location per staff member.
- `GET /api/audit/locations/history` — filtered location history.

All read APIs require administrator authorization. Only staff can report their own location; one staff member cannot submit a location for another.

## Production governance and scale

- Define employee notice, lawful basis/consent, viewer roles, investigation approvals, retention, and deletion requirements with the company's legal/privacy advisers before enabling GPS in production.
- Keep audit tables append-only through the application. Do not expose update/delete endpoints.
- Monitor audit-write failures and alert immediately; an operational request is not rolled back merely because its after-response activity write fails.
- At high volume, move writes through a durable transactional outbox/queue and partition activity/location tables by month.
- Use a reporting replica for long investigations and dashboards, while PostgreSQL remains authoritative.
- Apply a documented retention job using privileged maintenance credentials; ordinary application staff must not be able to alter history.
- Restrict location APIs and exports more tightly than normal operational reports, and log every administrator view through the same request audit middleware.
