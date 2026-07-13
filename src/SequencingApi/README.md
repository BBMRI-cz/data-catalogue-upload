# sequencing_api

Source API service for sequencing data, part of the data-catalogue sync system (#30). It follows the
same **Clean Architecture + DDD** layering as [biobank_api](../BiobankApi): a domain with aggregates
and invariant factories, a CQRS application layer dispatched through the
[`Mediator`](https://github.com/martinothamar/Mediator) source generator, EF Core for persistence, and
an ASP.NET Core Minimal API host. Ingestion runs in-process in the Web host (weekly Quartz job +
`POST /admin/ingest`).

> **Status: scaffold.** No sequencing domain aggregate, repository or EF migration exists yet — those
> land with feature work under #30. The stub ingestion source reports zero records so the pipeline runs
> end-to-end. Search the tree for `ponytail:` to find the exact spots to fill in.

## Projects

| Layer | Project | Notes |
|-------|---------|-------|
| Domain | `SequencingApi.Domain` | `Common/` base types only (Entity, AggregateRoot, ValueObject, `IStronglyTypedId`). |
| Application | `SequencingApi.Application` | `IngestRecordsCommand` + handler, `ISequencingDataSource` port, Mediator/FluentValidation wiring. |
| Infrastructure | `SequencingApi.Infrastructure` | `SequencingDbContext` (no DbSets yet), `SequencingOptions`, design-time factory, `StubSequencingDataSource`. |
| Web | `SequencingApi.Web` | Minimal-API host: `GET /health`, `POST /admin/ingest`, Quartz `IngestionJob`. |

## Configuration (environment variables)

Defaults keep the service runnable with no environment set.

| Variable | Default | Purpose |
|----------|---------|---------|
| `POSTGRES_HOST` / `POSTGRES_PORT` | `localhost` / `5434` | Database host/port. |
| `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` | `sequencing_api` / `postgres` / `postgres` | Database name and credentials. |
| `SEQUENCING_PORT` | `8002` | HTTP port (bind via `ASPNETCORE_URLS` in the container). |
| `SEQUENCING_DATA_PATH` | `data/records` | Data source path (unused until the real source lands). |
| `SEQUENCING_INGEST_CRON` | `0 0 17 ? * SUN` | Quartz ingestion schedule (Sundays 17:00 UTC). |
| `DisableScheduler` | `false` | `true` disables the Quartz job (used by integration tests). |
| `RUN_MIGRATIONS` | unset | `true` applies EF migrations on startup (set by the container). |

## Running locally

```bash
# start the database
docker compose -f compose.prod.yml up -d sequencing-db

# run the API (applies EF migrations on startup when RUN_MIGRATIONS=true)
RUN_MIGRATIONS=true POSTGRES_PORT=5434 \
  dotnet run --project src/SequencingApi/SequencingApi.Web    # http://localhost:8002

curl http://localhost:8002/health                 # {"status":"ok"}
curl -X POST http://localhost:8002/admin/ingest    # {"ingested":0,"failed":0,"errors":[]}
```
