---
name: python-dev
description: Coding patterns and architecture rules for the data-catalogue-upload Python codebase. Use when writing or modifying Python in src/ - adding domain models, application builders/services, infrastructure adapters (HTTP gateways, DB repositories), or ports. Covers the hexagonal layer boundaries, dataclass and Protocol patterns, uv usage, and the lint/type validation loop.
---

# Python development (data-catalogue-upload)

This project is a hexagonal / layered sync job. Keep changes inside the right layer and validate with ruff + mypy before finishing.

## Layers and dependency direction

Dependencies only point inward: `infrastructure` -> `application` -> `domain`.

| Layer | Path | Put here | Never import |
|-------|------|----------|--------------|
| Domain | `src/domain/` | Pure dataclass models, `compute_fingerprint`, parsing helpers | `application`, `infrastructure`, `requests`, `sqlalchemy` |
| Application | `src/application/` | Use cases (`sync_service.py`), planning (`sync_planner.py`), `builders/`, ports (`interfaces/ports.py`) | concrete infrastructure classes |
| Infrastructure | `src/infrastructure/` | HTTP gateways (`api/clients.py`), DB ORM + repositories (`db/`) | - |

`main.py` is the composition root; it is the only place that wires concrete infrastructure to application use cases.

## Patterns

**Start modules with future annotations:**

```python
from __future__ import annotations
```

**Domain models are dataclasses.** Define new entities as `@dataclass` in `src/domain/models/` and export them from `domain/models/__init__.py`.

**Builders map raw `dict` -> domain objects** using `.get(...)` for optional fields. Follow `application/builders/clinical_builder.py`:

```python
class ClinicalBuilder:
    def build_personal(self, payload: dict) -> Personal:
        return Personal(
            personal_identifier=payload.get("personal_identifier"),
            year_of_birth=payload.get("year_of_birth"),
        )
```

**Ports are `typing.Protocol`s** in `application/interfaces/ports.py`. To add a new external dependency:
1. Define a `Protocol` with the methods the application needs.
2. Implement it with a concrete class in `infrastructure/`.
3. Wire the implementation in `main.py`.

```python
class SourceDataGateway(Protocol):
    def fetch_patients(self) -> list[dict]: ...
```

**Fingerprinting:** change detection uses `compute_fingerprint(*objs)` (SHA-256 over sorted-key JSON of dataclasses) in `domain/models/sync.py`. Reuse it rather than rolling your own hashing.

## Dependencies

Use uv; do not hand-edit `pyproject.toml` versions or `uv.lock`:

```bash
uv add <pkg>                 # runtime dependency
uv add --group dev <pkg>     # dev dependency
```

## Validation loop

After any change, run from the repo root and fix until clean:

```bash
uv run ruff check .
uv run ruff format .
uv run mypy .
```

mypy targets Python 3.11. Do not loosen the ruff/mypy config to silence errors - fix the code.
