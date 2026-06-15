from __future__ import annotations

from fastapi.testclient import TestClient

from biobank_api.application.get_patients import GetPatients
from biobank_api.domain.models import Patient
from biobank_api.infrastructure.web.app import create_app
from biobank_api.infrastructure.web.dependencies import get_patients_use_case


class FakeRepository:
    """In-memory BiobankRepository so the API can be tested without a database."""

    def __init__(self, patients: list[Patient]) -> None:
        self._patients = patients

    def list_patients(self) -> list[Patient]:
        return self._patients

    def save_patients(self, patients: list[Patient]) -> None:
        self._patients = patients


def _client(patients: list[Patient]) -> TestClient:
    app = create_app()
    app.dependency_overrides[get_patients_use_case] = lambda: GetPatients(
        FakeRepository(patients)
    )
    return TestClient(app)


def test_health() -> None:
    response = _client([]).get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


def test_list_patients_empty() -> None:
    response = _client([]).get("/patients")
    assert response.status_code == 200
    assert response.json() == []


def test_list_patients_returns_repository_data() -> None:
    response = _client([Patient(patient_id="P1")]).get("/patients")
    assert response.status_code == 200
    assert response.json() == [{"patient_id": "P1", "samples": []}]
