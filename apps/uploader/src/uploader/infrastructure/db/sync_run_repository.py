from datetime import datetime, timezone

from sqlalchemy.orm import Session

from uploader.application.sync_service import RunSummary
from uploader.infrastructure.db.models import SyncRunORM


class SyncRunRepository:
    def __init__(self, session: Session):
        self.session = session

    def start(self, run_id: str) -> None:
        self.session.add(SyncRunORM(id=run_id))
        self.session.commit()

    def finish(self, summary: RunSummary) -> None:
        run = self.session.get(SyncRunORM, summary.run_id)
        if run is None:
            run = SyncRunORM(id=summary.run_id)
            self.session.add(run)
        run.finished_at = datetime.now(timezone.utc)
        run.scanned_count = summary.scanned
        run.changed_count = summary.changed
        run.uploaded_count = summary.uploaded
        run.deleted_count = summary.deleted
        run.skipped_count = summary.skipped
        run.failed_count = summary.failed
        self.session.commit()
