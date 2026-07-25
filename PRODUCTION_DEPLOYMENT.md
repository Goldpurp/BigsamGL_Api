# BiGSAM API production deployment

This procedure is platform-neutral. Adapt the exact service names to the chosen
host, but do not weaken the gates.

## Infrastructure

- PostgreSQL 16 on a private network, with backups and a least-privileged
  application user.
- One migration job using the compiled `./migrate` bundle.
- One or more API containers behind a trusted TLS reverse proxy.
- Persistent storage for Data Protection keys.
- A PFX certificate supplied through a secret file or base64 secret.
- Central logs, uptime monitoring and alerts for readiness, 5xx rates, queue
  failures and carrier synchronization failures.

## Release order

1. Run the API, dashboard and customer-site CI gates.
2. Build immutable container images and record their digests.
3. Back up PostgreSQL and verify that the backup can be restored.
4. Rehearse the migration bundle on a restored copy.
5. Put provider credentials and configuration in the secret manager.
6. Run `./migrate --connection "<production connection>"` exactly once.
7. Deploy the API and wait for `/health/ready`.
8. Deploy the dashboard and customer website.
9. Run the acceptance checklist.

The API refuses to start when migrations are pending, the database is
unreachable, production uses loopback/insecure PostgreSQL without an explicitly
trusted private network, Swagger is enabled, automatic migrations are enabled,
hosts/origins are unsafe, proxy trust is missing, or Data Protection keys are
not persisted and certificate protected.

## Secrets

Use `.env.production.example` as the inventory, not as a deployable file.
Rotate JWT, database, email, carrier webhook, MSC, exchange-rate and PFX secrets
through the hosting platform. Remove bootstrap administrator credentials after
the first controlled sign-in.

## Acceptance

- `/health/live` and `/health/ready` return 200 through the public edge.
- An untrusted Host header is rejected.
- An untrusted CORS origin receives no allow-origin header.
- A local client header is rejected by the production API.
- Swagger is unavailable publicly.
- Login, refresh rotation, logout and suspended-user behavior pass.
- Branch isolation and universal administrator controls pass.
- Invoice, quotation, receipt, manifest, tracking and report workflows pass.
- Customer invoice tracking exposes only reviewed BiGSAM events.
- Audit records contain actor, branch, correlation ID and permitted location
  metadata without secrets.
- MSC and email remain explicitly labelled non-live until provider onboarding
  and controlled end-to-end tests pass.
- Rollback and PostgreSQL restore have been rehearsed.

## Framework lifecycle

The current release line is .NET 8. Keep it on the latest patch while the
planned .NET 10 LTS upgrade is developed and tested. Complete that upgrade
before .NET 8 support ends in November 2026.
