from __future__ import annotations

from collections.abc import Iterable
from datetime import datetime, timezone

from sqlalchemy import select
from sqlalchemy.orm import Session

from domain.models import (
    EntitySyncState,
    ImagingStudySyncState,
    PatientSyncState,
    PatientSyncStates,
    SampleSyncState,
    SequencingSyncState,
    SyncStatus,
    WsiSyncState,
)
from infrastructure.db.models import (
    ImagingStudySyncStateORM,
    PatientSyncStateORM,
    SampleSyncStateORM,
    SequencingSyncStateORM,
    SyncStatusDB,
    WsiSyncStateORM,
)


def _patient_to_domain(orm: PatientSyncStateORM) -> PatientSyncState:
    return PatientSyncState(
        patient_id=orm.patient_id,
        source_fingerprint=orm.source_fingerprint,
        catalogue_remote_id=orm.catalogue_remote_id,
        status=SyncStatus(orm.status.value),
        is_deleted=orm.is_deleted,
        last_seen_at=orm.last_seen_at,
        last_synced_at=orm.last_synced_at,
        last_error=orm.last_error,
        run_id=orm.run_id,
    )


def _sample_to_domain(orm: SampleSyncStateORM) -> SampleSyncState:
    return SampleSyncState(
        sample_id=orm.sample_id,
        patient_id=orm.patient_id,
        source_fingerprint=orm.source_fingerprint,
        catalogue_remote_id=orm.catalogue_remote_id,
        status=SyncStatus(orm.status.value),
        is_deleted=orm.is_deleted,
        last_seen_at=orm.last_seen_at,
        last_synced_at=orm.last_synced_at,
        last_error=orm.last_error,
        run_id=orm.run_id,
    )


def _sequencing_to_domain(orm: SequencingSyncStateORM) -> SequencingSyncState:
    return SequencingSyncState(
        predictive_number=orm.predictive_number,
        sample_id=orm.sample_id,
        source_fingerprint=orm.source_fingerprint,
        catalogue_remote_id=orm.catalogue_remote_id,
        status=SyncStatus(orm.status.value),
        is_deleted=orm.is_deleted,
        last_seen_at=orm.last_seen_at,
        last_synced_at=orm.last_synced_at,
        last_error=orm.last_error,
        run_id=orm.run_id,
    )


def _wsi_to_domain(orm: WsiSyncStateORM) -> WsiSyncState:
    return WsiSyncState(
        bioptic_number=orm.bioptic_number,
        sample_id=orm.sample_id,
        source_fingerprint=orm.source_fingerprint,
        catalogue_remote_id=orm.catalogue_remote_id,
        status=SyncStatus(orm.status.value),
        is_deleted=orm.is_deleted,
        last_seen_at=orm.last_seen_at,
        last_synced_at=orm.last_synced_at,
        last_error=orm.last_error,
        run_id=orm.run_id,
    )


def _imaging_to_domain(orm: ImagingStudySyncStateORM) -> ImagingStudySyncState:
    return ImagingStudySyncState(
        accession_number=orm.accession_number,
        patient_id=orm.patient_id,
        source_fingerprint=orm.source_fingerprint,
        catalogue_remote_id=orm.catalogue_remote_id,
        status=SyncStatus(orm.status.value),
        is_deleted=orm.is_deleted,
        last_seen_at=orm.last_seen_at,
        last_synced_at=orm.last_synced_at,
        last_error=orm.last_error,
        run_id=orm.run_id,
    )


_AnyStateORM = (
    PatientSyncStateORM
    | SampleSyncStateORM
    | SequencingSyncStateORM
    | WsiSyncStateORM
    | ImagingStudySyncStateORM
)


