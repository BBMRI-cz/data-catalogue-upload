---
name: debugging
description: Systematic debugging workflow for the data-catalogue-upload codebase. Use when investigating a bug, traceback, exception, failing test, mypy error, or unexpected sync result (e.g. a non-zero "failed" count, wrong CREATE/UPDATE/DELETE decision, or entities not appearing in the catalogue). Covers reproduce-isolate-hypothesize-fix, layer-aware isolation, where errors hide in this project, and the debugging tools (pytest, breakpoint, logging).
---

# Debugging (data-catalogue-upload)

Debug by evidence, not by guessing. Change one thing at a time, and confirm each assumption before moving on. Resist the urge to patch the first symptom you see — find the cause.

## Method

1. **Reproduce reliably.** Get a deterministic repro before changing anything. The smaller and faster, the better — ideally a single failing `pytest` case. If you can't reproduce it, you can't confirm a fix.
2. **Read the error properly.** Read tracebacks **bottom-up**: the last line is the actual exception; the frames above are the call path. Note the exact type, message, file, and line. Don't skim.
3. **Localize.** Narrow *where* the fault is before asking *why*. Use the layer map below and binary-search the pipeline (fetch → build → plan → execute → persist). Confirm the failing layer with a focused test or a print/breakpoint.
4. **Form one hypothesis.** State a specific, falsifiable cause ("the builder drops `year_of_birth` because the payload key is `birth_year`"). Predict what you'd observe if it's true.
5. **Test the hypothesis.** Add a temporary assertion, log, or breakpoint that proves or kills it. One variable at a time.
6. **Fix the cause.** Address the root cause, respecting layer boundaries (see `python-dev`). Don't silence errors to make symptoms disappear.
7. **Confirm + guard.** Re-run the repro to confirm it's gone, run the full check suite, and add a regression test so it can't come back silently.

## Layer-aware isolation

Dependencies point inward (`infrastructure → application → domain`), so isolate from the inside out — the inner layers are pure and trivial to test.

| Symptom | Most likely layer | First thing to check |
|---------|-------------------|----------------------|
| Wrong/missing field on a domain object | `application/builders/` | The builder's `.get(...)` key matches the raw payload key. |
| Wrong CREATE/UPDATE/SKIP/DELETE decision | `application/sync_planner.py` + `domain` fingerprints | What `compute_fingerprint(...)` hashes, and the prior `SyncStatus`. |
| HTTP error, timeout, bad URL, auth | `infrastructure/api/clients.py` | The request URL/params and the source/catalogue response. |
| DB error, stale state, migration mismatch | `infrastructure/db/` + alembic | ORM model vs. migration; `alembic upgrade head` applied. |
| Crash on startup before any work | `main.py` / env | Missing env var raises `RuntimeError: Missing required environment variable`. |

Because the domain and application layers have no I/O, reproduce most bugs as **pure unit tests with fake ports** (see the `testing` skill) instead of running the whole job against live APIs and a DB.

## Where errors hide in this project

- **Per-entity failures are swallowed on purpose.** `CatalogueSyncService._execute` catches `except Exception`, increments `summary.failed`, sets the entity's `SyncStatus.FAILED`, and stores the message in the state's `last_error` — the run keeps going and **no traceback is printed**. So a non-zero `"failed"` in the JSON summary is a real bug with its detail hidden in the DB.
  - Inspect it: query the relevant `*_sync_state` table for rows where `status = 'failed'` and read the `last_error` column.
  - Reproduce it: drive `_execute` (or `_upsert`) in a test with a fake `CatalogueGateway` that raises, and assert on the resulting state — you'll see the real exception instead of a swallowed one.
- **Silent SKIPs** usually mean the fingerprint didn't change when you expected it to. Check exactly which fields feed `compute_fingerprint(...)` in `sync_planner.py`; a field not included in the fingerprint won't trigger an UPDATE.
- **Unexpected DELETEs** mean an entity is absent from the current source fetch (matched by `predictive_number` / `bioptic_number` / `accession_numbers`). Verify the source payload and the matching key.
- **`alembic` revision errors** point at DB-vs-code drift, not application logic — see the Troubleshooting table in `DEVELOPMENT.md`.

## Tools

```bash
uv run pytest path/to/test.py::test_name -x   # stop at first failure
uv run pytest -x -vv                           # verbose asserts, halt on first fail
uv run pytest --lf                             # re-run only last-failed
uv run pytest -s                               # don't capture stdout (see prints/logs)
uv run pytest --pdb                            # drop into the debugger on failure
```

- **Interactive debugger:** drop `breakpoint()` at the suspect line and run with `uv run pytest -s --pdb` (or run the module). Useful pdb commands: `pp <expr>` (pretty-print), `w` (where/stack), `u`/`d` (move up/down frames), `n`/`s`/`c` (next/step/continue).
- **Temporary tracing:** add `print(...)` or `logging` while narrowing down, but **remove it before committing**. Never leave a bare `except` or a swallowed error as a "fix".
- **Type-level bugs:** `uv run mypy .` often catches the class of bug (wrong Optional handling, mismatched signatures) before runtime — read its output before reaching for the debugger.

## Before you call it fixed

Run the full suite and make sure it passes, then keep the regression test:

```bash
uv run ruff check .
uv run ruff format --check .
uv run mypy .
uv run pytest
```
