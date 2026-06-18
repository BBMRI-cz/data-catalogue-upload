# Development

Developer guide for setting up, running, and contributing to `data-catalogue-upload`. For the high-level
design see [`ARCHITECTURE.md`](ARCHITECTURE.md); for AI-agent conventions see [`AGENTS.md`](AGENTS.md).

This repository is a **uv workspace** (monorepo). Members live under `apps/`:

- [`apps/uploader`](apps/uploader) - the sync job ([README](apps/uploader/README.md))
- [`apps/biobank_api`](apps/biobank_api) - the biobank source API ([README](apps/biobank_api/README.md))

There is one `uv.lock` and one `.venv` for the whole workspace; each member declares only its own dependencies
and has **its own `.env`, Alembic migrations, and PostgreSQL database**.

## Prerequisites

- **Python** - local development uses `3.14` (pinned in `.python-version`); the project supports `>=3.11` and CI runs `3.11`.
- **[uv](https://github.com/astral-sh/uv)** - dependency manager and task runner.
- **Docker** (with Compose) - to run the PostgreSQL databases and the API services.

## Setup

1. Install the whole workspace (all members + shared dev tools):

```bash
uv sync --all-packages --group dev
```

2. Create each member's env file from its template and adjust as needed:

```bash
cp apps/uploader/.env.example    apps/uploader/.env
cp apps/biobank_api/.env.example apps/biobank_api/.env
```

## Environment variables

Each member owns its config: `apps/<member>/.env` (gitignored; only `.env.example` is tracked).

- `apps/uploader/.env` - `POSTGRES_*` for the uploader's database, plus the five source/catalogue API URLs.
  Loaded by `main.py` and the uploader's `migrations/env.py`.
- `apps/biobank_api/.env` - `POSTGRES_*` for the biobank API's database and `BIOBANK_*` (server bind + XML
  export path). Read by `config.Settings` (which also drives `migrations/env.py`).

Never commit a real `.env`.

## Databases

Each app has its own PostgreSQL service in [`compose.prod.yml`](compose.prod.yml):

```bash
docker compose -f compose.prod.yml up -d uploader-db biobank-db
```

| Service | Host port | Database (`POSTGRES_DB`) | Used by |
|---------|-----------|--------------------------|---------|
| `uploader-db` | `5432` | `data_catalogue_upload` | the uploader (host-run, via `localhost:5432`) |
| `biobank-db` | `5433` | `biobank_api` | the biobank API |

Apply each member's migrations (own Alembic tree + database):

```bash
cd apps/uploader    && uv run alembic -c alembic.ini upgrade head && cd -
cd apps/biobank_api && uv run alembic -c alembic.ini upgrade head && cd -
```

> Note: both `uploader` (`main.py`) and `biobank_api` (`ingest.py`) also call `Base.metadata.create_all(...)`,
> so tables can be created without migrations during development. For anything reproducible, use Alembic.

## Running the services

With the databases up and each `.env` filled in:

```bash
# uploader sync job (prints a JSON run summary; exits 0, or 1 if any entity failed)
uv run --package uploader uploader

# biobank API HTTP server (then curl localhost:8001/health and /patients)
uv run --package biobank_api biobank-api-serve

# biobank API ingestion (one-shot: parse XML exports -> DB)
uv run --package biobank_api biobank-api-ingest
```

Or run the biobank API via Docker (it reaches `biobank-db` over the compose network):

```bash
docker compose -f compose.prod.yml up -d biobank-db biobank_api
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

### Tests: unit vs integration

A member's tests live in `apps/<pkg>/tests/`, split by what they touch:

- `tests/unit/` — pure tests with no I/O (domain models, builders, use cases; handlers tested
  against fake ports). Fast; these are the bulk of the suite.
- `tests/integration/` — exercise a real adapter (e.g. a repository against a real database
  engine), no live external service needed. A `conftest.py` in the folder is a shared file pytest
  auto-loads to provide fixtures (like a DB session) to every test under it.

```bash
uv run pytest apps/<pkg>/tests/unit         # fast inner loop
uv run pytest apps/<pkg>/tests/integration  # adapter round-trips
uv run pytest apps/<pkg>/tests              # everything (what CI runs; pytest recurses)
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
| `docker compose up` fails with "port is already allocated" | A host port (`5432`/`5433`) is taken. Change the mapping in `compose.prod.yml` (and the member's `.env` `POSTGRES_PORT` for host runs). |
| App/alembic can't connect to the database | The relevant db service isn't up, or the member's `.env` doesn't match it. Check `docker compose -f compose.prod.yml ps` and the `POSTGRES_*` values in `apps/<member>/.env`. |
| `RuntimeError: Missing required environment variable` (uploader) | A required API URL is unset. Ensure all five (`BIOBANK_API_URL`, `RADIOLOGY_API_URL`, `SEQUENCING_API_URL`, `WSI_API_URL`, `CATALOGUE_API_URL`) are in `apps/uploader/.env`. |
| Alembic: "Target database is not up to date" | Pending migrations. Run `cd apps/<pkg> && uv run alembic -c alembic.ini upgrade head`. |
| Alembic: "Can't locate revision identified by ..." | The DB's `alembic_version` points at a revision not in `apps/<pkg>/migrations/versions/` (e.g. after switching branches). Align the branch with the DB, or recreate the dev DB. |
| `.env` values seem ignored | Each member's `.env` lives in `apps/<member>/`, not the repo root. The uploader loads it in `main.py`; the biobank API reads it via `config.Settings`. |
| `uv sync` removed a member's deps | At the workspace root, `uv sync` alone only syncs the root. Use `uv sync --all-packages --group dev`. |
