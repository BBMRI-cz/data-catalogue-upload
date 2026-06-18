from __future__ import annotations

from sqlalchemy import select
from sqlalchemy.orm import Session

from biobank_api.domain.models import Patient
from biobank_api.infrastructure.db.mappers import orm_to_patient, patient_to_orm
from biobank_api.infrastructure.db.models import PatientORM


class SqlBiobankRepository:
    def __init__(self, session: Session) -> None:
        self._session = session

    def list_patients(self) -> list[Patient]:
        rows = self._session.scalars(select(PatientORM)).all()
        return [orm_to_patient(row) for row in rows]

    def save_patients(self, patients: list[Patient]) -> None:
        """Upsert each patient and its children (idempotent re-ingest).

        Delete-then-insert per patient: deleting the existing ``PatientORM`` triggers the
        ``delete-orphan`` cascade, clearing its rows in every child table, so a re-save
        never leaves stale or duplicate sample/specimen rows.
        """
        for patient in patients:
            existing = self._session.get(PatientORM, patient.patient_id)
            if existing is not None:
                self._session.delete(existing)
                self._session.flush()
            self._session.add(patient_to_orm(patient))
        self._session.commit()
