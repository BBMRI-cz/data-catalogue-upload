from __future__ import annotations

from functools import lru_cache

from sqlalchemy import create_engine
from sqlalchemy.engine import Engine
from sqlalchemy.orm import Session, sessionmaker

from biobank_api.config import get_settings


@lru_cache(maxsize=1)
def get_engine() -> Engine:
    """Build the engine lazily so importing the package needs no live database."""
    return create_engine(get_settings().database_url, echo=False)


@lru_cache(maxsize=1)
def get_sessionmaker() -> sessionmaker[Session]:
    return sessionmaker(bind=get_engine())
