"""init biobank_api schema

Revision ID: 0001_init
Revises:
Create Date: 2026-06-15

"""

from __future__ import annotations

import sqlalchemy as sa
from alembic import op

from biobank_api.domain.enums import Retrieved, Sex

revision = "0001_init"
down_revision = None
branch_labels = None
depends_on = None


def _shared_sample_columns() -> list[sa.Column]:
    """Fresh ``Column`` objects for the shared ``Sample`` group (one set per table)."""
    return [
        sa.Column("sample_id", sa.String(), primary_key=True),
        sa.Column(
            "patient_id",
            sa.String(),
            sa.ForeignKey("patient.patient_id", ondelete="CASCADE"),
            nullable=False,
        ),
        sa.Column("material_type", sa.String(), nullable=False),
        sa.Column("event_number", sa.Integer(), nullable=True),
        sa.Column("collection_year", sa.Integer(), nullable=True),
        sa.Column("biopsy", sa.String(), nullable=True),
        sa.Column("predictive_number", sa.String(), nullable=True),
        sa.Column("samples_no", sa.Integer(), nullable=True),
        sa.Column("available_samples_no", sa.Integer(), nullable=True),
        sa.Column("accession_numbers", sa.JSON(), nullable=False),
    ]


def upgrade() -> None:
    op.create_table(
        "patient",
        sa.Column("patient_id", sa.String(), primary_key=True),
        sa.Column("biobank", sa.String(), nullable=True),
        sa.Column("consent", sa.Boolean(), nullable=True),
        sa.Column("sex", sa.Enum(Sex, native_enum=False), nullable=True),
        sa.Column("birth_year", sa.Integer(), nullable=True),
        sa.Column("birth_month", sa.Integer(), nullable=True),
        sa.Column("accession_numbers", sa.JSON(), nullable=False),
    )

    op.create_table(
        "tissue_sample",
        *_shared_sample_columns(),
        sa.Column("diagnosis", sa.String(), nullable=True),
        sa.Column("p_tnm", sa.String(), nullable=True),
        sa.Column("morphology", sa.String(), nullable=True),
        sa.Column("cut_time", sa.DateTime(), nullable=True),
        sa.Column("freeze_time", sa.DateTime(), nullable=True),
        sa.Column("retrieved", sa.Enum(Retrieved, native_enum=False), nullable=True),
    )
    op.create_index("ix_tissue_sample_patient_id", "tissue_sample", ["patient_id"])

    op.create_table(
        "serum_sample",
        *_shared_sample_columns(),
        sa.Column("diagnosis", sa.String(), nullable=True),
        sa.Column("taking_date", sa.DateTime(), nullable=True),
        sa.Column("retrieved", sa.Enum(Retrieved, native_enum=False), nullable=True),
    )
    op.create_index("ix_serum_sample_patient_id", "serum_sample", ["patient_id"])

    op.create_table(
        "genome_sample",
        *_shared_sample_columns(),
        sa.Column("taking_date", sa.DateTime(), nullable=True),
        sa.Column("retrieved", sa.Enum(Retrieved, native_enum=False), nullable=True),
    )
    op.create_index("ix_genome_sample_patient_id", "genome_sample", ["patient_id"])

    op.create_table(
        "diagnostic_specimen",
        sa.Column("sample_id", sa.String(), primary_key=True),
        sa.Column(
            "patient_id",
            sa.String(),
            sa.ForeignKey("patient.patient_id", ondelete="CASCADE"),
            nullable=False,
        ),
        sa.Column("specimen_number", sa.Integer(), nullable=True),
        sa.Column("year", sa.Integer(), nullable=True),
        sa.Column("material_type", sa.String(), nullable=True),
        sa.Column("diagnosis", sa.String(), nullable=True),
        sa.Column("taking_date", sa.DateTime(), nullable=True),
        sa.Column("retrieved", sa.Enum(Retrieved, native_enum=False), nullable=True),
    )
    op.create_index(
        "ix_diagnostic_specimen_patient_id", "diagnostic_specimen", ["patient_id"]
    )


def downgrade() -> None:
    op.drop_table("diagnostic_specimen")
    op.drop_table("genome_sample")
    op.drop_table("serum_sample")
    op.drop_table("tissue_sample")
    op.drop_table("patient")
