# Secure no-login attendance kiosk

## Purpose

The attendance kiosk lets a staff member scan a branch QR code or barcode, confirm their identity, and check in or out without signing into the management dashboard. The backend remains the source of truth for time, branch, staff identity, location verification, session pairing, reporting, and audit evidence.

This flow is separate from normal application authentication. A staff member still needs an active staff profile and a six-digit personal attendance PIN. Administrators can enroll a PIN, and a signed-in staff member can change their own PIN.

## Dashboard pages

### Attendance stations

Permission: `SuperAdmin`, `Administrator`, or `OperationsManager`.

Use `POST /api/workforce/attendance/stations` to create one station per physical check-in point. Configure the branch, exact latitude/longitude, geofence radius, and maximum accepted GPS accuracy. The response returns:

- station metadata;
- `kioskUrl` and `barcodePayload`, which contain the station public ID and secret;
- `plaintextStationCode`, shown only when the station is created or its code is rotated.

Render `barcodePayload` as a QR code and optionally as a Code 128 barcode. Print and place it only at the physical branch. The station secret is in the URL fragment after `#`; the browser must parse it locally so normal web-server query logs do not receive it.

Use `GET /api/workforce/attendance/stations` for the station list, `PUT /api/workforce/attendance/stations/{stationId}` to change location/security settings or disable it, and `POST /api/workforce/attendance/stations/{stationId}/rotate-code` when a printed code is lost or copied. Rotation immediately invalidates the old printed code; print the replacement.

### Public attendance kiosk

The route must be accessible without a dashboard session, for example `/attendance/kiosk#station=...&code=...`.

1. Read `station` and `code` from the URL fragment. Do not place either value in analytics, error telemetry, or a query string.
2. Create and retain a random browser device ID in local storage for this kiosk workflow. It is an anti-reuse signal, not a permanent hardware identity.
3. Request a fresh browser position with high accuracy. Do not reuse a cached location.
4. Call `POST /api/attendance/kiosk/stations/{stationPublicId}/challenge` with the station code, coordinates, GPS accuracy, capture time, and device ID.
5. On success, show the returned active staff dropdown. It contains full name, masked email, staff profile ID, and `attendanceReady`. Do not expose full emails in the public list.
6. The staff member selects their name, types their full work email and six-digit attendance PIN, then chooses **Check in** or **Check out**.
7. Capture GPS again and call `POST /api/attendance/kiosk/record` with the one-time challenge token. Disable repeat submission while it is pending.
8. Show the server-recorded time and result. Clear the entered email/PIN and challenge token immediately.

The challenge expires after five minutes and can be used once. The API rechecks device ID, GPS radius, GPS accuracy, station state, staff branch, active account, exact email, PIN, recent device reuse, and open-session state. Time is always taken from the server. Five failed PIN attempts lock attendance PIN use for fifteen minutes. Public attendance endpoints are rate limited.

### Staff profile attendance section

`GET /api/staff` and `GET /api/staff/{userId}` include `staffProfileId` and `attendancePinConfigured`. Use the profile ID for attendance filtering and management actions.

- Administrator enrollment: `PUT /api/workforce/attendance/staff/{staffProfileId}/pin`.
- Signed-in staff self-service: `PUT /api/workforce/attendance/pin`.
- Profile history: `GET /api/workforce/attendance/sessions?staffProfileId={staffProfileId}&from=&to=`.

Never display or return a PIN hash. After PIN submission, clear the field and prevent browser password/autofill storage where the chosen UI library permits it.

### Reports and corrections

Permission: `SuperAdmin`, `Administrator`, or `OperationsManager`.

- `GET /api/workforce/attendance/report?from=&to=&staffProfileId=&branchCode=` returns totals, per-staff summaries, and session rows.
- `GET /api/workforce/attendance/report/export.csv?...` downloads a spreadsheet-compatible export.
- `GET /api/workforce/attendance/report/print?...` returns a print-ready report with a browser print action.
- `POST /api/workforce/attendance/sessions/{sessionId}/correct` corrects a paired session and requires a written reason.

The default report period is the current UTC month and a custom period can cover up to one year. The dashboard must translate a branch-local date range to UTC before calling the API, then render returned UTC timestamps in the branch timezone. Mixed-branch reports should label the timezone per branch or display UTC explicitly.

Corrections do not overwrite the original verification events in `attendance_records`. The paired session carries the corrected reporting result, reviewer, review time, and reason, while entity audit history retains the before/after values.

## Data model and scaling boundary

- `attendance_stations`: branch geofence and rotated secret hash.
- `attendance_kiosk_challenges`: one-time, expiring location/device evidence and rejected-attempt reason.
- `attendance_sessions`: paired check-in/check-out, calculated minutes, review state, and correction metadata.
- `attendance_records`: event-level verification evidence for each successful action.
- `staff_profiles`: PIN hash, failed-attempt counter, lockout, and PIN-updated time only.

Foreign keys and report lookup columns are indexed. PostgreSQL enforces valid coordinates/radii, valid states, non-negative duration values, and one open session per staff profile through a partial unique index. This separation allows payroll, shift scheduling, overtime, biometric readers, or access-control devices to be added without reshaping the existing kiosk or report records.

## Anti-fraud limits and recommended next controls

The implemented controls materially reduce casual buddy-punching and replay: physical station secret, strict geofence plus accuracy, one-time challenge, server time, exact staff identity, personal PIN, branch matching, recent cross-staff device blocking, and a retained evidence trail.

Browser GPS can still be spoofed on a compromised device and a staff member can share a PIN. No browser-only system can guarantee otherwise. For higher assurance, add managed-device attestation or passkeys, a fixed tablet in kiosk mode, a supervisor/photo confirmation policy, or a biometric/access-control reader. These additions should feed the existing attendance service and record their verification source rather than replacing report sessions.
