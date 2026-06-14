from __future__ import annotations

from dataclasses import replace
from typing import TypeVar

from domain.models import (
    AnyOperation,
    EntitySyncState,
    ImagingStudy,
    ImagingStudyOperation,
    ImagingStudySyncState,
    PatientAggregate,
    PatientOperation,
    PatientSyncState,
    PatientSyncStates,
    Sample,
    SampleOperation,
    SampleSyncState,
    SequencingOperation,
    SequencingSyncState,
    SyncOp,
    SyncStatus,
    WsiOperation,
    WsiSyncState,
    compute_fingerprint,
    now_utc,
)

StateT = TypeVar("StateT", bound=EntitySyncState)


class FingerprintSyncPlanner:
    """Plans per-entity catalogue operations by comparing fingerprints.

    Decision rule per entity:
      - no prior state, or prior state soft-deleted -> CREATE
      - fingerprint changed                          -> UPDATE
      - fingerprint unchanged                        -> SKIP
    Entities present in a prior run but absent now -> DELETE (soft, DB only).

    Operations are returned in dependency order: patient first, then samples
    and their sequencing/WSI children, then imaging studies, then deletions.
    """

    def plan(
        self, aggregate: PatientAggregate, existing: PatientSyncStates
    ) -> list[AnyOperation]:
        ops: list[AnyOperation] = []
        eligible = aggregate.is_upload_eligible()

        ops.append(self._plan_patient(aggregate, existing, eligible))

        seen_samples: set[str] = set()
        seen_sequencing: set[str] = set()
        seen_wsi: set[str] = set()
        seen_imaging: set[str] = set()

        if eligible:
            for sample in aggregate.samples:
                seen_samples.add(sample.sample_id)
                ops.append(self._plan_sample(sample, aggregate.patient_id, existing))

                if sample.sequencing and sample.predictive_number:
                    seen_sequencing.add(sample.predictive_number)
                    ops.append(
                        self._plan_sequencing(
                            sample, sample.predictive_number, existing
                        )
                    )

                if sample.wsi and sample.bioptic_number:
                    seen_wsi.add(sample.bioptic_number)
                    ops.append(self._plan_wsi(sample, sample.bioptic_number, existing))

            for study in aggregate.radiology:
                seen_imaging.add(study.accession_number)
                ops.append(
                    self._plan_imaging_study(study, aggregate.patient_id, existing)
                )

        ops.extend(
            self._plan_deletions(
                existing,
                seen_samples=seen_samples,
                seen_sequencing=seen_sequencing,
                seen_wsi=seen_wsi,
                seen_imaging=seen_imaging,
            )
        )
        return ops

    @staticmethod
    def _decide(new_fingerprint: str, prior: EntitySyncState | None) -> SyncOp:
        if prior is None or prior.is_deleted:
            return SyncOp.CREATE
        if prior.source_fingerprint != new_fingerprint:
            return SyncOp.UPDATE
        return SyncOp.SKIP

    def _plan_patient(
        self,
        aggregate: PatientAggregate,
        existing: PatientSyncStates,
        eligible: bool,
    ) -> PatientOperation:
        fingerprint = compute_fingerprint(aggregate.personal, aggregate.clinical)
        prior = existing.patient
        op = SyncOp.SKIP if not eligible else self._decide(fingerprint, prior)
        state = PatientSyncState(
            patient_id=aggregate.patient_id,
            source_fingerprint=fingerprint,
            catalogue_remote_id=prior.catalogue_remote_id if prior else None,
            status=prior.status if prior else SyncStatus.PENDING,
            is_deleted=False,
            last_seen_at=now_utc(),
            last_synced_at=prior.last_synced_at if prior else None,
            last_error=None,
            run_id="",
        )
        return PatientOperation(
            op=op,
            source_fingerprint=fingerprint,
            entity=(aggregate.personal, aggregate.clinical),
            state=state,
        )

    def _plan_sample(
        self, sample: Sample, patient_id: str, existing: PatientSyncStates
    ) -> SampleOperation:
        fingerprint = compute_fingerprint(sample.material)
        prior = existing.samples.get(sample.sample_id)
        state = SampleSyncState(
            sample_id=sample.sample_id,
            patient_id=patient_id,
            source_fingerprint=fingerprint,
            catalogue_remote_id=prior.catalogue_remote_id if prior else None,
            status=prior.status if prior else SyncStatus.PENDING,
            is_deleted=False,
            last_seen_at=now_utc(),
            last_synced_at=prior.last_synced_at if prior else None,
            last_error=None,
            run_id="",
        )
        return SampleOperation(
            op=self._decide(fingerprint, prior),
            source_fingerprint=fingerprint,
            entity=sample,
            state=state,
        )

    def _plan_sequencing(
        self, sample: Sample, predictive_number: str, existing: PatientSyncStates
    ) -> SequencingOperation:
        fingerprint = compute_fingerprint(sample.sequencing)
        prior = existing.sequencing.get(predictive_number)
        state = SequencingSyncState(
            predictive_number=predictive_number,
            sample_id=sample.sample_id,
            source_fingerprint=fingerprint,
            catalogue_remote_id=prior.catalogue_remote_id if prior else None,
            status=prior.status if prior else SyncStatus.PENDING,
            is_deleted=False,
            last_seen_at=now_utc(),
            last_synced_at=prior.last_synced_at if prior else None,
            last_error=None,
            run_id="",
        )
        return SequencingOperation(
            op=self._decide(fingerprint, prior),
            source_fingerprint=fingerprint,
            entity=sample.sequencing,
            state=state,
        )

    def _plan_wsi(
        self, sample: Sample, bioptic_number: str, existing: PatientSyncStates
    ) -> WsiOperation:
        fingerprint = compute_fingerprint(sample.wsi)
        prior = existing.wsi.get(bioptic_number)
        state = WsiSyncState(
            bioptic_number=bioptic_number,
            sample_id=sample.sample_id,
            source_fingerprint=fingerprint,
            catalogue_remote_id=prior.catalogue_remote_id if prior else None,
            status=prior.status if prior else SyncStatus.PENDING,
            is_deleted=False,
            last_seen_at=now_utc(),
            last_synced_at=prior.last_synced_at if prior else None,
            last_error=None,
            run_id="",
        )
        return WsiOperation(
            op=self._decide(fingerprint, prior),
            source_fingerprint=fingerprint,
            entity=sample.wsi,
            state=state,
        )

    def _plan_imaging_study(
        self, study: ImagingStudy, patient_id: str, existing: PatientSyncStates
    ) -> ImagingStudyOperation:
        fingerprint = compute_fingerprint(study)
        prior = existing.imaging_studies.get(study.accession_number)
        state = ImagingStudySyncState(
            accession_number=study.accession_number,
            patient_id=patient_id,
            source_fingerprint=fingerprint,
            catalogue_remote_id=prior.catalogue_remote_id if prior else None,
            status=prior.status if prior else SyncStatus.PENDING,
            is_deleted=False,
            last_seen_at=now_utc(),
            last_synced_at=prior.last_synced_at if prior else None,
            last_error=None,
            run_id="",
        )
        return ImagingStudyOperation(
            op=self._decide(fingerprint, prior),
            source_fingerprint=fingerprint,
            entity=study,
            state=state,
        )

    def _plan_deletions(
        self,
        existing: PatientSyncStates,
        seen_samples: set[str],
        seen_sequencing: set[str],
        seen_wsi: set[str],
        seen_imaging: set[str],
    ) -> list[AnyOperation]:
        deletions: list[AnyOperation] = []

        for key, sample_state in existing.samples.items():
            if key not in seen_samples and not sample_state.is_deleted:
                deletions.append(
                    SampleOperation(
                        op=SyncOp.DELETE,
                        source_fingerprint=sample_state.source_fingerprint,
                        entity=None,
                        state=self._as_deleted(sample_state),
                    )
                )

        for key, sequencing_state in existing.sequencing.items():
            if key not in seen_sequencing and not sequencing_state.is_deleted:
                deletions.append(
                    SequencingOperation(
                        op=SyncOp.DELETE,
                        source_fingerprint=sequencing_state.source_fingerprint,
                        entity=None,
                        state=self._as_deleted(sequencing_state),
                    )
                )

        for key, wsi_state in existing.wsi.items():
            if key not in seen_wsi and not wsi_state.is_deleted:
                deletions.append(
                    WsiOperation(
                        op=SyncOp.DELETE,
                        source_fingerprint=wsi_state.source_fingerprint,
                        entity=None,
                        state=self._as_deleted(wsi_state),
                    )
                )

        for key, imaging_state in existing.imaging_studies.items():
            if key not in seen_imaging and not imaging_state.is_deleted:
                deletions.append(
                    ImagingStudyOperation(
                        op=SyncOp.DELETE,
                        source_fingerprint=imaging_state.source_fingerprint,
                        entity=None,
                        state=self._as_deleted(imaging_state),
                    )
                )

        return deletions

    @staticmethod
    def _as_deleted(state: StateT) -> StateT:
        return replace(
            state,
            status=SyncStatus.DELETED,
            is_deleted=True,
            last_seen_at=now_utc(),
            last_error=None,
        )
