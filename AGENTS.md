# AGENTS.md

Guidance for AI coding agents (primarily Claude Code) working in this repository. `CLAUDE.md` is a symlink to this file for Claude Code discovery, so editing `AGENTS.md` updates both. Project-specific skills live in `.claude/skills/`.

## Project

`data-catalogue-upload` is a **uv-workspace monorepo** for the data-catalogue sync system. Its members live under `apps/`:

- `apps/uploader` - a scheduled, one-shot sync job. For each patient it reads from four source APIs (biobank, radiology, sequencing, WSI), aggregates the data into one FAIR Genomes-shaped patient record, compares it against fingerprints stored in PostgreSQL, and upserts/deletes records in a central data catalogue API.
- `apps/biobank_api` - a source API service that parses biobank XML exports and serves the patient/sample/clinical endpoints the uploader consumes. (More `*_api` services will follow.)

Read [`ARCHITECTURE.md`](ARCHITECTURE.md) for the data flow and layering, and [`DEVELOPMENT.md`](DEVELOPMENT.md) for setup and local run instructions.

## Workspace

- Python `>=3.11` (local dev pins `3.14` via `.python-version`; CI and mypy target `3.11`).
- [uv](https://github.com/astral-sh/uv) **workspace**: one root `uv.lock` + shared `.venv`. Each member declares only its own deps, has its own Alembic migrations + PostgreSQL database, and its own `.env` (copied from that member's `.env.example`).
- Shared dev tooling lives in the root dev group: ruff (lint + format), mypy (types), pytest (tests).

```
apps/
├── uploader/                 # sync job (import package `uploader`)
│   ├── pyproject.toml  .env.example  alembic.ini  migrations/  Dockerfile
│   └── src/uploader/{domain,application,infrastructure,main.py}
└── biobank_api/              # biobank source API (import package `biobank_api`)
    ├── pyproject.toml  .env.example  alembic.ini  migrations/  Dockerfile
    └── src/biobank_api/{domain,application,infrastructure,config.py,server.py,ingest.py}
pyproject.toml                # virtual workspace root: [tool.uv.workspace], shared dev group, ruff/mypy config
uv.lock                       # single lockfile for the whole workspace
```

Each member is a src-layout package with a distinct import name (`uploader`, `biobank_api`). A single shared venv cannot host two top-level `domain` packages, so imports are always `from <package>.domain...`.

## apps/uploader

**Tech stack**
- `requests` for HTTP, `python-dotenv` for config
- SQLAlchemy + PostgreSQL (via `psycopg2-binary`), Alembic for migrations

**Layout** (`apps/uploader/src/uploader/`, dependency direction `infrastructure -> application -> domain`)
- `domain/` - pure dataclass models and fingerprinting (`compute_fingerprint`). No I/O, no framework imports.
- `application/` - `sync_service.py`, `sync_planner.py`, `builders/` (raw dict -> domain), `interfaces/ports.py` (Protocols).
- `infrastructure/` - `api/clients.py` (HTTP gateways) and `db/` (SQLAlchemy ORM + repositories).
- `main.py` - composition root; loads `apps/uploader/.env` then wires everything from env vars.
- `migrations/` - Alembic for the uploader's own database.

**Config** `apps/uploader/.env`: `POSTGRES_*` (its database) + the five source/catalogue API URLs. **Entrypoint:** `uploader`.

## apps/biobank_api

**Tech stack**
- **FastAPI + Uvicorn** for the HTTP server; Pydantic v2 + `pydantic-settings` for schemas/config
- `lxml` for biobank XML parsing
- SQLAlchemy + PostgreSQL (via `psycopg2-binary`), Alembic for migrations

**Layout** (`apps/biobank_api/src/biobank_api/`, same dependency direction)
- `domain/` - pure dataclass models (`Patient`, `Sample`).
- `application/` - use cases (`get_patients.py`, `ingest_exports.py`) + `interfaces/ports.py` (Protocols).
- `infrastructure/` - `xml/` (lxml parser), `db/` (ORM, lazy session, repositories), `web/` (FastAPI app, routers, Pydantic schemas, DI).
- `config.py` - `pydantic-settings` `Settings`, reads `apps/biobank_api/.env`.
- `server.py` / `ingest.py` - the HTTP server and one-shot ingestion entrypoints (composition roots).
- `migrations/` - Alembic for the biobank API's own database.

**Config** `apps/biobank_api/.env`: `POSTGRES_*` (its database) + `BIOBANK_*` (server bind + XML export path). **Entrypoints:** `biobank-api-serve`, `biobank-api-ingest`.

## Commands

Run from the repository root unless noted. `<pkg>` is `uploader` or `biobank_api`.

```bash
uv sync --all-packages --group dev      # install the whole workspace (all members + dev tools)
uv sync --package <pkg> --no-dev        # install just one member's closure (what Docker does)

uv run ruff check apps/<pkg>            # lint one package
uv run ruff format --check apps/<pkg>   # format check (drop --check to apply)
uv run mypy apps/<pkg>                  # type check
uv run pytest apps/<pkg>/tests          # tests
uv lock --check                         # workspace lockfile is consistent

cp apps/<pkg>/.env.example apps/<pkg>/.env                     # each member has its own .env
cd apps/<pkg> && uv run alembic -c alembic.ini upgrade head    # migrations (own DB)
uv run --package <pkg> <console-script>                        # run an entrypoint (see per-app sections)
```

## Conventions

- **Respect layer boundaries.** Domain must not import application or infrastructure. Application depends on `domain` and the Protocols in `interfaces/ports.py`, never on concrete infrastructure classes. Infrastructure implements the ports.
- **Ports are `typing.Protocol`s.** When adding a new external dependency, define a Protocol in the member's `application/interfaces/ports.py` and implement it in `infrastructure/`.
- **Domain models are dataclasses.** Builders map raw `dict` payloads into domain objects using `.get(...)` for optional fields.
- **Imports are absolute under the package name** (`from uploader.domain...`, `from biobank_api.application...`).
- **Use `from __future__ import annotations`** at the top of modules.
- **Tests split into `tests/unit/` (pure, no I/O) and `tests/integration/`** (real adapters — DB repositories run against in-memory SQLite via the `session` fixture). `uv run pytest apps/<pkg>/tests` recurses into both.
- **Add dependencies with uv to the right member**, e.g. `cd apps/biobank_api && uv add fastapi`; put shared dev tooling in the root dev group (`uv add --group dev <pkg>`). Do not hand-edit `pyproject.toml` versions or `uv.lock`.

## Before finishing any change

For each package you touched (`<pkg>` = `uploader` and/or `biobank_api`), run and ensure they pass:

```bash
uv run ruff check apps/<pkg>
uv run ruff format --check apps/<pkg>
uv run mypy apps/<pkg>
uv run pytest apps/<pkg>/tests
uv lock --check
```

These mirror CI (see [`.github/workflows/ci.yml`](.github/workflows/ci.yml)), which runs a per-package matrix plus a lockfile check.

## What to avoid

- Do not commit secrets or a real `.env`. Only each member's `.env.example` is tracked.
- Do not bypass the layers (e.g. calling `requests`/`lxml`/`sqlalchemy` from `application/` or `domain/`).
- Do not add a dependency to the wrong member - keep each member's deps minimal; shared dev tooling goes in the root dev group.
- Do not edit `uv.lock` by hand.
- Do not loosen mypy/ruff config to silence errors; fix the code instead.
