from __future__ import annotations

from dataclasses import dataclass

from domain.models.radiology import RadiologyData
from domain.models.sample import Sample


@dataclass(frozen=True)
class Personal:
    personal_identifier: str | None = None
    year_of_birth: int | None = None
    gender_at_birth: str | None = None
    gender_identity: str | None = None


@dataclass(frozen=True)
class Clinical:
    clinical_identifier: str | None = None
    belongs_to_person: str | None = None
    clinical_diagnosis: list[str] | None = None
    age_at_diagnosis: int | None = None
    age_of_onset: int | None = None


@dataclass(frozen=True)
class PatientAggregate:
    patient_id: str
    accession_numbers: list[str]
    personal: Personal | None
    clinical: Clinical | None
    samples: list[Sample]
    payload: dict
    radiology: RadiologyData

    def is_upload_eligible(self) -> bool:
        return len(self.samples) > 0
