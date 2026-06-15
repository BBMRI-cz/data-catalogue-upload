from __future__ import annotations

from dataclasses import dataclass

from uploader.domain.models.sequencing import SequencingData
from uploader.domain.models.wsi import WsiData


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
