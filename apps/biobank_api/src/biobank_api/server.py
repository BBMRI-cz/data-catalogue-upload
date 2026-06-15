from __future__ import annotations

import uvicorn

from biobank_api.config import get_settings


def main() -> None:
    """HTTP server entrypoint (`biobank-api-serve`)."""
    settings = get_settings()
    uvicorn.run(
        "biobank_api.infrastructure.web.app:app",
        host=settings.biobank_host,
        port=settings.biobank_port,
    )


if __name__ == "__main__":
    main()