class SyncStateRepository:
    def __init__(self, session: Session):
        self.session = session

    def get_all_for_patient(self, patient_id: str) -> PatientSyncStates:
        patient_orm = self.session.get(PatientSyncStateORM, patient_id)
        patient = _patient_to_domain(patient_orm) if patient_orm else None

        sample_orms = list(
            self.session.execute(
                select(SampleSyncStateORM).where(
                    SampleSyncStateORM.patient_id == patient_id
                )
            ).scalars()
        )
        imaging_orms = list(
            self.session.execute(
                select(ImagingStudySyncStateORM).where(
                    ImagingStudySyncStateORM.patient_id == patient_id
                )
            ).scalars()
        )

        sample_ids = [orm.sample_id for orm in sample_orms]
        sequencing_orms: list[SequencingSyncStateORM] = []
        wsi_orms: list[WsiSyncStateORM] = []
        if sample_ids:
            sequencing_orms = list(
                self.session.execute(
                    select(SequencingSyncStateORM).where(
                        SequencingSyncStateORM.sample_id.in_(sample_ids)
                    )
                ).scalars()
            )
            wsi_orms = list(
                self.session.execute(
                    select(WsiSyncStateORM).where(
                        WsiSyncStateORM.sample_id.in_(sample_ids)
                    )
                ).scalars()
            )

        return PatientSyncStates(
            patient=patient,
            samples={orm.sample_id: _sample_to_domain(orm) for orm in sample_orms},
            sequencing={
                orm.predictive_number: _sequencing_to_domain(orm)
                for orm in sequencing_orms
            },
            wsi={orm.bioptic_number: _wsi_to_domain(orm) for orm in wsi_orms},
            imaging_studies={
                orm.accession_number: _imaging_to_domain(orm) for orm in imaging_orms
            },
        )

    def save(self, state: EntitySyncState) -> None:
        if isinstance(state, PatientSyncState):
            self._save_patient(state)
        elif isinstance(state, SampleSyncState):
            self._save_sample(state)
        elif isinstance(state, SequencingSyncState):
            self._save_sequencing(state)
        elif isinstance(state, WsiSyncState):
            self._save_wsi(state)
        elif isinstance(state, ImagingStudySyncState):
            self._save_imaging(state)
        else:
            raise TypeError(f"Unsupported sync state type: {type(state)!r}")
        self.session.commit()

    def soft_delete_children(self, parent_key: str, run_id: str) -> None:
        """Soft-delete a patient's whole subtree in the DB only.

        ``parent_key`` is the patient_id. No catalogue gateway calls are made.
        """
        now = datetime.now(timezone.utc)

        sample_orms = list(
            self.session.execute(
                select(SampleSyncStateORM).where(
                    SampleSyncStateORM.patient_id == parent_key
                )
            ).scalars()
        )
        imaging_orms = list(
            self.session.execute(
                select(ImagingStudySyncStateORM).where(
                    ImagingStudySyncStateORM.patient_id == parent_key
                )
            ).scalars()
        )
        self._mark_deleted(sample_orms, run_id, now)
        self._mark_deleted(imaging_orms, run_id, now)

        sample_ids = [orm.sample_id for orm in sample_orms]
        if sample_ids:
            sequencing_orms = self.session.execute(
                select(SequencingSyncStateORM).where(
                    SequencingSyncStateORM.sample_id.in_(sample_ids)
                )
            ).scalars()
            wsi_orms = self.session.execute(
                select(WsiSyncStateORM).where(WsiSyncStateORM.sample_id.in_(sample_ids))
            ).scalars()
            self._mark_deleted(sequencing_orms, run_id, now)
            self._mark_deleted(wsi_orms, run_id, now)

        self.session.commit()

    def mark_missing_patients_as_deleted(
        self, seen_ids: set[str], run_id: str
    ) -> list[PatientSyncState]:
        rows = self.session.execute(select(PatientSyncStateORM)).scalars()
        missing: list[PatientSyncState] = []
        now = datetime.now(timezone.utc)
        for row in rows:
            if row.patient_id in seen_ids or row.is_deleted:
                continue
            self._mark_one_deleted(row, run_id, now)
            missing.append(_patient_to_domain(row))
        self.session.commit()
        return missing

    def _save_patient(self, state: PatientSyncState) -> None:
        orm = self.session.get(PatientSyncStateORM, state.patient_id)
        if orm is None:
            orm = PatientSyncStateORM(patient_id=state.patient_id)
            self.session.add(orm)
        self._apply_common(orm, state)

    def _save_sample(self, state: SampleSyncState) -> None:
        orm = self.session.get(SampleSyncStateORM, state.sample_id)
        if orm is None:
            orm = SampleSyncStateORM(
                sample_id=state.sample_id, patient_id=state.patient_id
            )
            self.session.add(orm)
        else:
            orm.patient_id = state.patient_id
        self._apply_common(orm, state)

    def _save_sequencing(self, state: SequencingSyncState) -> None:
        orm = self.session.get(SequencingSyncStateORM, state.predictive_number)
        if orm is None:
            orm = SequencingSyncStateORM(
                predictive_number=state.predictive_number, sample_id=state.sample_id
            )
            self.session.add(orm)
        else:
            orm.sample_id = state.sample_id
        self._apply_common(orm, state)

    def _save_wsi(self, state: WsiSyncState) -> None:
        orm = self.session.get(WsiSyncStateORM, state.bioptic_number)
        if orm is None:
            orm = WsiSyncStateORM(
                bioptic_number=state.bioptic_number, sample_id=state.sample_id
            )
            self.session.add(orm)
        else:
            orm.sample_id = state.sample_id
        self._apply_common(orm, state)

    def _save_imaging(self, state: ImagingStudySyncState) -> None:
        orm = self.session.get(ImagingStudySyncStateORM, state.accession_number)
        if orm is None:
            orm = ImagingStudySyncStateORM(
                accession_number=state.accession_number, patient_id=state.patient_id
            )
            self.session.add(orm)
        else:
            orm.patient_id = state.patient_id
        self._apply_common(orm, state)

    @staticmethod
    def _apply_common(orm: _AnyStateORM, state: EntitySyncState) -> None:
        orm.source_fingerprint = state.source_fingerprint
        orm.catalogue_remote_id = state.catalogue_remote_id
        orm.status = SyncStatusDB(state.status.value)
        orm.is_deleted = state.is_deleted
        orm.last_seen_at = state.last_seen_at
        orm.last_synced_at = state.last_synced_at
        orm.last_error = state.last_error
        orm.run_id = state.run_id

    def _mark_deleted(
        self, orms: Iterable[_AnyStateORM], run_id: str, now: datetime
    ) -> None:
        for orm in orms:
            if not orm.is_deleted:
                self._mark_one_deleted(orm, run_id, now)

    @staticmethod
    def _mark_one_deleted(orm: _AnyStateORM, run_id: str, now: datetime) -> None:
        orm.is_deleted = True
        orm.status = SyncStatusDB.DELETED
        orm.last_seen_at = now
        orm.last_synced_at = now
        orm.last_error = None
        orm.run_id = run_id
