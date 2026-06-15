# biobank_api

Standalone service that parses the biobank XML exports and exposes the patient / sample / clinical
endpoints the [`uploader`](../uploader) sync job consumes (`GET /patients`, the shape
`HttpSourceDataGateway.fetch_patients` / `ClinicalBuilder` expect).

This package is a member of the repository's uv workspace. See the repository root
[`DEVELOPMENT.md`](../../DEVELOPMENT.md) for workspace-wide setup.

> Status: **scaffold** (#32). Endpoints and the XML mapping are stubbed; `GET /patients` returns an
> empty list until ingestion is implemented (#33).

## Layout

```
src/biobank_api/
├── domain/models/        # pure dataclasses (Patient, Sample)
├── application/          # use cases (get_patients, ingest_exports) + interfaces/ports.py (Protocols)
├── infrastructure/
│   ├── xml/              # lxml parser implementing XmlExportSource
│   ├── db/               # SQLAlchemy ORM, lazy session, repositories
│   └── web/              # FastAPI app, routers, Pydantic schemas, DI
├── config.py             # pydantic-settings (env prefix BIOBANK_)
├── server.py             # HTTP server entrypoint  (biobank-api-serve)
└── ingest.py             # ingestion/scheduler entrypoint  (biobank-api-ingest)
migrations/               # Alembic environment + versioned migrations (own database)
```

## Configuration

Environment variables (prefix `BIOBANK_`, read by `config.Settings`; defaults keep it runnable locally):

| Variable | Default | Purpose |
|----------|---------|---------|
| `BIOBANK_DATABASE_URL` | `postgresql+psycopg2://postgres:postgres@localhost:5432/biobank_api` | Service database |
| `BIOBANK_HOST` | `0.0.0.0` | Server bind host |
| `BIOBANK_PORT` | `8001` | Server bind port |
| `BIOBANK_XML_EXPORT_PATH` | `data/exports` | Directory of biobank XML exports to ingest |

## Run

From the repository root (after `uv sync --all-packages --group dev`):

```bash
# apply migrations (own database)
cd apps/biobank_api && uv run alembic -c alembic.ini upgrade head && cd -

# ingestion (one-shot): parse XML exports -> database
uv run --package biobank_api biobank-api-ingest

# HTTP server
uv run --package biobank_api biobank-api-serve
# then: curl localhost:8001/health   ->  {"status":"ok"}
#       curl localhost:8001/patients ->  []
```

## Test

```bash
uv run --package biobank_api pytest apps/biobank_api/tests
```
