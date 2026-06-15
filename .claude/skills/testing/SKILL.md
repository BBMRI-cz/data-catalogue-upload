---
name: testing
description: Pytest conventions for the data-catalogue-upload codebase. Use when writing, running, or fixing tests - unit-testing builders and the planner, faking the SourceDataGateway / CatalogueGateway / SyncStateRepository Protocol ports, and running pytest via uv.
---

# Testing (data-catalogue-upload)

Tests live in `tests/` at the repo root and run with pytest via uv.

## Running

```bash
uv run pytest            # all tests
uv run pytest -v         # verbose
uv run pytest tests/test_clinical_builder.py::test_build_personal   # one test
```

## Conventions

- Put test files in `tests/`, named `test_*.py`, with functions named `test_*`.
- Prefer fast, pure unit tests. The domain and application layers have no I/O, so they need no DB or network.

## Testing builders

Builders are pure dict -> dataclass mappers. Pass a raw payload and assert on the returned domain object:

```python
from application.builders.clinical_builder import ClinicalBuilder


def test_build_personal_maps_fields():
    payload = {"personal_identifier": "P1", "year_of_birth": 1980}
    personal = ClinicalBuilder().build_personal(payload)
    assert personal.personal_identifier == "P1"
    assert personal.year_of_birth == 1980


def test_build_personal_handles_missing_fields():
    personal = ClinicalBuilder().build_personal({})
    assert personal.personal_identifier is None
```

## Faking the ports

The application depends on Protocols (`SourceDataGateway`, `CatalogueGateway`, `SyncStateRepository`, `SyncPlanner` in `application/interfaces/ports.py`), so tests can pass simple fakes - no mocking framework required. Implement only the methods the test exercises:

```python
class FakeSourceGateway:
    def __init__(self, patients): self._patients = patients
    def fetch_patients(self): return self._patients
    def fetch_radiology(self, accession_numbers): return []
    def fetch_sequencing(self, predictive_number): return None
    def fetch_wsi(self, bioptic_number): return None
```

A class that satisfies the Protocol's method signatures is accepted by mypy - it does not need to inherit from it. Inject the fake where `main.py` would inject the real gateway (e.g. into `CatalogueSyncService`).

## CI

`uv run pytest` runs in CI on every push/PR to `master`. Keep tests green before pushing.
