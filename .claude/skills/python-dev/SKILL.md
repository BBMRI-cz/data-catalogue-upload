---
name: python-dev
description: Coding patterns and architecture rules for the data-catalogue-upload Python codebase. Use when writing or modifying Python in any workspace member under apps/ - adding domain models, application use cases/builders, infrastructure adapters (HTTP/XML/web gateways, DB repositories), or ports. Covers the uv workspace layout, hexagonal layer boundaries, dataclass and Protocol patterns, uv usage, and the lint/type validation loop.
---

# Python development (data-catalogue-upload)

This repository is a **uv workspace** (monorepo). Members live under `apps/` - `apps/uploader` (the sync job)
and `apps/biobank_api` (the biobank source API), with more `*_api` services to come. Each member is a
hexagonal / layered package; keep changes inside the right layer and the right member, and validate with
ruff + mypy before finishing.

## Workspace rules

- Each member is a **src-layout package** with a distinct import name: `apps/<pkg>/src/<pkg>/`. Imports are
  always absolute under that name (`from uploader.domain...`, `from biobank_api.application...`).
- Add a runtime dependency to the **member that needs it** (`cd apps/<pkg> && uv add <pkg>`); add shared dev
  tooling to the **root** dev group (`uv add --group dev <pkg>`). Keep each member's deps minimal.
- One `uv.lock` and one `.venv` for the whole workspace. Install with `uv sync --all-packages --group dev`.

## Layers and dependency direction

Within a member, dependencies only point inward: `infrastructure` -> `application` -> `domain`.

| Layer | Path (per member) | Put here | Never import |
|-------|-------------------|----------|--------------|
| Domain | `apps/<pkg>/src/<pkg>/domain/` | Pure dataclass models, `compute_fingerprint`, parsing helpers | `application`, `infrastructure`, `requests`, `lxml`, `sqlalchemy`, `fastapi` |
| Application | `apps/<pkg>/src/<pkg>/application/` | Use cases, builders, ports (`interfaces/ports.py`) | concrete infrastructure classes |
| Infrastructure | `apps/<pkg>/src/<pkg>/infrastructure/` | HTTP/XML/web gateways, DB ORM + repositories (`db/`) | - |

The entrypoint (`main.py` / `server.py` / `ingest.py`) is the composition root; it is the only place that
wires concrete infrastructure to application use cases.

## Patterns

**Start modules with future annotations:**

```python
from __future__ import annotations
```

**Domain models are dataclasses.** Define new entities as `@dataclass` in `apps/<pkg>/src/<pkg>/domain/models/`
and export them from `domain/models/__init__.py`.

**Builders map raw `dict` -> domain objects** using `.get(...)` for optional fields. Follow
`apps/uploader/src/uploader/application/builders/clinical_builder.py`:

```python
class ClinicalBuilder:
    def build_personal(self, payload: dict) -> Personal:
        return Personal(
            personal_identifier=payload.get("personal_identifier"),
            year_of_birth=payload.get("year_of_birth"),
        )
```

**Ports are `typing.Protocol`s** in the member's `application/interfaces/ports.py`. To add a new external
dependency:
1. Define a `Protocol` with the methods the application needs.
2. Implement it with a concrete class in `infrastructure/`.
3. Wire the implementation in the entrypoint (composition root).

```python
class BiobankRepository(Protocol):
    def list_patients(self) -> list[Patient]: ...
```

**Fingerprinting (uploader):** change detection uses `compute_fingerprint(*objs)` (SHA-256 over sorted-key
JSON of dataclasses) in `uploader.domain.models.sync`. Reuse it rather than rolling your own hashing.

## Dependencies

Use uv; do not hand-edit `pyproject.toml` versions or `uv.lock`:

```bash
cd apps/<pkg> && uv add <pkg>     # runtime dependency of one member
uv add --group dev <pkg>          # shared dev dependency (run at the repo root)
```

## Validation loop

After any change, run for each package you touched (`<pkg>` = `uploader` or `biobank_api`) and fix until clean:

```bash
uv run ruff check apps/<pkg>
uv run ruff format apps/<pkg>
uv run mypy apps/<pkg>
```

mypy targets Python 3.11. Do not loosen the ruff/mypy config to silence errors - fix the code.
