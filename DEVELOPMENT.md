# Development

Developer guide for setting up, running, and contributing to `data-catalogue-upload`. For the high-level design see [`ARCHITECTURE.md`](ARCHITECTURE.md); for AI-agent conventions see [`AGENTS.md`](AGENTS.md).

## Prerequisites

- **Python** - local development uses `3.14` (pinned in `.python-version`); the project supports `>=3.11` and CI runs `3.11`.
- **[uv](https://github.com/astral-sh/uv)** - dependency manager and task runner.
- **Docker** (with Compose) - to run the local PostgreSQL database.

## Setup

1. Install dependencies (including dev tools):

```bash
uv sync --group dev
```

2. Create your env file from the template and adjust as needed:

```bash
cp .env.example .env
```

## Environment variables

`.env.example` currently only contains the PostgreSQL settings. The sync job (`src/main.py`) also requires five API URLs. A complete `.env` looks like this:

```bash
# PostgreSQL (used by docker compose and the app)
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_DB=data_catalogue_upload
POSTGRES_PORT=5432

# Source + catalogue APIs (required by src/main.py)
BIOBANK_API_URL=http://localhost:8001
RADIOLOGY_API_URL=http://localhost:8002
SEQUENCING_API_URL=http://localhost:8003
WSI_API_URL=http://localhost:8004
CATALOGUE_API_URL=http://localhost:8000
```

Keep `.env` in the **project root** - `src/migrations/env.py` loads it before connecting to the database. Never commit a real `.env`; only `.env.example` is tracked.

## Database

Start PostgreSQL (set `POSTGRES_PORT` in `.env` first if the default port is in use):

```bash
docker compose -f compose.prod.yml up -d
```

Apply migrations:

```bash
cd src && uv run alembic -c alembic.ini upgrade head
```

The schema consists of `sync_run` plus five per-entity sync-state tables (`patient_sync_state`, `sample_sync_state`, `sequencing_sync_state`, `wsi_sync_state`, `imaging_study_sync_state`), and Alembic's `alembic_version`.

> Note: `main.py` also calls `Base.metadata.create_all(...)`, so tables can be created without migrations during development. For anything reproducible, use Alembic.

## Running the sync

With the database up and a complete `.env`:

```bash
uv run python src/main.py
```

The job prints a JSON run summary (scanned / changed / uploaded / deleted / skipped / failed) and exits `0` on success, `1` if any entity failed.

## Quality checks

Run these before pushing - they mirror CI exactly (see [`.github/workflows/ci.yml`](.github/workflows/ci.yml)):

```bash
uv run ruff check .             # lint
uv run ruff format --check .    # formatting (drop --check to auto-format)
uv run mypy .                   # type check
uv run pytest                   # tests
```

## Adding a migration

After changing the ORM models in `src/infrastructure/db/models.py`:

```bash
cd src
uv run alembic -c alembic.ini revision --autogenerate -m "describe change"
uv run alembic -c alembic.ini upgrade head
```

Review the generated file under `src/migrations/versions/` before committing - autogenerate is a starting point, not a guarantee.

## Adding dependencies

Use uv so `pyproject.toml` and `uv.lock` stay consistent:

```bash
uv add requests                 # runtime dependency
uv add --group dev pytest       # dev dependency
```

Do not hand-edit `uv.lock`.
