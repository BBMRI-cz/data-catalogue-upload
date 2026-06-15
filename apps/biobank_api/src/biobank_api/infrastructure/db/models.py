from __future__ import annotations

from typing import Any

from sqlalchemy import JSON, String
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column


class Base(DeclarativeBase):
    pass


class PatientORM(Base):
    __tablename__ = "patient"

    external_id: Mapped[str] = mapped_column(String, primary_key=True)
    # Raw parsed patient record; the typed shape ClinicalBuilder consumes lands in #33.
    payload: Mapped[dict[str, Any]] = mapped_column(JSON, nullable=False, default=dict)
