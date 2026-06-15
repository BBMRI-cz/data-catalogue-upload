from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker

import os


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


engine = create_engine(get_database_url(), echo=False)

SessionLocal = sessionmaker(bind=engine)
