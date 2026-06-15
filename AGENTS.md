# AGENTS.md

Guidance for AI coding agents (primarily Claude Code) working in this repository. `CLAUDE.md` is a symlink to this file for Claude Code discovery, so editing `AGENTS.md` updates both. Project-specific skills live in `.claude/skills/`.

## Project

`data-catalogue-upload` is a **uv-workspace monorepo** for the data-catalogue sync system. Its members live under `apps/`:

- `apps/uploader` - a scheduled, one-shot sync job. For each patient it reads from four source APIs (biobank, radiology, sequencing, WSI), aggregates the data into one FAIR Genomes-shaped patient record, compares it against fingerprints stored in PostgreSQL, and upserts/deletes records in a central data catalogue API.
- `apps/biobank_api` - a source API service that parses biobank XML exports and serves the patient/sample/clinical endpoints the uploader consumes. (More `*_api` services will follow.)

Read [`ARCHITECTURE.md`](ARCHITECTURE.md) for the data flow and layering, and [`DEVELOPMENT.md`](DEVELOPMENT.md) for setup and local run instructions.

## Tech stack

- Python `>=3.11` (local dev pins `3.14` via `.python-version`; CI and mypy target `3.11`)
- [uv](https://github.com/astral-sh/uv) **workspace**: one root `uv.lock` + shared `.venv`; each member declares its own deps
- SQLAlchemy + PostgreSQL (via `psycopg2-binary`), Alembic for migrations (per service)
- `requests` (uploader HTTP client); **FastAPI + Uvicorn** (API services); `lxml` (biobank XML parsing)
- `python-dotenv` / `pydantic-settings` for config
- ruff (lint + format), mypy (types), pytest (tests)

## Workspace layout

```
apps/
├── uploader/                 # sync job (import package `uploader`)
│   ├── pyproject.toml        # its own deps
│   ├── alembic.ini  migrations/
│   └── src/uploader/{domain,application,infrastructure,main.py}
└── biobank_api/              # biobank source API (import package `biobank_api`)
    ├── pyproject.toml
    ├── alembic.ini  migrations/
    └── src/biobank_api/{domain,application,infrastructure,server.py,ingest.py}
pyproject.toml                # virtual workspace root: [tool.uv.workspace], shared dev group, ruff/mypy config
uv.lock                       # single lockfile for the whole workspace
```

Each member is a src-layout package with a distinct import name (`uploader`, `biobank_api`). A single shared venv cannot host two top-level `domain` packages, so imports are always `from <package>.domain...`.

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

cd apps/<pkg> && uv run alembic -c alembic.ini upgrade head   # migrations (own DB)
```

Entrypoints (console scripts, run via `uv run --package <pkg> <script>`):
- uploader: `uploader`
- biobank_api: `biobank-api-serve` (HTTP server), `biobank-api-ingest` (one-shot ingestion)

## Project layout (within a member)

Each member follows a hexagonal / layered architecture under `apps/<pkg>/src/<pkg>/` with the dependency direction `infrastructure -> application -> domain`:

- `domain/` - pure dataclass models (+ `compute_fingerprint` in the uploader). No I/O, no framework imports.
- `application/` - use cases/orchestration and `interfaces/ports.py` (Protocol definitions).
- `infrastructure/` - adapters implementing the ports: HTTP/XML/web and `db/` (SQLAlchemy ORM + repositories).
- `migrations/` - Alembic environment and versioned migrations (per service).

The entrypoint (`main.py` / `server.py` / `ingest.py`) is the composition root that wires everything from environment variables.

## Conventions

- **Respect layer boundaries.** Domain must not import application or infrastructure. Application depends on `domain` and the Protocols in `interfaces/ports.py`, never on concrete infrastructure classes. Infrastructure implements the ports.
- **Ports are `typing.Protocol`s.** When adding a new external dependency, define a Protocol in the member's `application/interfaces/ports.py` and implement it in `infrastructure/`.
- **Domain models are dataclasses.** Builders map raw `dict` payloads into domain objects using `.get(...)` for optional fields.
- **Imports are absolute under the package name** (`from uploader.domain...`, `from biobank_api.application...`).
- **Use `from __future__ import annotations`** at the top of modules.
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

- Do not commit secrets or a real `.env`. Only `.env.example` is tracked.
- Do not bypass the layers (e.g. calling `requests`/`lxml`/`sqlalchemy` from `application/` or `domain/`).
- Do not add a dependency to the wrong member - keep each member's deps minimal; shared dev tooling goes in the root dev group.
- Do not edit `uv.lock` by hand.
- Do not loosen mypy/ruff config to silence errors; fix the code instead.
