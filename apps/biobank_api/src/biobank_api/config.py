from __future__ import annotations

from pathlib import Path

from pydantic_settings import BaseSettings, SettingsConfigDict

# apps/biobank_api (this service's root, where its .env lives).
_APP_ROOT = Path(__file__).resolve().parents[2]


class Settings(BaseSettings):
    """Runtime configuration for the biobank API.

    Read from environment variables, falling back to this service's own
    ``apps/biobank_api/.env`` file. Defaults keep the package importable (and
    testable) without a configured environment.
    """

    model_config = SettingsConfigDict(env_file=_APP_ROOT / ".env", extra="ignore")

    # PostgreSQL (the names the biobank-db container also uses). Inside compose the
    # service overrides POSTGRES_HOST/POSTGRES_PORT to reach the biobank-db container.
    postgres_user: str = "postgres"
    postgres_password: str = "postgres"
    postgres_db: str = "biobank_api"
    postgres_host: str = "localhost"
    postgres_port: int = 5433

    # HTTP server bind address.
    biobank_host: str = "0.0.0.0"
    biobank_port: int = 8001

    # Directory holding the biobank XML export(s) the ingestion parses.
    biobank_xml_export_path: str = "data/exports"

    @property
    def database_url(self) -> str:
        return (
            f"postgresql+psycopg2://{self.postgres_user}:{self.postgres_password}"
            f"@{self.postgres_host}:{self.postgres_port}/{self.postgres_db}"
        )


def get_settings() -> Settings:
    return Settings()
