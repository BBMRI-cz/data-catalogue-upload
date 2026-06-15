from __future__ import annotations

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Runtime configuration, read from environment variables (prefix ``BIOBANK_``).

    Defaults keep the package importable (and testable) without a configured
    environment; real values come from ``.env`` / the deployment environment.
    """

    model_config = SettingsConfigDict(env_prefix="BIOBANK_", extra="ignore")

    # Database the ingestion writes to and the server reads from.
    database_url: str = (
        "postgresql+psycopg2://postgres:postgres@localhost:5432/biobank_api"
    )

    # HTTP server bind address (see uploader's BIOBANK_API_URL, default :8001).
    host: str = "0.0.0.0"
    port: int = 8001

    # Directory holding the biobank XML export(s) the ingestion parses.
    xml_export_path: str = "data/exports"


def get_settings() -> Settings:
    return Settings()
