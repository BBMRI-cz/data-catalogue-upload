# syntax=docker/dockerfile:1
# Build from the repository root: docker build -f apps/biobank_api/Dockerfile .
# (the uv workspace lockfile lives at the root and is shared by all members).

FROM ghcr.io/astral-sh/uv:python3.11-bookworm-slim AS builder
ENV UV_COMPILE_BYTECODE=1 \
    UV_LINK_MODE=copy
WORKDIR /app
COPY . /app
# --package biobank_api installs only this service's dependency closure (no dev tools).
RUN --mount=type=cache,target=/root/.cache/uv \
    uv sync --frozen --no-dev --package biobank_api

FROM python:3.11-slim-bookworm
ENV PATH="/app/.venv/bin:$PATH"
WORKDIR /app
COPY --from=builder /app /app
EXPOSE 8001
# Override with `biobank-api-ingest` for the one-shot ingestion job.
CMD ["biobank-api-serve"]
