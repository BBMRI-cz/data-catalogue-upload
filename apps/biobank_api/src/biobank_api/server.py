from __future__ import annotations

import uvicorn
from dotenv import load_dotenv

from biobank_api.config import get_settings


def main() -> None:
    """HTTP server entrypoint (`biobank-api-serve`)."""
    load_dotenv()
    settings = get_settings()
    uvicorn.run(
        "biobank_api.infrastructure.web.app:app",
        host=settings.host,
        port=settings.port,
    )


if __name__ == "__main__":
    main()
