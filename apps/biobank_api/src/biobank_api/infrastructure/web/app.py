from __future__ import annotations

from fastapi import FastAPI

from biobank_api.infrastructure.web.routers import health, patients


def create_app() -> FastAPI:
    app = FastAPI(title="Biobank API", version="0.1.0")
    app.include_router(health.router)
    app.include_router(patients.router)
    return app


# Module-level app for `uvicorn biobank_api.infrastructure.web.app:app`.
app = create_app()
