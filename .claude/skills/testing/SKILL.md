---
name: testing
description: Pytest conventions for the data-catalogue-upload workspace. Use when writing, running, or fixing tests in any member under apps/ - unit-testing builders/use cases and the planner, faking Protocol ports (SourceDataGateway / CatalogueGateway / SyncStateRepository / BiobankRepository), testing FastAPI endpoints with TestClient, and running pytest via uv.
---

# Testing (data-catalogue-upload)

Each workspace member keeps its tests in `apps/<pkg>/tests/`, named `test_*.py`, run with pytest via uv.

## Running

```bash
uv run pytest apps/uploader/tests        # one member's tests
uv run pytest apps/biobank_api/tests
uv run pytest apps/uploader/tests -v     # verbose
uv run pytest apps/uploader/tests/test_clinical_builder.py::test_build_personal   # one test
```

## Conventions

- Put test files in the member's `tests/`, named `test_*.py`, with functions named `test_*`.
- Prefer fast, pure unit tests. The domain and application layers have no I/O, so they need no DB or network.
- Import from the member's package: `from uploader.application.builders... import ...`,
  `from biobank_api.domain.models import Patient`.

## Testing builders / use cases

Builders are pure dict -> dataclass mappers. Pass a raw payload and assert on the returned domain object:

```python
from uploader.application.builders.clinical_builder import ClinicalBuilder


def test_build_personal_maps_fields():
    payload = {"personal_identifier": "P1", "year_of_birth": 1980}
    personal = ClinicalBuilder().build_personal(payload)
    assert personal.personal_identifier == "P1"
    assert personal.year_of_birth == 1980
```

## Faking the ports

The application depends on Protocols (e.g. `SourceDataGateway`, `CatalogueGateway`, `SyncStateRepository` in
the uploader; `BiobankRepository`, `XmlExportSource` in biobank_api), so tests can pass simple fakes - no
mocking framework required. Implement only the methods the test exercises:

```python
class FakeBiobankRepository:
    def __init__(self, patients): self._patients = patients
    def list_patients(self): return self._patients
    def save_patients(self, patients): self._patients = patients
```

A class that satisfies the Protocol's method signatures is accepted by mypy - it does not need to inherit
from it. Inject the fake where the composition root would inject the real adapter.

## Testing FastAPI endpoints (API services)

Use Starlette's `TestClient` and override the route's dependency to avoid a live database (see
`apps/biobank_api/tests/test_server.py`):

```python
app = create_app()
app.dependency_overrides[get_patients_use_case] = lambda: GetPatients(FakeBiobankRepository([]))
client = TestClient(app)
assert client.get("/patients").json() == []
```

## CI

CI runs `uv run pytest apps/<pkg>/tests` per member in a matrix on every push/PR to `master`. Keep tests
green before pushing.
