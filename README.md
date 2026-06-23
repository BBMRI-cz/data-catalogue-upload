# data-catalogue-upload

A **.NET solution** for the data-catalogue sync system: the sync job and the source API
services it reads from. The solution is [`DataCatalogueUpload.slnx`](DataCatalogueUpload.slnx) at the repo root.

| Service | Projects | What it is |
|---------|----------|------------|
| uploader | [`src/Uploader`](src/Uploader) | Scheduled, one-shot sync job: aggregates per-patient data from the source APIs and upserts it into the data catalogue. |
| biobank_api | [`src/BiobankApi`](src/BiobankApi) | Source API service: parses biobank XML exports and serves the patient/sample/clinical endpoints the uploader consumes. |

Each service is its own set of projects (Domain / Application / Infrastructure / host) following
**Clean Architecture + DDD**: a rich domain with aggregates and domain services that enforce their
own invariants (constructors/factories return `ErrorOr` or throw `DomainException`), a CQRS
application layer dispatched through the free [`Mediator`](https://github.com/martinothamar/Mediator)
source generator with handlers returning `ErrorOr`, EF Core for persistence, and ASP.NET Core Minimal
API for the HTTP surface. Each service owns its own PostgreSQL database and EF Core migrations.

## Quickstart

```bash
dotnet restore DataCatalogueUpload.slnx
dotnet build DataCatalogueUpload.slnx
dotnet test DataCatalogueUpload.slnx

# start both databases
docker compose -f compose.prod.yml up -d uploader-db biobank-db

# run the biobank API (applies its EF migrations on startup when RUN_MIGRATIONS=true)
RUN_MIGRATIONS=true POSTGRES_PORT=5433 \
  dotnet run --project src/BiobankApi/BiobankApi.Web          # http://localhost:8001

# one-shot XML ingestion
RUN_MIGRATIONS=true POSTGRES_PORT=5433 \
  dotnet run --project src/BiobankApi/BiobankApi.Web -- ingest

# run the sync job (applies its EF migrations on startup, then syncs and prints a JSON summary)
dotnet run --project src/Uploader/Uploader.Host
```

See [`DEVELOPMENT.md`](DEVELOPMENT.md) for full setup, [`ARCHITECTURE.md`](ARCHITECTURE.md) for the
design, and [`docs/patient-data-report.md`](docs/patient-data-report.md) for the biobank XML format.
