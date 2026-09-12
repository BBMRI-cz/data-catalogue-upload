# data-catalogue-upload

A **.NET solution** for the data-catalogue sync system: the sync job and the source API
services it reads from. The solution is [`DataCatalogueUpload.slnx`](DataCatalogueUpload.slnx) at the repo root.

| Service | Projects | What it is |
|---------|----------|------------|
| uploader | [`src/Uploader`](src/Uploader) | Scheduled, one-shot sync job: aggregates per-patient data from the source APIs and saves it into a MOLGENIS EMX2 catalogue. |
| biobank_api | [`src/BiobankApi`](src/BiobankApi) | Source API service: parses biobank XML exports and serves the patient/sample/clinical endpoints the uploader consumes. |
| sequencing_api | [`src/SequencingApi`](src/SequencingApi) | Source API service for sequencing data (domain model landed; persistence and endpoints land with #55/#58). Same Clean Architecture layering and in-process Quartz ingestion as biobank_api. |

Each service is its own set of projects (Domain / Application / Infrastructure / host) following
**Clean Architecture + DDD**: a rich domain with aggregates and domain services that enforce their
own invariants (factories return `ErrorOr` validation errors), a CQRS
application layer dispatched through the free [`Mediator`](https://github.com/martinothamar/Mediator)
source generator with handlers returning `ErrorOr` and FluentValidation request validators, EF Core
for persistence, and ASP.NET Core Minimal API for the HTTP surface. Each service owns its own PostgreSQL database and EF Core migrations.

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

# trigger ingestion on the running API (also runs weekly via the Quartz schedule)
curl -X POST http://localhost:8001/admin/ingest

# point the sync job at a catalogue: copy .env.example to .env and fill in CATALOGUE_TOKEN
cp .env.example .env

# run the sync job (applies its EF migrations on startup, then syncs and prints a JSON summary)
dotnet run --project src/Uploader/Uploader.Host
```

See [`DEVELOPMENT.md`](DEVELOPMENT.md) for full setup, [`ARCHITECTURE.md`](ARCHITECTURE.md) for the
design, [`docs/catalogue-api-contract.md`](docs/catalogue-api-contract.md) for what the catalogue
accepts, and [`docs/patient-data-report.md`](docs/patient-data-report.md) for the biobank XML format.

The commands above run everything on one machine. In production the two source services sit on
different servers, because each reads a directory only that machine has:
[`docs/deployment.md`](docs/deployment.md) is the runbook for that, using `compose.biobank.yml` and
`compose.sequencing.yml`.

## License

Licensed under the [Apache License 2.0](LICENSE).
