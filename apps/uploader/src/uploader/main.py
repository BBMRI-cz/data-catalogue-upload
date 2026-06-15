from __future__ import annotations

from pathlib import Path

from dotenv import load_dotenv

# Load this app's .env before importing infrastructure (the db session builds the
# engine at import). main.py is at apps/uploader/src/uploader/main.py; the app root
# (where .env lives) is 2 levels up.
load_dotenv(Path(__file__).resolve().parents[2] / ".env")

import json
import os

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
