from __future__ import annotations

from dataclasses import asdict, dataclass
import hashlib
import json

from domain.models.radiology import RadiologyData
from domain.models.sequencing import SequencingData
from domain.models.wsi import WsiData


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
class Material:
    material_identifier: str | None = None
    collected_from_person: str | None = None
    belongs_to_diagnosis: list[str] | None = None
    sampling_timestamp: str | None = None
    registration_timestamp: str | None = None
    sampling_protocol: str | None = None
    sampling_protocol_deviation: str | None = None
    reason_for_sampling_protocol_deviation: str | None = None
    biospecimen_type: str | None = None
    anatomical_source: str | None = None
    pathological_state: str | None = None
    storage_conditions: str | None = None
    expiration_date: str | None = None
    percentage_tumor_cells: float | None = None
    physical_location: str | None = None
    analyses_performed: list[str] | None = None
    derived_from: str | None = None


@dataclass(frozen=True)
class Sample:
    sample_id: str
    predictive_number: str | None
    bioptic_number: str | None
    payload: dict
    material: Material | None = None
    sequencing: SequencingData | None = None
    wsi: WsiData | None = None


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

    def source_fingerprint(self) -> str:
        serialized = json.dumps(asdict(self), sort_keys=True, separators=(",", ":"))
        return hashlib.sha256(serialized.encode("utf-8")).hexdigest()
