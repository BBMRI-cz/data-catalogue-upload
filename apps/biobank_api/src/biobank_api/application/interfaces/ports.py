from __future__ import annotations

from typing import Protocol

from biobank_api.domain.models import Patient


class BiobankRepository(Protocol):
    """Persistence of ingested biobank patients (implemented by the db layer)."""

    def list_patients(self) -> list[Patient]: ...

    def save_patients(self, patients: list[Patient]) -> None: ...


class XmlExportSource(Protocol):
    """Parses biobank XML exports into domain patients (implemented by the xml layer)."""

    def parse_patients(self) -> list[Patient]: ...
