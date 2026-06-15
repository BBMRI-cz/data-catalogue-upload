from __future__ import annotations

from sqlalchemy import select
from sqlalchemy.orm import Session

from biobank_api.domain.models import Patient
from biobank_api.infrastructure.db.models import PatientORM


class SqlBiobankRepository:
    """SQLAlchemy-backed :class:`BiobankRepository`."""

    def __init__(self, session: Session) -> None:
        self._session = session

    def list_patients(self) -> list[Patient]:
        rows = self._session.scalars(select(PatientORM)).all()
        return [Patient(patient_id=row.external_id) for row in rows]

    def save_patients(self, patients: list[Patient]) -> None:
        # TODO(#33): serialize the full Patient (samples/clinical) into payload.
        for patient in patients:
            self._session.merge(PatientORM(external_id=patient.patient_id, payload={}))
        self._session.commit()
