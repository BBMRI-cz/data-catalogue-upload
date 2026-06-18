---
name: testing
description: Pytest conventions for the data-catalogue-upload workspace. Use when writing, running, or fixing tests in any member under apps/ - unit-testing builders/use cases and the planner, faking Protocol ports (SourceDataGateway / CatalogueGateway / SyncStateRepository / BiobankRepository), testing FastAPI endpoints with TestClient, and running pytest via uv.
---

# Testing (data-catalogue-upload)

Each workspace member keeps its tests in `apps/<pkg>/tests/`, named `test_*.py`, run with pytest via uv.
They are split into `tests/unit/` (pure, no I/O) and `tests/integration/` (real adapters, e.g. a DB engine).

## Running

```bash
uv run pytest apps/uploader/tests        # one member's tests
uv run pytest apps/biobank_api/tests
uv run pytest apps/biobank_api/tests/unit         # fast pure tests only
uv run pytest apps/biobank_api/tests/integration  # DB round-trips (in-memory sqlite)
uv run pytest apps/uploader/tests -v     # verbose
uv run pytest apps/biobank_api/tests/unit/test_domain_models.py::test_patient_minimal_construction_stays_valid   # one test
```

## Conventions

- Put test files in the member's `tests/`, named `test_*.py`, with functions named `test_*`.
- Place pure tests (no I/O) in `tests/unit/`; tests that drive a real adapter (a DB engine, etc.) in `tests/integration/`.
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

## Integration tests (DB repositories)

A repository is tested against a **real engine**, not a mocked `Session` (a mock would just assert how
SQLAlchemy was called - testing the library). Use an in-memory SQLite engine built fresh per test. The
`session` fixture in `apps/biobank_api/tests/integration/conftest.py`:

```python
@pytest.fixture
def session() -> Iterator[Session]:
    engine = create_engine("sqlite://")

    @event.listens_for(engine, "connect")
    def _enable_sqlite_fk(dbapi_connection: object, _record: object) -> None:
        cursor = dbapi_connection.cursor()
        cursor.execute("PRAGMA foreign_keys=ON")  # off by default in sqlite
        cursor.close()

    Base.metadata.create_all(engine)
    db = sessionmaker(bind=engine)()
    try:
        yield db
    finally:
        db.close()
        engine.dispose()
```

A test then drives the real repository and asserts a save/read round-trip:

```python
def test_round_trip(session: Session) -> None:
    SqlBiobankRepository(session).save_patients([Patient(patient_id="P1", consent=True)])
    (loaded,) = SqlBiobankRepository(session).list_patients()
    assert loaded.patient_id == "P1"
```

The schema only uses column types SQLite supports, so this stays representative of PostgreSQL. Keep pure
mapping logic (ORM <-> domain) in `tests/unit/` where it needs no engine.

## CI

CI runs `uv run pytest apps/<pkg>/tests` per member in a matrix on every push/PR to `master` (pytest recurses
into `unit/` and `integration/`). Keep tests green before pushing.
