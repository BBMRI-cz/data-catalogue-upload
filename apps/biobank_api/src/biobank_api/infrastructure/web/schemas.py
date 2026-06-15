from __future__ import annotations

from pydantic import BaseModel, Field


class SampleOut(BaseModel):
    sample_id: str
    material_type: str | None = None


class PatientOut(BaseModel):
    """Response shape for ``GET /patients``.

    Minimal scaffold shape; the full FAIR Genomes payload the uploader's
    ``ClinicalBuilder`` consumes is defined in #33.
    """

    patient_id: str
    samples: list[SampleOut] = Field(default_factory=list)
