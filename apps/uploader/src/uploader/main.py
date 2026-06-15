from __future__ import annotations

import json
import os
from dotenv import load_dotenv

from uploader.application import CatalogueSyncService, FingerprintSyncPlanner
from uploader.infrastructure import (
    Base,
    SessionLocal,
    SyncRunRepository,
    SyncStateRepository,
    build_catalogue_gateway_from_env,
    build_source_gateway_from_env,
    engine,
)


def _require_env(name: str) -> str:
    value = os.getenv(name)
    if not value:
        raise RuntimeError(f"Missing required environment variable: {name}")
    return value


def main() -> int:
    load_dotenv()
    _require_env("BIOBANK_API_URL")
    _require_env("RADIOLOGY_API_URL")
    _require_env("SEQUENCING_API_URL")
    _require_env("WSI_API_URL")
    _require_env("CATALOGUE_API_URL")

    Base.metadata.create_all(bind=engine)
    with SessionLocal() as session:
        service = CatalogueSyncService(
            source_gateway=build_source_gateway_from_env(),
            catalogue_gateway=build_catalogue_gateway_from_env(),
            state_repository=SyncStateRepository(session),
            planner=FingerprintSyncPlanner(),
        )

        summary = service.run_catalogue_sync()
        SyncRunRepository(session).finish(summary)

    print(json.dumps(summary.__dict__, indent=2, sort_keys=True))
    return 0 if summary.failed == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
