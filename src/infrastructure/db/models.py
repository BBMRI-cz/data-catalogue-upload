from __future__ import annotations

import enum
from datetime import datetime

from sqlalchemy import (
    Boolean,
    DateTime,
    Enum,
    ForeignKey,
    Integer,
    String,
    Text,
    func,
)
from sqlalchemy.orm import (
    DeclarativeBase,
    Mapped,
    mapped_column,
    relationship,
)


class Base(DeclarativeBase):
    pass


class SyncStatusDB(enum.Enum):
    PENDING = "pending"
    SYNCED = "synced"
    FAILED = "failed"
    DELETED = "deleted"


class SyncRunORM(Base):
    __tablename__ = "sync_run"

    id: Mapped[str] = mapped_column(String, primary_key=True)
    started_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now(), nullable=False
    )
    finished_at: Mapped[datetime | None] = mapped_column(
        DateTime(timezone=True), nullable=True
    )
    scanned_count: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    changed_count: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    uploaded_count: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    deleted_count: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    skipped_count: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    failed_count: Mapped[int] = mapped_column(Integer, nullable=False, default=0)


class _SyncStateColumns:
    """Shared sync-state columns mixed into every per-boundary table."""

    source_fingerprint: Mapped[str] = mapped_column(String, nullable=False)
    catalogue_remote_id: Mapped[str | None] = mapped_column(String, nullable=True)
    status: Mapped[SyncStatusDB] = mapped_column(
        Enum(SyncStatusDB, native_enum=False), nullable=False, index=True
    )
    is_deleted: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False)
    last_seen_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now(), nullable=False
    )
    last_synced_at: Mapped[datetime | None] = mapped_column(
        DateTime(timezone=True), nullable=True
    )
    last_error: Mapped[str | None] = mapped_column(Text, nullable=True)
    run_id: Mapped[str] = mapped_column(String, nullable=False, index=True)


class PatientSyncStateORM(_SyncStateColumns, Base):
    __tablename__ = "patient_sync_state"

    patient_id: Mapped[str] = mapped_column(String, primary_key=True)

    samples: Mapped[list[SampleSyncStateORM]] = relationship(back_populates="patient")
    imaging_studies: Mapped[list[ImagingStudySyncStateORM]] = relationship(
        back_populates="patient"
    )


class SampleSyncStateORM(_SyncStateColumns, Base):
    __tablename__ = "sample_sync_state"

    sample_id: Mapped[str] = mapped_column(String, primary_key=True)
    patient_id: Mapped[str] = mapped_column(
        String,
        ForeignKey("patient_sync_state.patient_id", ondelete="CASCADE"),
        nullable=False,
        index=True,
    )

    patient: Mapped[PatientSyncStateORM] = relationship(back_populates="samples")
    sequencing: Mapped[list[SequencingSyncStateORM]] = relationship(
        back_populates="sample"
    )
    wsi: Mapped[list[WsiSyncStateORM]] = relationship(back_populates="sample")


class SequencingSyncStateORM(_SyncStateColumns, Base):
    __tablename__ = "sequencing_sync_state"

    predictive_number: Mapped[str] = mapped_column(String, primary_key=True)
    sample_id: Mapped[str] = mapped_column(
        String,
        ForeignKey("sample_sync_state.sample_id", ondelete="CASCADE"),
        nullable=False,
        index=True,
    )

    sample: Mapped[SampleSyncStateORM] = relationship(back_populates="sequencing")


class WsiSyncStateORM(_SyncStateColumns, Base):
    __tablename__ = "wsi_sync_state"

    bioptic_number: Mapped[str] = mapped_column(String, primary_key=True)
    sample_id: Mapped[str] = mapped_column(
        String,
        ForeignKey("sample_sync_state.sample_id", ondelete="CASCADE"),
        nullable=False,
        index=True,
    )

    sample: Mapped[SampleSyncStateORM] = relationship(back_populates="wsi")


class ImagingStudySyncStateORM(_SyncStateColumns, Base):
    __tablename__ = "imaging_study_sync_state"

    accession_number: Mapped[str] = mapped_column(String, primary_key=True)
    patient_id: Mapped[str] = mapped_column(
        String,
        ForeignKey("patient_sync_state.patient_id", ondelete="CASCADE"),
        nullable=False,
        index=True,
    )

    patient: Mapped[PatientSyncStateORM] = relationship(
        back_populates="imaging_studies"
    )
