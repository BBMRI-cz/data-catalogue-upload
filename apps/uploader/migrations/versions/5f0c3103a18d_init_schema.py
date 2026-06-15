"""init schema

Revision ID: 5f0c3103a18d
Revises:
Create Date: 2026-04-22 17:43:43.435976

"""

from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = "5f0c3103a18d"
down_revision: Union[str, Sequence[str], None] = None
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Upgrade schema."""
    op.create_table(
        "sync_run",
        sa.Column("id", sa.String(), nullable=False),
        sa.Column(
            "started_at",
            sa.DateTime(timezone=True),
            server_default=sa.text("now()"),
            nullable=False,
        ),
        sa.Column("finished_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("scanned_count", sa.Integer(), nullable=False),
        sa.Column("changed_count", sa.Integer(), nullable=False),
        sa.Column("uploaded_count", sa.Integer(), nullable=False),
        sa.Column("deleted_count", sa.Integer(), nullable=False),
        sa.Column("skipped_count", sa.Integer(), nullable=False),
        sa.Column("failed_count", sa.Integer(), nullable=False),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_table(
        "sync_state",
        sa.Column("id", sa.Integer(), autoincrement=True, nullable=False),
        sa.Column("entity_type", sa.String(), nullable=False),
        sa.Column("entity_key", sa.String(), nullable=False),
        sa.Column("source_fingerprint", sa.String(), nullable=False),
        sa.Column("catalogue_remote_id", sa.String(), nullable=True),
        sa.Column(
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
        ),
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
        op.f("ix_sync_state_entity_key"),
        "sync_state",
        ["entity_key"],
        unique=False,
    )
    op.create_index(
        op.f("ix_sync_state_entity_type"),
        "sync_state",
        ["entity_type"],
        unique=False,
    )
    op.create_index(
        op.f("ix_sync_state_run_id"),
        "sync_state",
        ["run_id"],
        unique=False,
    )
    op.create_index(
        op.f("ix_sync_state_status"),
        "sync_state",
        ["status"],
        unique=False,
    )


def downgrade() -> None:
    """Downgrade schema."""
    op.drop_index(op.f("ix_sync_state_status"), table_name="sync_state")
    op.drop_index(op.f("ix_sync_state_run_id"), table_name="sync_state")
    op.drop_index(op.f("ix_sync_state_entity_type"), table_name="sync_state")
    op.drop_index(op.f("ix_sync_state_entity_key"), table_name="sync_state")
    op.drop_table("sync_state")
    op.drop_table("sync_run")
