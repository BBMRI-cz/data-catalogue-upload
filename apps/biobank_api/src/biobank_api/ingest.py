from __future__ import annotations

from dotenv import load_dotenv

from biobank_api.application.ingest_exports import IngestExports
from biobank_api.config import get_settings
from biobank_api.infrastructure.db.models import Base
from biobank_api.infrastructure.db.repositories import SqlBiobankRepository
from biobank_api.infrastructure.db.session import get_engine, get_sessionmaker
from biobank_api.infrastructure.xml.parser import XmlExportParser


def main() -> int:
    """Ingestion / scheduler entrypoint (`biobank-api-ingest`).

    One-shot: parse the biobank XML exports and persist them. Intended to be run
    on a schedule (cron / CronJob), mirroring the uploader sync job.
    """
    load_dotenv()
    settings = get_settings()

    # Dev convenience: create tables when migrations haven't been applied yet.
    Base.metadata.create_all(bind=get_engine())

    with get_sessionmaker()() as session:
        ingest = IngestExports(
            source=XmlExportParser(settings.xml_export_path),
            repository=SqlBiobankRepository(session),
        )
        count = ingest()

    print(f"ingested {count} patients")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
