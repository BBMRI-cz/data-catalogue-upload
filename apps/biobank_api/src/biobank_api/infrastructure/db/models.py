from __future__ import annotations

from datetime import datetime

from sqlalchemy import (
    JSON,
    Boolean,
    DateTime,
    Enum,
    ForeignKey,
    Integer,
    String,
)
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column, relationship

from biobank_api.domain.enums import Retrieved, Sex


class Base(DeclarativeBase):
    pass


class PatientORM(Base):
    __tablename__ = "patient"

    patient_id: Mapped[str] = mapped_column(String, primary_key=True)
    biobank: Mapped[str | None] = mapped_column(String, nullable=True)
    consent: Mapped[bool | None] = mapped_column(Boolean, nullable=True)
    sex: Mapped[Sex | None] = mapped_column(Enum(Sex, native_enum=False), nullable=True)
    birth_year: Mapped[int | None] = mapped_column(Integer, nullable=True)
    birth_month: Mapped[int | None] = mapped_column(Integer, nullable=True)
    accession_numbers: Mapped[list[str]] = mapped_column(
        JSON, nullable=False, default=list
    )

    # delete-orphan so re-saving a patient (delete + re-insert) clears its child rows.
    tissue_samples: Mapped[list[TissueSampleORM]] = relationship(
        cascade="all, delete-orphan", lazy="selectin"
    )
    serum_samples: Mapped[list[SerumSampleORM]] = relationship(
        cascade="all, delete-orphan", lazy="selectin"
    )
    genome_samples: Mapped[list[GenomeSampleORM]] = relationship(
        cascade="all, delete-orphan", lazy="selectin"
    )
    diagnostic_specimens: Mapped[list[DiagnosticSpecimenORM]] = relationship(
        cascade="all, delete-orphan", lazy="selectin"
    )


class _SampleColumns:
    # Column mixin (not a table): the shared sample columns, mixed into each per-type table.
    sample_id: Mapped[str] = mapped_column(String, primary_key=True)
    patient_id: Mapped[str] = mapped_column(
        String,
        ForeignKey("patient.patient_id", ondelete="CASCADE"),
        nullable=False,
        index=True,
    )
    material_type: Mapped[str] = mapped_column(String, nullable=False)
    event_number: Mapped[int | None] = mapped_column(Integer, nullable=True)
    collection_year: Mapped[int | None] = mapped_column(Integer, nullable=True)
    biopsy: Mapped[str | None] = mapped_column(String, nullable=True)
    predictive_number: Mapped[str | None] = mapped_column(String, nullable=True)
    samples_no: Mapped[int | None] = mapped_column(Integer, nullable=True)
    available_samples_no: Mapped[int | None] = mapped_column(Integer, nullable=True)
    accession_numbers: Mapped[list[str]] = mapped_column(
        JSON, nullable=False, default=list
    )


class TissueSampleORM(_SampleColumns, Base):
    __tablename__ = "tissue_sample"

    diagnosis: Mapped[str | None] = mapped_column(String, nullable=True)
    p_tnm: Mapped[str | None] = mapped_column(String, nullable=True)
    morphology: Mapped[str | None] = mapped_column(String, nullable=True)
    cut_time: Mapped[datetime | None] = mapped_column(DateTime, nullable=True)
    freeze_time: Mapped[datetime | None] = mapped_column(DateTime, nullable=True)
    retrieved: Mapped[Retrieved | None] = mapped_column(
        Enum(Retrieved, native_enum=False), nullable=True
    )


class SerumSampleORM(_SampleColumns, Base):
    __tablename__ = "serum_sample"

    diagnosis: Mapped[str | None] = mapped_column(String, nullable=True)
    taking_date: Mapped[datetime | None] = mapped_column(DateTime, nullable=True)
    retrieved: Mapped[Retrieved | None] = mapped_column(
        Enum(Retrieved, native_enum=False), nullable=True
    )


class GenomeSampleORM(_SampleColumns, Base):
    __tablename__ = "genome_sample"

    taking_date: Mapped[datetime | None] = mapped_column(DateTime, nullable=True)
    retrieved: Mapped[Retrieved | None] = mapped_column(
        Enum(Retrieved, native_enum=False), nullable=True
    )


class DiagnosticSpecimenORM(Base):
    __tablename__ = "diagnostic_specimen"

    sample_id: Mapped[str] = mapped_column(String, primary_key=True)
    patient_id: Mapped[str] = mapped_column(
        String,
        ForeignKey("patient.patient_id", ondelete="CASCADE"),
        nullable=False,
        index=True,
    )
    specimen_number: Mapped[int | None] = mapped_column(Integer, nullable=True)
    year: Mapped[int | None] = mapped_column(Integer, nullable=True)
    material_type: Mapped[str | None] = mapped_column(String, nullable=True)
    diagnosis: Mapped[str | None] = mapped_column(String, nullable=True)
    taking_date: Mapped[datetime | None] = mapped_column(DateTime, nullable=True)
    retrieved: Mapped[Retrieved | None] = mapped_column(
        Enum(Retrieved, native_enum=False), nullable=True
    )
