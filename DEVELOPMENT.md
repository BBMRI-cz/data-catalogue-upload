# Development

Setup and local-run guide for the **.NET solution**. All commands run from the repo root.

## Prerequisites

- **.NET SDK 10** (pinned in `global.json`). Check with `dotnet --version`.
- **Docker** (for the two PostgreSQL databases).
- EF Core CLI tooling is restored as a local tool: `dotnet tool restore` (uses `dotnet-tools.json`).

## Install & verify

```bash
dotnet tool restore                       # dotnet-ef local tool
dotnet restore DataCatalogueUpload.slnx
dotnet build DataCatalogueUpload.slnx -c Release   # warnings are errors
dotnet test DataCatalogueUpload.slnx               # unit + integration tests
dotnet format DataCatalogueUpload.slnx --verify-no-changes   # lint/format check
```

## Databases

Each service owns its own PostgreSQL database, and each lives in its own compose file so the
stacks can be deployed on separate machines. Start the two you need:

```bash
docker compose -f compose.uploader.yml up -d uploader-db
docker compose -f compose.biobank.yml up -d biobank-db
```

- `uploader-db` -> `localhost:5432`, database `data_catalogue_upload`
- `biobank-db` -> `localhost:5433`, database `biobank_api`
- `sequencing-db` -> `localhost:5434`, database `sequencing_api`

Both default to `postgres` / `postgres`.

## Configuration

Configuration is read from **environment variables** (no `.env` files are tracked). Defaults in
`BiobankOptions` / `SequencingOptions` / `UploaderOptions` match the Docker databases above.

**biobank_api:** `POSTGRES_USER|PASSWORD|DB|HOST|PORT`, `BIOBANK_HOST|PORT`, `BIOBANK_XML_EXPORT_PATH`.
For local runs against `biobank-db`, set `POSTGRES_PORT=5433`.

**sequencing_api:** `POSTGRES_USER|PASSWORD|DB|HOST|PORT`, `SEQUENCING_HOST|PORT`,
`SEQUENCING_DATA_PATH` (organised run tree), `SEQUENCING_LIBRARIES_PATH` (libraries table + BEDs),
`SEQUENCING_MAPPING_TABLE_PATH` (pseudonymizer mappings), `SEQUENCING_INGEST_CRON`. For local runs
against `sequencing-db`, set `POSTGRES_PORT=5434`.

**uploader:** `POSTGRES_USER|PASSWORD|DB|HOST|PORT`, the source and catalogue URLs,
`PSEUDONYM_PREFIX` (default `mmci`) - the biobank prefix on every pseudonym the uploader mints, see
[`docs/pseudonymization.md`](docs/pseudonymization.md) - and `UPLOADER_SYNC_CRON`, the Quartz
schedule in UTC (default `0 0 2 ? * MON,WED,FRI`).

