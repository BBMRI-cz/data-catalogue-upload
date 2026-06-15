# data-catalogue-upload

A **uv-workspace monorepo** for the data-catalogue sync system. It contains the sync job and the source API
services it reads from.

| Member | Path | What it is |
|--------|------|------------|
| uploader | [`apps/uploader`](apps/uploader) | Scheduled, one-shot sync job: aggregates per-patient data from the source APIs and upserts it into the data catalogue. |
| biobank_api | [`apps/biobank_api`](apps/biobank_api) | Source API service: parses biobank XML exports and serves the patient/sample/clinical endpoints the uploader consumes. |

More `*_api` services (radiology, sequencing, WSI) will be added as additional members. Each member declares
its own dependencies and has its own `.env`, Alembic migrations, and PostgreSQL database.

## Quickstart

```bash
uv sync --all-packages --group dev                 # install the whole workspace

# each member has its own .env
cp apps/uploader/.env.example    apps/uploader/.env
cp apps/biobank_api/.env.example apps/biobank_api/.env

# each app has its own database service
docker compose -f compose.prod.yml up -d uploader-db biobank-db

# apply each member's migrations
cd apps/uploader    && uv run alembic -c alembic.ini upgrade head && cd -
cd apps/biobank_api && uv run alembic -c alembic.ini upgrade head && cd -

# run a member
uv run --package biobank_api biobank-api-serve     # http://localhost:8001
uv run --package uploader    uploader              # the sync job
```

See [`DEVELOPMENT.md`](DEVELOPMENT.md) for full setup, [`ARCHITECTURE.md`](ARCHITECTURE.md) for the design,
and each member's README for service-specific details.
