# Development

Developer guide for setting up, running, and contributing to `data-catalogue-upload`. For the high-level
design see [`ARCHITECTURE.md`](ARCHITECTURE.md); for AI-agent conventions see [`AGENTS.md`](AGENTS.md).

This repository is a **uv workspace** (monorepo). Members live under `apps/`:

- [`apps/uploader`](apps/uploader) - the sync job ([README](apps/uploader/README.md))
- [`apps/biobank_api`](apps/biobank_api) - the biobank source API ([README](apps/biobank_api/README.md))

There is one `uv.lock` and one `.venv` for the whole workspace; each member declares only its own dependencies.

## Prerequisites

- **Python** - local development uses `3.14` (pinned in `.python-version`); the project supports `>=3.11` and CI runs `3.11`.
- **[uv](https://github.com/astral-sh/uv)** - dependency manager and task runner.
- **Docker** (with Compose) - to run PostgreSQL and the API services.

## Setup

1. Install the whole workspace (all members + shared dev tools):

```bash
uv sync --all-packages --group dev
```

2. Create your env file from the template and adjust as needed:

```bash
cp .env.example .env
```

## Environment variables

`.env.example` holds the PostgreSQL settings, the uploader's API URLs, and the `BIOBANK_*` settings for the
biobank API. Keep `.env` in the **project root** - each service's Alembic `env.py` loads it from there. Never
commit a real `.env`; only `.env.example` is tracked.

## Database

Start PostgreSQL (set `POSTGRES_PORT` in `.env` first if the default port is in use):

```bash
docker compose -f compose.prod.yml up -d db
```

The container creates the uploader's database (`POSTGRES_DB`) and, on first init, the biobank API's database
(`biobank_api`, via [`docker/postgres/init`](docker/postgres/init)).

Apply migrations per service (each member has its own Alembic tree and database):

```bash
cd apps/uploader   && uv run alembic -c alembic.ini upgrade head && cd -
cd apps/biobank_api && uv run alembic -c alembic.ini upgrade head && cd -
```

> Note: both `uploader` (`main.py`) and `biobank_api` (`ingest.py`) also call `Base.metadata.create_all(...)`,
> so tables can be created without migrations during development. For anything reproducible, use Alembic.

## Running the services

With the database up and a complete `.env`:

```bash
# uploader sync job (prints a JSON run summary; exits 0, or 1 if any entity failed)
uv run --package uploader uploader

# biobank API HTTP server (then curl localhost:8001/health and /patients)
uv run --package biobank_api biobank-api-serve

# biobank API ingestion (one-shot: parse XML exports -> DB)
uv run --package biobank_api biobank-api-ingest
```

Or run the API services via Docker:

```bash
docker compose -f compose.prod.yml up -d db biobank_api
docker compose -f compose.prod.yml --profile ingest run --rm biobank_api_ingest
```

## Quality checks

Run these for each package you touched (`<pkg>` = `uploader` or `biobank_api`); they mirror CI (see
[`.github/workflows/ci.yml`](.github/workflows/ci.yml), a per-package matrix):

```bash
uv run ruff check apps/<pkg>            # lint
uv run ruff format --check apps/<pkg>   # formatting (drop --check to auto-format)
uv run mypy apps/<pkg>                  # type check
uv run pytest apps/<pkg>/tests          # tests
uv lock --check                         # workspace lockfile is consistent
```

## Adding a migration

After changing a member's ORM models (e.g. `apps/biobank_api/src/biobank_api/infrastructure/db/models.py`):

```bash
cd apps/biobank_api
uv run alembic -c alembic.ini revision --autogenerate -m "describe change"
uv run alembic -c alembic.ini upgrade head
```

Review the generated file under `apps/<pkg>/migrations/versions/` before committing - autogenerate is a
starting point, not a guarantee.

## Adding dependencies

Use uv so `pyproject.toml` and `uv.lock` stay consistent. Add runtime deps to the **member that needs them**;
add shared dev tooling to the **root** dev group:

```bash
cd apps/biobank_api && uv add fastapi    # runtime dependency of one member
uv add --group dev pytest                # shared dev dependency (run at the repo root)
```

Do not hand-edit `uv.lock`.

## Troubleshooting

| Symptom | Likely cause & fix |
|---------|--------------------|
| `docker compose up` fails with "port is already allocated" | The host `5432` is taken (often a local Postgres). Set a free `POSTGRES_PORT` (e.g. `5433`) in `.env` and re-run. |
| App/alembic can't connect to the database | Postgres isn't up or `.env` doesn't match the container. Check `docker compose -f compose.prod.yml ps`, and confirm the `POSTGRES_*` values match what the container started with. |
| biobank API: `database "biobank_api" does not exist` | The init script only runs on a fresh data volume. Either recreate the volume, or `createdb biobank_api` manually. |
| `RuntimeError: Missing required environment variable` (uploader) | A required API URL is unset. Ensure all five (`BIOBANK_API_URL`, `RADIOLOGY_API_URL`, `SEQUENCING_API_URL`, `WSI_API_URL`, `CATALOGUE_API_URL`) are in `.env`. |
| Alembic: "Target database is not up to date" | Pending migrations. Run `cd apps/<pkg> && uv run alembic -c alembic.ini upgrade head`. |
| Alembic: "Can't locate revision identified by ..." | The DB's `alembic_version` points at a revision not in `apps/<pkg>/migrations/versions/` (e.g. after switching branches). Align the branch with the DB, or recreate the dev DB. |
| `.env` values seem ignored by migrations | `.env` must live in the **project root**; each `migrations/env.py` loads it from there. |
| `uv sync` removed a member's deps | At the workspace root, `uv sync` alone only syncs the root. Use `uv sync --all-packages --group dev`. |
