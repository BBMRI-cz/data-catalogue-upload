# uploader

Scheduled, one-shot sync job. For each patient it reads from the source APIs (biobank, radiology,
sequencing, WSI), aggregates the data into one FAIR Genomes-shaped record, compares it against
fingerprints stored in PostgreSQL, and upserts/deletes records in the central data catalogue API.

This package is a member of the repository's uv workspace. See the repository root
[`DEVELOPMENT.md`](../../DEVELOPMENT.md) for workspace-wide setup and [`ARCHITECTURE.md`](../../ARCHITECTURE.md)
for the data flow and layering.

## Run

From the repository root:

```bash
uv sync --all-packages --group dev                    # install the whole workspace
cp apps/uploader/.env.example apps/uploader/.env       # this app's own .env
docker compose -f compose.prod.yml up -d uploader-db   # this app's database
cd apps/uploader && uv run alembic -c alembic.ini upgrade head   # apply migrations
uv run --package uploader uploader                     # run the sync (needs a complete .env)
```

The job prints a JSON run summary (scanned / changed / uploaded / deleted / skipped / failed) and exits
`0` on success, `1` if any entity failed.

## Layout

```
src/uploader/
├── domain/          # pure dataclass models + compute_fingerprint (no I/O)
├── application/     # use cases, builders (dict -> domain), interfaces/ports.py (Protocols)
├── infrastructure/  # adapters: api/clients.py (HTTP gateways), db/ (ORM + repositories)
└── main.py          # composition root (wires everything from environment variables)
migrations/          # Alembic environment and versioned migrations
```
