from typing import Protocol

from domain.models import (
    AnyOperation,
    Clinical,
    EntitySyncState,
    ImagingStudy,
    PatientAggregate,
    PatientSyncState,
    PatientSyncStates,
    Personal,
    Sample,
    SequencingData,
    WsiData,
)


class SourceDataGateway(Protocol):
    def fetch_patients(self) -> list[dict]: ...

    def fetch_radiology(self, accession_numbers: list[str]) -> list[dict]: ...

    def fetch_sequencing(self, predictive_number: str) -> dict | None: ...

    def fetch_wsi(self, bioptic_number: str) -> dict | None: ...


class CatalogueGateway(Protocol):
    def upsert_patient(
        self, patient_id: str, personal: Personal | None, clinical: Clinical | None
    ) -> str: ...

    def upsert_sample(self, sample: Sample, patient_id: str) -> str: ...

    def upsert_sequencing(self, sequencing: SequencingData, sample_id: str) -> str: ...

    def upsert_wsi(self, wsi: WsiData, sample_id: str) -> str: ...

    def upsert_imaging_study(self, study: ImagingStudy, patient_id: str) -> str: ...

    def delete(
        self, entity_type: str, entity_key: str, remote_id: str | None
    ) -> None: ...


class SyncStateRepository(Protocol):
    def get_all_for_patient(self, patient_id: str) -> PatientSyncStates: ...

    def save(self, state: EntitySyncState) -> None: ...

    def soft_delete_children(self, parent_key: str, run_id: str) -> None: ...

    def mark_missing_patients_as_deleted(
        self, seen_ids: set[str], run_id: str
    ) -> list[PatientSyncState]: ...


class SyncPlanner(Protocol):
    def plan(
        self, aggregate: PatientAggregate, existing: PatientSyncStates
    ) -> list[AnyOperation]: ...
