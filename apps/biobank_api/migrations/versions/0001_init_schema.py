"""init biobank_api schema

Revision ID: 0001_init
Revises:
Create Date: 2026-06-15

"""

from __future__ import annotations

import sqlalchemy as sa
from alembic import op

revision = "0001_init"
down_revision = None
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.create_table(
        "patient",
        sa.Column("external_id", sa.String(), primary_key=True),
        sa.Column("payload", sa.JSON(), nullable=False),
    )


def downgrade() -> None:
    op.drop_table("patient")
