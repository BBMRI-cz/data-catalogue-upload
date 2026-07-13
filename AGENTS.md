# AGENTS.md

Guidance for AI coding agents (primarily Claude Code) working in this repository. `CLAUDE.md` mirrors this file for Claude Code discovery. Project-specific skills live in `.claude/skills/`.

## Project

`data-catalogue-upload` is a **.NET solution** for the data-catalogue sync system. The solution is `DataCatalogueUpload.slnx` at the repo root. The services:

- **uploader** (`src/Uploader`) - a scheduled, one-shot sync job. For each patient it reads from four source APIs (biobank, radiology, sequencing, WSI), aggregates the data into one FAIR Genomes-shaped patient record, compares it against fingerprints stored in PostgreSQL, and upserts/deletes records in a central data catalogue API.
- **biobank_api** (`src/BiobankApi`) - a source API service that parses biobank XML exports and serves the patient/sample/clinical endpoints the uploader consumes.
- **sequencing_api** (`src/SequencingApi`) - source API service for sequencing data. Currently a **scaffold** (host + stub ingestion; no domain aggregate or EF migration yet - those land with #30). Same layering and in-process Quartz ingestion as biobank_api.

Read [`ARCHITECTURE.md`](ARCHITECTURE.md) for the data flow and layering, and [`DEVELOPMENT.md`](DEVELOPMENT.md) for setup and local run instructions.

## Solution layout

- **.NET 10** (`net10.0`), pinned via `global.json`. Nullable + implicit usings on; warnings are errors (NuGet audit advisories `NU190x` are warnings only).
- **One solution, central package management**: `Directory.Build.props` (shared TFM/analyzers) and `Directory.Packages.props` (all NuGet versions). Do not put `Version=` on individual `PackageReference`s.
- **No shared kernel** - each service owns its own DDD base types (small, deliberate duplication; zero cross-service coupling).

```
DataCatalogueUpload.slnx  Directory.Build.props  Directory.Packages.props  global.json
src/
├── BiobankApi/     BiobankApi.{Domain,Application,Infrastructure,Web}
├── SequencingApi/  SequencingApi.{Domain,Application,Infrastructure,Web}
└── Uploader/       Uploader.{Domain,Application,Infrastructure,Host}
tests/
├── BiobankApi.{UnitTests,IntegrationTests}
├── SequencingApi.{UnitTests,IntegrationTests}
└── Uploader.{UnitTests,IntegrationTests}
```

Dependency direction per service: `Web`/`Host` -> `Infrastructure` -> `Application` -> `Domain`.

## Architecture (both services)

- **Clean Architecture + DDD.** Domain holds aggregates (`PatientAggregate` in both services), value objects, and invariants. The uploader adds the domain service `ISyncPlanner` (`FingerprintSyncPlanner`); change detection is aggregate behaviour (`ComputeFingerprint()` over `Fingerprint.Of(...)`). The biobank has no domain service - XML text cleaning lives in infrastructure (`XmlValueReader`). Domain has no I/O and no framework dependencies.
- **CQRS via Mediator.** Every use case is a `Command`/`Query` with a handler in `*.Application/Features/...`, dispatched through the free `Mediator` source generator (`ISender`). Handlers return `ErrorOr<T>`. A non-trivial result type is named after its request - `<Command>Result` (e.g. `RunCatalogueSyncCommandResult`, `IngestExportsCommandResult`) - in the same `Features/` folder.
- **Pipeline behaviors** (`*.Application/Behaviors`): `LoggingBehavior` wraps each request; `ValidationBehavior` runs any FluentValidation `AbstractValidator<TCommand>` (auto-registered via `AddValidatorsFromAssembly`) and short-circuits to an `ErrorOr` validation error before the handler. Application-level request validation lives here; domain invariants stay in the aggregate `Create(...)` factories. (Current commands are parameterless, so no validators exist yet.)
- **Ports are interfaces** in `*.Application/Abstractions`, implemented in `*.Infrastructure` (EF Core repositories, the biobank XML parser, the uploader's typed `HttpClient` gateways).
- **API style:** ASP.NET Core Minimal API; endpoints only build a Command/Query and call `ISender`, then map `ErrorOr` to HTTP via `ErrorResults`.

## biobank_api

EF Core (Npgsql) + LINQ-to-XML (`System.Xml.Linq`). Domain `PatientAggregate` with `TissueSample`/`SerumSample`/`GenomeSample` and `DiagnosticSpecimen`; invariants enforced by `Create(...)` factories returning `ErrorOr`. Ingestion reads exports behind the `IPatientExportSource` port (`XmlExportParser` discovers files, the pure `XmlPatientReader` maps each `<patient>`); there is exactly one source per biobank, and the handler reads it and reports invalid records in an `IngestExportsCommandResult`. The Application ports live in subfolders: `Abstractions/Export/` (`IPatientExportSource`, `ExportParseResult`, `ExportParseError`) and `Abstractions/Repositories/` (`IBiobankRepository`). CQRS: `GetPatientsQuery`, `IngestExportsCommand`. Host `BiobankApi.Web` runs the Minimal API and a **weekly Quartz job** (`IngestionJob`) that dispatches `IngestExportsCommand`; ingestion can also be triggered on demand via `POST /admin/ingest`. Config via env vars (`POSTGRES_*`, `BIOBANK_*` incl. `BIOBANK_INGEST_CRON` for the schedule); see `BiobankOptions`.

## sequencing_api

**Scaffold** mirroring biobank_api (host + stub ingestion; the sequencing domain, repository and first EF migration land with #30). Domain has only the `Common/` base types. Ingestion reads the `ISequencingDataSource` port (`Abstractions/DataSource/`); the current `StubSequencingDataSource` reports zero records so `IngestRecordsCommand` runs end-to-end. Host `SequencingApi.Web` runs the Minimal API (`GET /health`, `POST /admin/ingest`) and a **weekly Quartz job** (`IngestionJob`). Config via env vars (`POSTGRES_*`, `SEQUENCING_*` incl. `SEQUENCING_INGEST_CRON`); see `SequencingOptions`. Search for `ponytail:` to find the spots to fill in.

## uploader

EF Core (Npgsql) + typed `HttpClient`s. Domain `PatientAggregate` + `Sample`/sequencing/WSI/radiology value objects and the `*SyncState` types; `FingerprintSyncPlanner` decides CREATE/UPDATE/SKIP/DELETE. CQRS: `RunCatalogueSyncCommand`. Host `Uploader.Host` applies migrations, runs the sync, prints a JSON summary, and exits `0` (no failures) or `1`. Config via env vars (`POSTGRES_*` + the five `*_API_URL`s); see `UploaderOptions`.

## Commands

Run from the repo root.

```bash
dotnet restore DataCatalogueUpload.slnx
dotnet build DataCatalogueUpload.slnx -c Release          # warnings are errors
dotnet test DataCatalogueUpload.slnx                      # all unit + integration tests
dotnet format DataCatalogueUpload.slnx --verify-no-changes   # lint/format (drop the flag to apply)

# EF Core migrations
dotnet ef migrations add <Name> --project src/BiobankApi/BiobankApi.Infrastructure --startup-project src/BiobankApi/BiobankApi.Web -o Persistence/Migrations
dotnet ef migrations add <Name> --project src/Uploader/Uploader.Infrastructure   --startup-project src/Uploader/Uploader.Host    -o Persistence/Migrations
# sequencing_api has no entities yet (#30); once it does, the same command with SequencingApi paths applies.
```

## Conventions

- **Respect layer boundaries.** Domain must not reference Application or Infrastructure. Application depends on Domain and its own port interfaces, never concrete infrastructure. Infrastructure implements the ports.
- **Domain models are `record`s** (value semantics); invariants live in `Create(...)` factories returning `ErrorOr` (throw `InvalidOperationException` only for unreachable/programmer-error guards). Mutable sync-state classes are the deliberate exception.
- **No primary constructors on classes or structs.** Use an explicit constructor that assigns `private readonly` fields (`_camelCase`). Positional **records** for DTOs/value objects/typed ids are fine - that idiom stays. (Enforced by `dotnet_diagnostic.IDE0290.severity = none` in `.editorconfig`.)
- **New external dependency?** Define an interface in `*.Application/Abstractions` and implement it in `*.Infrastructure`.
- **Handlers return `ErrorOr<T>`**; don't throw for expected failures. Validate domain invariants where the data becomes a domain object - the aggregate factory (`Create(...)`) returns an `ErrorOr` validation error; validate request shape/options at the application boundary with a FluentValidation `AbstractValidator<TCommand>` (the `ValidationBehavior` runs it) - and let the handler/endpoint propagate failures via `ErrorResults`.
- **Tests split** into `*.UnitTests` (pure) and `*.IntegrationTests` (EF Core against in-memory SQLite via the `SqliteDatabase` helper; biobank API via `WebApplicationFactory`). Use xUnit; no FluentAssertions.
- **Add packages via central management:** `dotnet add <project> package <name>` updates `Directory.Packages.props`. Do not hand-edit versions onto a `PackageReference`.

## Before finishing any change

From the repo root, run and ensure they pass (these mirror CI in [`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml)):

```bash
dotnet format DataCatalogueUpload.slnx --verify-no-changes
dotnet build DataCatalogueUpload.slnx -c Release
dotnet test DataCatalogueUpload.slnx
```

## What to avoid

- **Do not `git commit`, `git push`, or create branches/PRs without the user's explicit permission.** Make changes in the working tree and let the user review and commit.
- Do not commit secrets or a real `.env`; configuration comes from environment variables.
- Do not bypass the layers (no EF Core / `HttpClient` / `XmlReader` in `Application` or `Domain`).
- Do not put NuGet versions on individual `PackageReference`s - use central package management.
- Do not loosen analyzer/format settings to silence errors; fix the code instead.
- Do not introduce a shared project between the services - keep their domains decoupled.
