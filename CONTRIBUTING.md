# Contributing to BiGSAM Global Logistics

## Working agreement

Use a short-lived branch from `main`. Keep one business outcome per pull
request. Do not mix formatting, schema changes, contract changes and unrelated
features in one review unless they are inseparable.

Suggested branch names:

- `feature/manifest-client-notifications`
- `fix/invoice-duplicate-check`
- `chore/dotnet-servicing-update`

At least one other developer reviews changes to authentication, authorization,
finance, migrations, customer identity, auditing, attendance, carrier
integrations or production configuration.

## Local setup

1. Install the SDK selected by `global.json`, PostgreSQL 16, `jq`, and the
   PostgreSQL client tools.
2. Copy `.env.local.example` to the ignored `.env.local`.
3. Fill the local database and optional bootstrap administrator values.
4. From `Api/`, run `dotnet watch run` for hot reload. Use
   `bash scripts/run-local.sh` from the repository root for a release-style
   local launch.
5. Run the dashboard from `/Users/mac/Documents/BigsamGL_Dashboard` and the
   customer website from `/Users/mac/Desktop/bigsam-global-logistics`.

Never commit `.env.local`, database dumps, PFX files, provider payloads,
customer spreadsheets, access tokens or real customer details.

## Architecture boundaries

- `Domain` owns entities, enums, value objects and business invariants. It does
  not depend on another project.
- `Application` owns use cases, validation, DTOs and interfaces.
- `Infrastructure` implements PostgreSQL, identity and external adapters.
- `Api` owns HTTP contracts, authorization, middleware and composition.
- `Tests` covers business rules and real PostgreSQL integration behavior.

Do not return EF entities directly from controllers. Public contracts live
under `Api/Contracts`. Shared provider interfaces live under
`Application/Common/Interfaces`. Provider-specific parsing belongs in
`Infrastructure/Services`.

## API and frontend changes

An API contract change is complete only when:

1. request/response contracts and validation are updated;
2. authorization and branch scope are verified;
3. Swagger is checked;
4. dashboard adapters/types are updated;
5. customer-site integration documentation is updated when public behavior
   changes;
6. compatibility or rollout notes appear in the pull request.

The browsers must send `X-BiGSAM-App-Environment` as `local` or `production`.
Never put secrets in a `VITE_*` value because those values are shipped to every
browser.

## PostgreSQL migrations

Create migrations from the solution root using the pinned local tool:

```bash
dotnet tool restore
dotnet tool run dotnet-ef migrations add <MigrationName> \
  --project Infrastructure \
  --startup-project Api
```

Migration pull requests include the migration, model snapshot, data/backfill
impact, lock risk, estimated duration, rollback/restore plan and evidence that
tests pass from an empty PostgreSQL database. Production migrations run as a
single release step; API startup does not apply them.

## Required checks

Run:

```bash
bash scripts/verify.sh
```

The pull request checklist additionally requires secret review, affected
permission tests and frontend builds when their contract changed.
