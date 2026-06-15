from __future__ import annotations

from biobank_api.application.interfaces.ports import BiobankRepository
from biobank_api.domain.models import Patient


class GetPatients:
    """Use case backing ``GET /patients``: list all ingested patients."""

    def __init__(self, repository: BiobankRepository) -> None:
        self._repository = repository

    def __call__(self) -> list[Patient]:
        return self._repository.list_patients()
