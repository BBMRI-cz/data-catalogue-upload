# data-catalogue-upload

A **uv-workspace monorepo** for the data-catalogue sync system. It contains the sync job and the source API
services it reads from.

| Member | Path | What it is |
|--------|------|------------|
| uploader | [`apps/uploader`](apps/uploader) | Scheduled, one-shot sync job: aggregates per-patient data from the source APIs and upserts it into the data catalogue. |
| biobank_api | [`apps/biobank_api`](apps/biobank_api) | Source API service: parses biobank XML exports and serves the patient/sample/clinical endpoints the uploader consumes. |

More `*_api` services (radiology, sequencing, WSI) will be added as additional members.

## Quickstart

```bash
uv sync --all-packages --group dev          # install the whole workspace
cp .env.example .env                         # then adjust as needed
docker compose -f compose.prod.yml up -d db  # start PostgreSQL

# apply each member's migrations
cd apps/uploader    && uv run alembic -c alembic.ini upgrade head && cd -
cd apps/biobank_api && uv run alembic -c alembic.ini upgrade head && cd -

# run a member
uv run --package biobank_api biobank-api-serve   # http://localhost:8001
uv run --package uploader    uploader            # the sync job
```

See [`DEVELOPMENT.md`](DEVELOPMENT.md) for full setup, [`ARCHITECTURE.md`](ARCHITECTURE.md) for the design,
and each member's README for service-specific details.
