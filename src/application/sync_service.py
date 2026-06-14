from __future__ import annotations

from dataclasses import dataclass
import uuid

from application.builders.clinical_builder import ClinicalBuilder
from application.builders.radiology_builder import RadiologyBuilder
from application.builders.sequencing_builder import SequencingBuilder
from application.builders.wsi_builder import WsiBuilder
from application.interfaces.ports import (
    CatalogueGateway,
    SourceDataGateway,
    SyncPlanner,
    SyncStateRepository,
)
from domain.models import (
    AnyOperation,
    ImagingStudyOperation,
    PatientAggregate,
    PatientOperation,
    RadiologyData,
    Sample,
    SampleOperation,
    SequencingOperation,
    SyncOp,
    SyncStatus,
    WsiOperation,
    now_utc,
)

_DELETE_ENTITY_TYPE = "patient"


@dataclass
class RunSummary:
    run_id: str
    scanned: int = 0
    changed: int = 0
    uploaded: int = 0
    deleted: int = 0
    skipped: int = 0
    failed: int = 0


class CatalogueSyncService:
    def __init__(
        self,
        source_gateway: SourceDataGateway,
        catalogue_gateway: CatalogueGateway,
        state_repository: SyncStateRepository,
        planner: SyncPlanner,
        clinical_builder: ClinicalBuilder | None = None,
        radiology_builder: RadiologyBuilder | None = None,
        sequencing_builder: SequencingBuilder | None = None,
        wsi_builder: WsiBuilder | None = None,
    ) -> None:
        self.source_gateway = source_gateway
        self.catalogue_gateway = catalogue_gateway
        self.state_repository = state_repository
        self.planner = planner
        self.clinical_builder = clinical_builder or ClinicalBuilder()
        self.radiology_builder = radiology_builder or RadiologyBuilder()
        self.sequencing_builder = sequencing_builder or SequencingBuilder()
        self.wsi_builder = wsi_builder or WsiBuilder()

    def run_catalogue_sync(self) -> RunSummary:
        run_id = str(uuid.uuid4())
        summary = RunSummary(run_id=run_id)
        raw_patients = self.source_gateway.fetch_patients()

        seen_patient_ids: set[str] = set()
        for raw_patient in raw_patients:
            aggregate = self._build_patient_aggregate(raw_patient)
            summary.scanned += 1
            seen_patient_ids.add(aggregate.patient_id)

            existing = self.state_repository.get_all_for_patient(aggregate.patient_id)
            for operation in self.planner.plan(aggregate, existing):
                self._execute(operation, run_id, summary)

        self._delete_missing_patients(seen_patient_ids, run_id, summary)
        return summary

    def _delete_missing_patients(
        self, seen_patient_ids: set[str], run_id: str, summary: RunSummary
    ) -> None:
        missing = self.state_repository.mark_missing_patients_as_deleted(
            seen_patient_ids, run_id
        )
        for state in missing:
            try:
                self.catalogue_gateway.delete(
                    _DELETE_ENTITY_TYPE, state.patient_id, state.catalogue_remote_id
                )
                summary.deleted += 1
            except Exception:  # prototype: keep run moving
                summary.failed += 1
            # children are soft-deleted in the DB only, no gateway calls
            self.state_repository.soft_delete_children(state.patient_id, run_id)

    def _execute(
        self, operation: AnyOperation, run_id: str, summary: RunSummary
    ) -> None:
        operation.state.run_id = run_id

        if operation.op == SyncOp.SKIP:
            summary.skipped += 1
            self.state_repository.save(operation.state)
            return

        if operation.op == SyncOp.DELETE:
            # soft delete: state already marked deleted by the planner, DB only
            summary.deleted += 1
            self.state_repository.save(operation.state)
            return

        summary.changed += 1
        try:
            remote_id = self._upsert(operation)
            operation.state.catalogue_remote_id = remote_id
            operation.state.status = SyncStatus.SYNCED
            operation.state.is_deleted = False
            operation.state.last_synced_at = now_utc()
            summary.uploaded += 1
            self.state_repository.save(operation.state)
        except Exception as exc:  # prototype: keep run moving
            summary.failed += 1
            operation.state.status = SyncStatus.FAILED
            operation.state.last_error = str(exc)
            self.state_repository.save(operation.state)

    def _upsert(self, operation: AnyOperation) -> str:
        if isinstance(operation, PatientOperation):
            personal, clinical = operation.entity if operation.entity else (None, None)
            return self.catalogue_gateway.upsert_patient(
                operation.state.patient_id, personal, clinical
            )
        if isinstance(operation, SampleOperation):
            assert operation.entity is not None
            return self.catalogue_gateway.upsert_sample(
                operation.entity, operation.state.patient_id
            )
        if isinstance(operation, SequencingOperation):
            assert operation.entity is not None
            return self.catalogue_gateway.upsert_sequencing(
                operation.entity, operation.state.sample_id
            )
        if isinstance(operation, WsiOperation):
            assert operation.entity is not None
            return self.catalogue_gateway.upsert_wsi(
                operation.entity, operation.state.sample_id
            )
        if isinstance(operation, ImagingStudyOperation):
            assert operation.entity is not None
            return self.catalogue_gateway.upsert_imaging_study(
                operation.entity, operation.state.patient_id
            )
        raise TypeError(f"Unsupported operation type: {type(operation)!r}")

    def _build_patient_aggregate(self, raw_patient: dict) -> PatientAggregate:
        personal = self.clinical_builder.build_personal(raw_patient)
        clinical = self.clinical_builder.build_clinical(raw_patient)
        samples: list[Sample] = []
        for raw_sample in raw_patient.get("samples", []):
            predictive_number = raw_sample.get("predictive_number")
            bioptic_number = raw_sample.get("bioptic_number")

            sequencing = None
            if predictive_number:
                sequencing_payload = self.source_gateway.fetch_sequencing(
                    predictive_number
                )
                if sequencing_payload:
                    sequencing = self.sequencing_builder.build_sequencing_data(
                        predictive_number=predictive_number,
                        payload=sequencing_payload,
                    )

            wsi = None
            if bioptic_number:
                wsi_payload = self.source_gateway.fetch_wsi(bioptic_number)
                if wsi_payload:
                    wsi = self.wsi_builder.build_wsi(
                        bioptic_number=bioptic_number,
                        payload=wsi_payload,
                    )

            samples.append(
                Sample(
                    sample_id=str(raw_sample["sample_id"]),
                    predictive_number=predictive_number,
                    bioptic_number=bioptic_number,
                    material=self.clinical_builder.build_material(raw_sample),
                    payload=raw_sample,
                    sequencing=sequencing,
                    wsi=wsi,
                )
            )

        accession_numbers = [
            str(value) for value in raw_patient.get("accession_numbers", [])
        ]
        radiology_payloads = self.source_gateway.fetch_radiology(accession_numbers)
        radiology: RadiologyData = []
        for item in radiology_payloads:
            if not isinstance(item, dict):
                continue
            radiology.append(self.radiology_builder.build_imaging_study(item))

        return PatientAggregate(
            patient_id=str(raw_patient["patient_id"]),
            accession_numbers=accession_numbers,
            personal=personal,
            clinical=clinical,
            samples=samples,
            payload=raw_patient,
            radiology=radiology,
        )
