"""per-boundary sync_state tables

Replaces the single ``sync_state`` table with one table per entity boundary
(patient / sample / sequencing / wsi / imaging_study), wired together with
real foreign keys.

Revision ID: c1a2b3d4e5f6
Revises: 5f0c3103a18d
Create Date: 2026-06-14 00:00:00.000000

"""

from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = "c1a2b3d4e5f6"
down_revision: Union[str, Sequence[str], None] = "5f0c3103a18d"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def _status_column() -> sa.Column:
    return sa.Column(
        "status",
        sa.Enum(
            "PENDING",
            "SYNCED",
            "FAILED",
            "DELETED",
            name="syncstatusdb",
            native_enum=False,
        ),
        nullable=False,
    )


def _common_columns() -> list[sa.Column]:
    return [
        sa.Column("source_fingerprint", sa.String(), nullable=False),
        sa.Column("catalogue_remote_id", sa.String(), nullable=True),
        _status_column(),
        sa.Column("is_deleted", sa.Boolean(), nullable=False),
        sa.Column(
            "last_seen_at",
            sa.DateTime(timezone=True),
            server_default=sa.text("now()"),
            nullable=False,
        ),
        sa.Column("last_synced_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("last_error", sa.Text(), nullable=True),
        sa.Column("run_id", sa.String(), nullable=False),
    ]


def upgrade() -> None:
    """Upgrade schema."""
    op.drop_index(op.f("ix_sync_state_status"), table_name="sync_state")
    op.drop_index(op.f("ix_sync_state_run_id"), table_name="sync_state")
    op.drop_index(op.f("ix_sync_state_entity_type"), table_name="sync_state")
    op.drop_index(op.f("ix_sync_state_entity_key"), table_name="sync_state")
    op.drop_table("sync_state")

    op.create_table(
        "patient_sync_state",
        sa.Column("patient_id", sa.String(), nullable=False),
        *_common_columns(),
        sa.PrimaryKeyConstraint("patient_id"),
    )
    op.create_table(
        "sample_sync_state",
        sa.Column("sample_id", sa.String(), nullable=False),
        sa.Column("patient_id", sa.String(), nullable=False),
        *_common_columns(),
        sa.ForeignKeyConstraint(
            ["patient_id"],
            ["patient_sync_state.patient_id"],
            ondelete="CASCADE",
        ),
        sa.PrimaryKeyConstraint("sample_id"),
    )
    op.create_table(
        "sequencing_sync_state",
        sa.Column("predictive_number", sa.String(), nullable=False),
        sa.Column("sample_id", sa.String(), nullable=False),
        *_common_columns(),
        sa.ForeignKeyConstraint(
            ["sample_id"],
            ["sample_sync_state.sample_id"],
            ondelete="CASCADE",
        ),
        sa.PrimaryKeyConstraint("predictive_number"),
    )
    op.create_table(
        "wsi_sync_state",
        sa.Column("bioptic_number", sa.String(), nullable=False),
        sa.Column("sample_id", sa.String(), nullable=False),
        *_common_columns(),
        sa.ForeignKeyConstraint(
            ["sample_id"],
            ["sample_sync_state.sample_id"],
            ondelete="CASCADE",
        ),
        sa.PrimaryKeyConstraint("bioptic_number"),
    )
    op.create_table(
        "imaging_study_sync_state",
        sa.Column("accession_number", sa.String(), nullable=False),
        sa.Column("patient_id", sa.String(), nullable=False),
        *_common_columns(),
        sa.ForeignKeyConstraint(
            ["patient_id"],
            ["patient_sync_state.patient_id"],
            ondelete="CASCADE",
        ),
        sa.PrimaryKeyConstraint("accession_number"),
    )

    for table in (
        "patient_sync_state",
        "sample_sync_state",
        "sequencing_sync_state",
        "wsi_sync_state",
        "imaging_study_sync_state",
    ):
        op.create_index(op.f(f"ix_{table}_status"), table, ["status"], unique=False)
        op.create_index(op.f(f"ix_{table}_run_id"), table, ["run_id"], unique=False)

    for table, column in (
        ("sample_sync_state", "patient_id"),
        ("sequencing_sync_state", "sample_id"),
        ("wsi_sync_state", "sample_id"),
        ("imaging_study_sync_state", "patient_id"),
    ):
        op.create_index(op.f(f"ix_{table}_{column}"), table, [column], unique=False)


def downgrade() -> None:
    """Downgrade schema."""
    op.drop_table("imaging_study_sync_state")
    op.drop_table("wsi_sync_state")
    op.drop_table("sequencing_sync_state")
    op.drop_table("sample_sync_state")
    op.drop_table("patient_sync_state")

    op.create_table(
        "sync_state",
        sa.Column("id", sa.Integer(), autoincrement=True, nullable=False),
        sa.Column("entity_type", sa.String(), nullable=False),
        sa.Column("entity_key", sa.String(), nullable=False),
        sa.Column("source_fingerprint", sa.String(), nullable=False),
        sa.Column("catalogue_remote_id", sa.String(), nullable=True),
        _status_column(),
        sa.Column("is_deleted", sa.Boolean(), nullable=False),
        sa.Column(
            "last_seen_at",
            sa.DateTime(timezone=True),
            server_default=sa.text("now()"),
            nullable=False,
        ),
        sa.Column("last_synced_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("last_error", sa.Text(), nullable=True),
        sa.Column("run_id", sa.String(), nullable=False),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index(
        op.f("ix_sync_state_entity_key"), "sync_state", ["entity_key"], unique=False
    )
    op.create_index(
        op.f("ix_sync_state_entity_type"),
        "sync_state",
        ["entity_type"],
        unique=False,
    )
    op.create_index(
        op.f("ix_sync_state_run_id"), "sync_state", ["run_id"], unique=False
    )
    op.create_index(
        op.f("ix_sync_state_status"), "sync_state", ["status"], unique=False
    )
