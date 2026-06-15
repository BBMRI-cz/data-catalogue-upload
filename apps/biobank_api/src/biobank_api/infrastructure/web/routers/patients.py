from __future__ import annotations

from fastapi import APIRouter, Depends

from biobank_api.application.get_patients import GetPatients
from biobank_api.infrastructure.web.dependencies import get_patients_use_case
from biobank_api.infrastructure.web.schemas import PatientOut, SampleOut

router = APIRouter(tags=["patients"])


@router.get("/patients", response_model=list[PatientOut])
def list_patients(
    use_case: GetPatients = Depends(get_patients_use_case),
) -> list[PatientOut]:
    return [
        PatientOut(
            patient_id=patient.patient_id,
            samples=[
                SampleOut(sample_id=s.sample_id, material_type=s.material_type)
                for s in patient.samples
            ],
        )
        for patient in use_case()
    ]
