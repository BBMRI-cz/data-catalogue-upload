from functools import lru_cache
import os

from sqlalchemy import create_engine
from sqlalchemy.engine import Engine
from sqlalchemy.orm import Session, sessionmaker


def require_env(name: str) -> str:
    value = os.getenv(name)
    if not value:
        raise RuntimeError(f"Missing required environment variable: {name}")
    return value


# needs to be a function for migrations to work
def get_database_url() -> str:
    return (
        f"postgresql+psycopg2://"
        f"{require_env('POSTGRES_USER')}:{require_env('POSTGRES_PASSWORD')}"
        f"@localhost:{require_env('POSTGRES_PORT')}"
        f"/{require_env('POSTGRES_DB')}"
    )


@lru_cache(maxsize=1)
def get_engine() -> Engine:
    """Build the engine lazily so importing the package needs no live database."""
    return create_engine(get_database_url(), echo=False)


@lru_cache(maxsize=1)
def get_sessionmaker() -> sessionmaker[Session]:
    return sessionmaker(bind=get_engine())
