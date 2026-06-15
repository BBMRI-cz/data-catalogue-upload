# syntax=docker/dockerfile:1
# Build from the repository root: docker build -f apps/uploader/Dockerfile .
# (the uv workspace lockfile lives at the root and is shared by all members).
#
# Note: the uploader currently builds its DB URL against `localhost` (see
# infrastructure/db/session.py), so this image expects a host-reachable Postgres
# (e.g. `--network host`). Making the host configurable is a follow-up.

FROM ghcr.io/astral-sh/uv:python3.11-bookworm-slim AS builder
ENV UV_COMPILE_BYTECODE=1 \
    UV_LINK_MODE=copy
WORKDIR /app
COPY . /app
RUN --mount=type=cache,target=/root/.cache/uv \
    uv sync --frozen --no-dev --package uploader

FROM python:3.11-slim-bookworm
ENV PATH="/app/.venv/bin:$PATH"
WORKDIR /app
COPY --from=builder /app /app
CMD ["uploader"]
