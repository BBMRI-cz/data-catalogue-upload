# AGENTS.md

Guidance for AI coding agents (primarily Claude Code) working in this repository. `CLAUDE.md` is a symlink to this file for Claude Code discovery, so editing `AGENTS.md` updates both. Project-specific skills live in `.claude/skills/`.

## Project

`data-catalogue-upload` is a scheduled, one-shot Python sync job. For each patient it reads from four source APIs (biobank, radiology, sequencing, WSI), aggregates the data into one FAIR Genomes-shaped patient record, compares it against fingerprints stored in PostgreSQL, and upserts/deletes records in a central data catalogue API.

Read [`ARCHITECTURE.md`](ARCHITECTURE.md) for the data flow and layering, and [`DEVELOPMENT.md`](DEVELOPMENT.md) for setup and local run instructions.

## Tech stack

- Python `>=3.11` (local dev pins `3.14` via `.python-version`; CI and mypy target `3.11`)
- [uv](https://github.com/astral-sh/uv) for dependency management and running tools
- SQLAlchemy + PostgreSQL (via `psycopg2-binary`), Alembic for migrations
- `requests` for HTTP, `python-dotenv` for config
- ruff (lint + format), mypy (types), pytest (tests)

## Commands

Run all commands from the repository root unless noted.

```bash
uv sync --group dev              # install deps (incl. dev tools)
uv run ruff check .              # lint
uv run ruff format --check .     # formatting check (drop --check to apply)
uv run mypy .                    # type check
uv run pytest                    # tests
cd src && uv run alembic -c alembic.ini upgrade head   # apply migrations
uv run python src/main.py        # run the sync (needs env vars + DB)
```

## Project layout

The codebase follows a hexagonal / layered architecture under `src/`:

- `domain/` - pure dataclass models and fingerprinting (`compute_fingerprint`). No I/O, no framework imports.
- `application/` - use cases and orchestration: `sync_service.py`, `sync_planner.py`, `builders/` (raw dict -> domain objects), and `interfaces/ports.py` (Protocol definitions for gateways, repository, planner).
- `infrastructure/` - adapters that implement the ports: `api/clients.py` (HTTP gateways) and `db/` (SQLAlchemy ORM + repositories).
- `migrations/` - Alembic environment and versioned migrations.

`main.py` is the entry point that wires everything from environment variables.

## Conventions

- **Respect layer boundaries.** Domain must not import application or infrastructure. Application depends on `domain` and the Protocols in `interfaces/ports.py`, never on concrete infrastructure classes. Infrastructure implements the ports.
- **Ports are `typing.Protocol`s.** When adding a new external dependency, define a Protocol in `application/interfaces/ports.py` and implement it in `infrastructure/`.
- **Domain models are dataclasses.** Builders map raw `dict` payloads into domain objects using `.get(...)` for optional fields (see `application/builders/clinical_builder.py`).
- **Use `from __future__ import annotations`** at the top of modules (matches existing files).
- **Add dependencies with uv**, e.g. `uv add requests` or `uv add --group dev pytest`. Do not hand-edit `pyproject.toml` versions or `uv.lock`.

## Before finishing any change

Always run, and ensure they pass:

```bash
uv run ruff check .
uv run ruff format --check .
uv run mypy .
uv run pytest
```

These are exactly the checks CI runs (see [`.github/workflows/ci.yml`](.github/workflows/ci.yml)). Fixing them locally avoids CI failures.

## What to avoid

- Do not commit secrets or a real `.env`. Only `.env.example` is tracked.
- Do not bypass the layers (e.g. calling `requests` from `application/` or `domain/`).
- Do not edit `uv.lock` by hand.
- Do not loosen mypy/ruff config to silence errors; fix the code instead.