| Variable | Default | Notes |
|---|---|---|
| `BIOBANK_API_URL` | `http://localhost:8001` | |
| `SEQUENCING_API_URL` | `http://localhost:8002` | matches what `compose.sequencing.yml` publishes |
| `RADIOLOGY_API_URL` | *(blank)* | no service yet (#29); blank means never contacted |
| `WSI_API_URL` | *(blank)* | no service yet (#31); blank means never contacted |
| `CATALOGUE_API_URL` | `http://localhost:8000` | |

**A blank source URL means "not deployed".** No HTTP client is registered for it and the uploader
never contacts it - the source simply answers empty. A source that *is* configured but unreachable
costs the affected patients that source's data, is counted under `source_unavailable` in the run
summary, and the run carries on. See [`docs/catalogue-api-contract.md`](docs/catalogue-api-contract.md)
for the catalogue side.

Everything here assumes one machine. Deploying onto the two servers that actually hold the source
data is [`docs/deployment.md`](docs/deployment.md).

Secrets (notably the catalogue token) go in `.env`, which is git-ignored; copy `.env.example` and
fill it in. Docker reads it via `env_file`; for a bare `dotnet run`, use
`dotnet user-secrets --project src/Uploader/Uploader.Host` or export the variables.

## Migrations (EF Core)

```bash
# biobank_api
dotnet ef migrations add <Name> \
  --project src/BiobankApi/BiobankApi.Infrastructure \
  --startup-project src/BiobankApi/BiobankApi.Web \
  --output-dir Persistence/Migrations
dotnet ef database update \
  --project src/BiobankApi/BiobankApi.Infrastructure \
  --startup-project src/BiobankApi/BiobankApi.Web

# sequencing_api (swap the project/startup paths)
dotnet ef migrations add <Name> \
  --project src/SequencingApi/SequencingApi.Infrastructure \
  --startup-project src/SequencingApi/SequencingApi.Web \
  --output-dir Persistence/Migrations

# uploader (swap the project/startup paths)
dotnet ef migrations add <Name> \
  --project src/Uploader/Uploader.Infrastructure \
  --startup-project src/Uploader/Uploader.Host \
  --output-dir Persistence/Migrations
```

At runtime all three services apply their migrations on startup when `RUN_MIGRATIONS=true` is set
(the containers set it; tests leave it unset).

## Running the services

```bash
# biobank API server (http://localhost:8001)
RUN_MIGRATIONS=true POSTGRES_PORT=5433 dotnet run --project src/BiobankApi/BiobankApi.Web

# start biobank ingestion on the running API (also runs weekly via Quartz), then check on it
curl -X POST http://localhost:8001/admin/ingest    # 202 started, 409 one is already running
curl http://localhost:8001/admin/ingest            # {"running": false, "last_run": {"ingested": ..., "errors": [...]}}

# sequencing API server (http://localhost:8002)
RUN_MIGRATIONS=true POSTGRES_PORT=5434 dotnet run --project src/SequencingApi/SequencingApi.Web
curl -X POST http://localhost:8002/admin/ingest
curl http://localhost:8002/admin/ingest            # {"running": false, "last_run": {"ingested_samples": ..., "error_count": ...}}

# uploader sync service (http://localhost:8003; syncs Mon/Wed/Fri 02:00 UTC, and on demand)
RUN_MIGRATIONS=true ASPNETCORE_URLS=http://localhost:8003 dotnet run --project src/Uploader/Uploader.Host
curl -X POST http://localhost:8003/admin/sync      # 202 started, 409 one is already running
curl http://localhost:8003/admin/sync              # {"running": false, "last_run": {"uploaded": ..., "failure": null}}
```

All three triggers return at once and run in the background; the `GET` beside each reports the
latest run - `finished_at` null with `running` false means it was cut short (a crash or a restart),
and `failure` says why a run could not do anything at all.

## Tests

- `*.UnitTests` - pure tests (domain services, planner, builders, handlers with fakes).
- `*.IntegrationTests` - EF Core against in-memory SQLite via the `SqliteDatabase` helper; all three
  hosts are exercised end-to-end with `WebApplicationFactory<Program>`, over a throwaway SQLite file
  (a run happens in the background while the test polls, which one in-memory connection cannot
  serve).

```bash
dotnet test DataCatalogueUpload.slnx                                  # everything
dotnet test tests/Uploader.UnitTests/Uploader.UnitTests.csproj  # one project
```

## Containers

One compose file per stack. Run them all on one machine, or one per machine — see
[`docs/deployment.md`](docs/deployment.md).

```bash
docker compose -f compose.biobank.yml up -d --build       # biobank-db + biobank-api    :8001
docker compose -f compose.sequencing.yml up -d --build    # sequencing-db + sequencing-api :8002
docker compose -f compose.uploader.yml up -d --build     # uploader-db :5432 + uploader :8003

curl -X POST http://localhost:8001/admin/ingest           # ingest on demand
curl -X POST http://localhost:8003/admin/sync             # sync on demand
curl http://localhost:8003/admin/sync                     # how the latest sync went
```

The three stacks are three separate Compose projects on three separate networks even on one host, so
container names do not resolve between them. The uploader addresses the source APIs as
`host.docker.internal`; `.env.example` covers it.

The Dockerfiles build with the repo root as their context (central package management +
project references). All three are long-running services with `restart: unless-stopped`; each runs
its own work on an in-process Quartz schedule.

## CI

[`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml) restores, runs
`dotnet format --verify-no-changes`, builds in Release (warnings as errors), and runs the tests.
Run those three locally before pushing.
