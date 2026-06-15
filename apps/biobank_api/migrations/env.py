from pathlib import Path

from dotenv import load_dotenv

# Load `.env` before building the engine. env.py lives at
# apps/biobank_api/migrations/env.py; the workspace root (where `.env` lives) is 4 levels up.
_project_root = Path(__file__).resolve().parents[3]
load_dotenv(_project_root / ".env")
load_dotenv()

from logging.config import fileConfig

from alembic import context

from biobank_api.config import get_settings
from biobank_api.infrastructure.db.models import Base
from biobank_api.infrastructure.db.session import get_engine

config = context.config

if config.config_file_name is not None:
    fileConfig(config.config_file_name)

target_metadata = Base.metadata


def run_migrations_offline() -> None:
    context.configure(
        url=get_settings().database_url,
        target_metadata=target_metadata,
        literal_binds=True,
        dialect_opts={"paramstyle": "named"},
    )
    with context.begin_transaction():
        context.run_migrations()


def run_migrations_online() -> None:
    connectable = get_engine()
    with connectable.connect() as connection:
        context.configure(connection=connection, target_metadata=target_metadata)
        with context.begin_transaction():
            context.run_migrations()


if context.is_offline_mode():
    run_migrations_offline()
else:
    run_migrations_online()
