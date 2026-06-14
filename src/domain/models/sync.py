from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
from enum import Enum


class SyncStatus(Enum):
    PENDING = "pending"
    SYNCED = "synced"
    FAILED = "failed"
    DELETED = "deleted"


class CatalogueOperation(Enum):
    CREATE = "create"
    UPDATE = "update"
    DELETE = "delete"
    SKIP = "skip"


@dataclass
class SyncState:
    entity_type: str
    entity_key: str
    source_fingerprint: str
    catalogue_remote_id: str | None
    status: SyncStatus
    is_deleted: bool
    last_seen_at: datetime
    last_synced_at: datetime | None
    last_error: str | None
    run_id: str


@dataclass(frozen=True)
class PlannedOperation:
    entity_type: str
    entity_key: str
    operation: CatalogueOperation
    payload: dict | None
    source_fingerprint: str | None


def now_utc() -> datetime:
    return datetime.now(timezone.utc)
