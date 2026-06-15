from __future__ import annotations

from collections.abc import Iterator

from biobank_api.application.get_patients import GetPatients
from biobank_api.infrastructure.db.repositories import SqlBiobankRepository
from biobank_api.infrastructure.db.session import get_sessionmaker


def get_patients_use_case() -> Iterator[GetPatients]:
    """Provide a ``GetPatients`` use case bound to a request-scoped DB session.

    Overridden in tests via ``app.dependency_overrides`` to avoid a live database.
    """
    session = get_sessionmaker()()
    try:
        yield GetPatients(SqlBiobankRepository(session))
    finally:
        session.close()
