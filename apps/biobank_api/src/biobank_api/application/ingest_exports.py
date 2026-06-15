from __future__ import annotations

from biobank_api.application.interfaces.ports import BiobankRepository, XmlExportSource


class IngestExports:
    """Use case backing the ingestion entrypoint: parse XML exports and persist them."""

    def __init__(self, source: XmlExportSource, repository: BiobankRepository) -> None:
        self._source = source
        self._repository = repository

    def __call__(self) -> int:
        patients = self._source.parse_patients()
        self._repository.save_patients(patients)
        return len(patients)
