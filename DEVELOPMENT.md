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

Each service owns its own PostgreSQL database. Start both with Docker:

```bash
docker compose -f compose.prod.yml up -d uploader-db biobank-db
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

**sequencing_api** (ingestion still stubbed): `POSTGRES_USER|PASSWORD|DB|HOST|PORT`, `SEQUENCING_HOST|PORT`,
`SEQUENCING_DATA_PATH`, `SEQUENCING_INGEST_CRON`. For local runs against `sequencing-db`, set
`POSTGRES_PORT=5434`.

**uploader:** `POSTGRES_USER|PASSWORD|DB|HOST|PORT` plus the five API URLs
`BIOBANK_API_URL`, `RADIOLOGY_API_URL`, `SEQUENCING_API_URL`, `WSI_API_URL`, `CATALOGUE_API_URL`.

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

At runtime the **uploader** applies migrations on startup; the **biobank_api** and **sequencing_api**
apply them when `RUN_MIGRATIONS=true` is set (the container sets it; tests leave it unset).

## Running the services

```bash
# biobank API server (http://localhost:8001)
RUN_MIGRATIONS=true POSTGRES_PORT=5433 dotnet run --project src/BiobankApi/BiobankApi.Web

# trigger biobank ingestion on the running API (also runs weekly via Quartz)
curl -X POST http://localhost:8001/admin/ingest

# sequencing API server (scaffold; http://localhost:8002)
RUN_MIGRATIONS=true POSTGRES_PORT=5434 dotnet run --project src/SequencingApi/SequencingApi.Web
curl -X POST http://localhost:8002/admin/ingest    # {"ingested":0,"failed":0,"errors":[]}

# uploader sync job (prints a JSON summary; exit 0 = no failures, 1 = failures)
dotnet run --project src/Uploader/Uploader.Host
```

## Tests

- `*.UnitTests` - pure tests (domain services, planner, builders, handlers with fakes).
- `*.IntegrationTests` - EF Core against in-memory SQLite via the `SqliteDatabase` helper; the API
  hosts are exercised end-to-end with `WebApplicationFactory<Program>`.

```bash
dotnet test DataCatalogueUpload.slnx                                  # everything
dotnet test tests/Uploader.UnitTests/Uploader.UnitTests.csproj  # one project
```

## Containers

```bash
docker compose -f compose.prod.yml up -d --build                # dbs + biobank-api + sequencing-api
curl -X POST http://localhost:8001/admin/ingest                 # ingest on demand
```

The Dockerfiles build with the repo root as their context (central package management +
project references). The uploader is run as a job (host `dotnet run` or its own container image).

## CI

[`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml) restores, runs
`dotnet format --verify-no-changes`, builds in Release (warnings as errors), and runs the tests.
Run those three locally before pushing.
