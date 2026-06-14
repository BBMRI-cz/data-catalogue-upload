from __future__ import annotations

from dataclasses import asdict, dataclass, is_dataclass
from datetime import datetime, timezone
from enum import Enum
import hashlib
import json
from typing import Any

from domain.models.patient import Clinical, Personal
from domain.models.radiology import ImagingStudy
from domain.models.sample import Sample
from domain.models.sequencing import SequencingData
from domain.models.wsi import WsiData


class SyncStatus(Enum):
    PENDING = "pending"
    SYNCED = "synced"
    FAILED = "failed"
    DELETED = "deleted"


class SyncOp(Enum):
    CREATE = "create"
    UPDATE = "update"
    DELETE = "delete"
    SKIP = "skip"


# --- Sync state: one typed dataclass per entity boundary -------------------


@dataclass
class EntitySyncState:
    source_fingerprint: str
    catalogue_remote_id: str | None
    status: SyncStatus
    is_deleted: bool
    last_seen_at: datetime
    last_synced_at: datetime | None
    last_error: str | None
    run_id: str


@dataclass
class PatientSyncState(EntitySyncState):
    patient_id: str


@dataclass
class SampleSyncState(EntitySyncState):
    sample_id: str
    patient_id: str


@dataclass
class SequencingSyncState(EntitySyncState):
    predictive_number: str
    sample_id: str


@dataclass
class WsiSyncState(EntitySyncState):
    bioptic_number: str
    sample_id: str


@dataclass
class ImagingStudySyncState(EntitySyncState):
    accession_number: str
    patient_id: str


@dataclass
class PatientSyncStates:
    """All sync states loaded for a single patient subtree."""

    patient: PatientSyncState | None
    samples: dict[str, SampleSyncState]
    sequencing: dict[str, SequencingSyncState]
    wsi: dict[str, WsiSyncState]
    imaging_studies: dict[str, ImagingStudySyncState]

    @classmethod
    def empty(cls) -> PatientSyncStates:
        return cls(patient=None, samples={}, sequencing={}, wsi={}, imaging_studies={})


# --- Planned operations: one typed dataclass per entity boundary -----------


@dataclass(frozen=True)
class OperationBase:
    op: SyncOp
    source_fingerprint: str | None


@dataclass(frozen=True)
class PatientOperation(OperationBase):
    entity: tuple[Personal | None, Clinical | None] | None
    state: PatientSyncState


@dataclass(frozen=True)
class SampleOperation(OperationBase):
    entity: Sample | None
    state: SampleSyncState


@dataclass(frozen=True)
class SequencingOperation(OperationBase):
    entity: SequencingData | None
    state: SequencingSyncState


@dataclass(frozen=True)
class WsiOperation(OperationBase):
    entity: WsiData | None
    state: WsiSyncState


@dataclass(frozen=True)
class ImagingStudyOperation(OperationBase):
    entity: ImagingStudy | None
    state: ImagingStudySyncState


AnyOperation = (
    PatientOperation
    | SampleOperation
    | SequencingOperation
    | WsiOperation
    | ImagingStudyOperation
)


# --- Helpers ----------------------------------------------------------------


def now_utc() -> datetime:
    return datetime.now(timezone.utc)


def _to_serializable(obj: Any) -> Any:
    if is_dataclass(obj) and not isinstance(obj, type):
        return asdict(obj)
    if isinstance(obj, (list, tuple)):
        return [_to_serializable(item) for item in obj]
    return obj


def compute_fingerprint(*objs: Any) -> str:
    payload = [_to_serializable(obj) for obj in objs if obj is not None]
    serialized = json.dumps(payload, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(serialized.encode("utf-8")).hexdigest()
