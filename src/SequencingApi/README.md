# sequencing_api

Source API service for sequencing data, part of the data-catalogue sync system (#30). It follows the
same **Clean Architecture + DDD** layering as [biobank_api](../BiobankApi): a domain with aggregates
and invariant factories, a CQRS application layer dispatched through the
[`Mediator`](https://github.com/martinothamar/Mediator) source generator, EF Core for persistence, and
an ASP.NET Core Minimal API host. Ingestion runs in-process in the Web host (weekly Quartz job +
`POST /admin/ingest`).

> **Status: domain model only.** The sequencing domain landed with #54; the repository and first EF
> migration land with #55, and the real export reader with #56. The stub ingestion source reports zero
> records so the pipeline runs end-to-end. Search the tree for `ponytail:` to find the remaining spots
> to fill in.

## Domain model

Derived from [`docs/sequencing-data-report.md`](../../docs/sequencing-data-report.md) §4 and kept
**biobank-agnostic** — the first source's folder layout, vendor report formats and panel-matching rules
all stay in the ingestion adapter.

Three aggregate roots, referencing each other by identity only:

```
SampleAggregate (SampleId = opaque external_id, + IdScheme)   PanelAggregate (PanelId)
  └── RunSample : Entity<SequencingRunId>                          ▲
        ├── LibraryPreparation?  ────────── PanelId ───────────────┘
        ├── SequencingFile[]  (by FileRole)
        └── Analysis[]                       SequencingRunAggregate (SequencingRunId)
              ├── SequencingFile[]             ├── ReadDefinition[]
              └── QualityMetrics?              └── PercentageQ30
```

**Individual variant records are deliberately not modelled.** Nothing in the catalogue path consumes
them, and they would be by far the largest table in the service. An analysis references its variant
calls as files (`FileRole.Vcf`, `FileRole.VcfFiltered`, `FileRole.VariantReport`) and summarises them
in `QualityMetrics` (`TotalVariants`, `TsTvRatio`, homozygous/heterozygous split). Add a `Variant`
entity the day something actually queries variants.

**`QualityMetrics` hangs off `Analysis` only.** Every metric in it — coverage, read counts, on-target
rate, variant summaries — is computed by the analysis pipeline, not by the instrument. A run measures
exactly one quality number, so it carries a plain `PercentageQ30` property instead of a
mostly-null metrics object, and `RunSample` carries none at all.

A run is shared by roughly a dozen samples and a panel by hundreds, so both are their own root rather
than being embedded. `RunSample` is identified by the run it belongs to — a sample is sequenced at most
once per run, and `SampleAggregate.Create` rejects duplicate run ids so that stays true.

Invariants **and value cleaning** live in the `Create(...)` factories (`ErrorOr<T>`, first failure
wins), sharing the internal `Common/Normalize` helper. Cleaning is limited to already-typed values —
trimming, whitespace collapsing, case folding, chromosome and gene-symbol canonicalisation. Turning
source text into numbers or dates is decoding, not domain logic, and stays in Infrastructure.

## Projects

| Layer | Project | Notes |
|-------|---------|-------|
| Domain | `SequencingApi.Domain` | `Samples/`, `Runs/`, `Panels/` aggregates; `QualityMetrics` + `Enums.cs` at the root; `Common/` base types, typed ids and `Normalize`. |
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
