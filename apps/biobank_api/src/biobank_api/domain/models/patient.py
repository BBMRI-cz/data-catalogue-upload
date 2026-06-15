from __future__ import annotations

from dataclasses import dataclass, field


@dataclass
class Sample:
    sample_id: str
    material_type: str | None = None


@dataclass
class Patient:
    patient_id: str
    samples: list[Sample] = field(default_factory=list)
    # TODO(#33): expand to the full FAIR Genomes shape the uploader's
    # ClinicalBuilder consumes (personal + clinical + material).
