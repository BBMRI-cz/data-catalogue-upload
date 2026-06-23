---
name: debugging
description: Systematic debugging workflow for the data-catalogue-upload .NET solution. Use when investigating a bug, exception, failing xUnit test, build/analyzer error, or unexpected sync result (e.g. a non-zero "failed" count in the run summary, a wrong CREATE/UPDATE/SKIP/DELETE decision, or entities not appearing in the catalogue). Covers reproduce-isolate-hypothesize-fix, layer-aware isolation, where errors hide in this project, and the .NET debugging tools (dotnet test, the debugger, logging).
---

# Debugging (data-catalogue-upload)

Debug by evidence, not by guessing. Change one thing at a time, and confirm each assumption before moving
on. Resist the urge to patch the first symptom you see - find the cause.

## Method

1. **Reproduce reliably.** Get a deterministic repro before changing anything - ideally a single failing
   xUnit test (`dotnet test --filter "FullyQualifiedName~..."`). If you can't reproduce it, you can't
   confirm a fix.
2. **Read the error properly.** For an exception, read the top frame for the throw site and walk down the
   call path; note the exact exception type, message, and `file:line`. For a build failure, fix the first
   analyzer/compiler error (warnings are errors here) - later errors are often cascades of the first.
3. **Localize.** Narrow *where* the fault is before asking *why*. Use the layer map below and binary-search
   the uploader pipeline (fetch -> build/map -> plan -> execute -> persist). Confirm the failing layer with a
   focused test or a breakpoint.
4. **Form one hypothesis.** State a specific, falsifiable cause ("`ToPatient` drops `birth_year` because the
   DTO property name doesn't match the JSON key"). Predict what you'd observe if it's true.
5. **Test the hypothesis.** Add a temporary assertion, log, or breakpoint that proves or kills it. One
   variable at a time.
6. **Fix the cause.** Address the root cause, respecting layer boundaries (see `dotnet-dev`). Don't silence
   errors to make symptoms disappear, and don't swallow an `ErrorOr` error into a default value.
7. **Confirm + guard.** Re-run the repro, run the full suite, and add a regression test so it can't come back
   silently.

## Layer-aware isolation

Dependencies point inward (`Infrastructure -> Application -> Domain`), so isolate from the inside out - the
inner layers are pure and trivial to test.

| Symptom | Most likely layer | First thing to check |
|---------|-------------------|----------------------|
| Wrong/missing field on a domain object | `Application/Mapping/SourceMapper` (uploader) or `Xml/XmlValueReader` (biobank) | The Mapperly mapping / DTO property name vs the source JSON key; `XmlValueReader` normalization. |
| Validation `ErrorOr` you didn't expect | `Domain` aggregate `Create(...)` | Which invariant fired - the `Error.Validation` code points at the field. |
| Wrong CREATE/UPDATE/SKIP/DELETE decision | `Domain/Services/FingerprintSyncPlanner` + aggregate `ComputeFingerprint()` | What `Fingerprint.Of(...)` serializes, and the prior `SyncStatus`/`IsDeleted`. |
| HTTP error, timeout, bad URL, auth | `Infrastructure/Http/*Gateway` | Request URL/params (from `UploaderOptions`) and the source/catalogue response. |
| DB error, stale state, migration mismatch | `Infrastructure/Persistence/` + EF migrations | EF entity vs migration; whether `dotnet ef database update` (or `RUN_MIGRATIONS`) has been applied. |
| Crash on startup before any work | host `Program.cs` / env | Options binding; a missing connection produces an EF/Npgsql error. |

Because Domain and Application have no I/O, reproduce most bugs as **pure unit tests with hand-written fakes**
(see the `testing` skill) instead of running the whole job against live APIs and a DB.

## Where errors hide in this project

- **Per-entity upsert failures are recorded, not thrown.** In `RunCatalogueSyncCommandHandler.ExecuteAsync`,
  when the catalogue gateway returns an `ErrorOr` error the run **continues**: it increments `result.Failed`,
  sets the entity's `SyncStatus.Failed`, stores the message in `state.LastError`, and persists it. So a
  non-zero `"failed"` in the JSON summary is a real bug whose detail is in the DB, not in stdout.
  - Inspect it: query the relevant `*_sync_state` table for rows where `status = 'failed'` and read
    `last_error`.
  - Reproduce it: drive the handler in a test with a `FakeCatalogueGateway` whose `FailUpsertTypes` includes
    the entity type, then assert on the resulting state - you'll see the real failure instead of a swallowed
    one.
- **Invalid/unparseable source patients are logged and skipped.** A `JsonException` or an `ErrorOr` validation
  error from mapping increments `result.Failed` and emits a `LogWarning` (with the patient id) - check the
  logs, not just the summary.
- **Silent SKIPs** mean the fingerprint didn't change when you expected it to. Check exactly which fields the
  aggregate feeds into `Fingerprint.Of(...)` inside its `ComputeFingerprint()`; a field not included won't
  trigger an UPDATE.
- **Unexpected DELETEs** mean an entity is absent from the current source fetch (matched by
  `predictive_number` / `bioptic_number` / `accession_numbers`). Verify the source payload and the match key
  before assuming the planner is wrong.
- **EF migration errors** point at DB-vs-model drift, not application logic - regenerate/apply the migration
  (see `DEVELOPMENT.md`) rather than editing the model to match a stale schema.

## Tools

```bash
dotnet test --filter "FullyQualifiedName~FingerprintSyncPlannerTests"   # one class
dotnet test --filter "FullyQualifiedName~SkipsUnchangedPatient"         # one test
dotnet test tests/Uploader.UnitTests/Uploader.UnitTests.csproj          # one project
dotnet test -l "console;verbosity=detailed"                             # full assert/log output
dotnet build DataCatalogueUpload.slnx -c Release                        # surface analyzer/compile errors
```

- **Interactive debugger:** set a breakpoint in your IDE (VS / VS Code / Rider) and debug the failing test,
  or drop `System.Diagnostics.Debugger.Break()` at the suspect line. Inspect `ErrorOr` values (`IsError`,
  `Errors[0].Code`/`.Description`) and the `*SyncState` fields.
- **Temporary tracing:** add a `logger.Log...` or `Console.WriteLine` while narrowing down, but **remove it
  before committing**. Never leave a swallowed `catch` or a discarded `ErrorOr` as a "fix".
- **Compile-time bugs:** `dotnet build -c Release` (warnings-as-errors) catches a whole class of nullability
  and signature bugs before runtime - read its output before reaching for the debugger.

## Before you call it fixed

Run the checks CI runs and keep the regression test:

```bash
dotnet format DataCatalogueUpload.slnx --verify-no-changes
dotnet build DataCatalogueUpload.slnx -c Release
dotnet test DataCatalogueUpload.slnx
```
