# AGENTS.md

Guidance for AI coding agents (primarily Claude Code) working in this repository. `CLAUDE.md` mirrors this file for Claude Code discovery. Project-specific skills live in `.claude/skills/`.

## Project

`data-catalogue-upload` is a **.NET solution** for the data-catalogue sync system. The solution is `DataCatalogueUpload.slnx` at the repo root. The two services:

- **uploader** (`src/Uploader`) - a scheduled, one-shot sync job. For each patient it reads from four source APIs (biobank, radiology, sequencing, WSI), aggregates the data into one FAIR Genomes-shaped patient record, compares it against fingerprints stored in PostgreSQL, and upserts/deletes records in a central data catalogue API.
- **biobank_api** (`src/BiobankApi`) - a source API service that parses biobank XML exports and serves the patient/sample/clinical endpoints the uploader consumes.

Read [`ARCHITECTURE.md`](ARCHITECTURE.md) for the data flow and layering, and [`DEVELOPMENT.md`](DEVELOPMENT.md) for setup and local run instructions.

## Solution layout

- **.NET 10** (`net10.0`), pinned via `global.json`. Nullable + implicit usings on; warnings are errors (NuGet audit advisories `NU190x` are warnings only).
- **One solution, central package management**: `Directory.Build.props` (shared TFM/analyzers) and `Directory.Packages.props` (all NuGet versions). Do not put `Version=` on individual `PackageReference`s.
- **No shared kernel** - each service owns its own DDD base types (small, deliberate duplication; zero cross-service coupling).

```
DataCatalogueUpload.slnx  Directory.Build.props  Directory.Packages.props  global.json
src/
├── BiobankApi/   BiobankApi.{Domain,Application,Infrastructure,Web}
└── Uploader/     Uploader.{Domain,Application,Infrastructure,Host}
tests/
├── BiobankApi.{UnitTests,IntegrationTests}
└── Uploader.{UnitTests,IntegrationTests}
```

Dependency direction per service: `Web`/`Host` -> `Infrastructure` -> `Application` -> `Domain`.

## Architecture (both services)

- **Clean Architecture + DDD.** Domain holds aggregates (`PatientAggregate` in both services), value objects, and invariants. The uploader adds the domain service `ISyncPlanner` (`FingerprintSyncPlanner`); change detection is aggregate behaviour (`ComputeFingerprint()` over `Fingerprint.Of(...)`). The biobank has no domain service - XML text cleaning lives in infrastructure (`XmlValueReader`). Domain has no I/O and no framework dependencies.
- **CQRS via Mediator.** Every use case is a `Command`/`Query` with a handler in `*.Application/Features/...`, dispatched through the free `Mediator` source generator (`ISender`). Handlers return `ErrorOr<T>`.
- **Pipeline behaviors** (`*.Application/Behaviors`): `LoggingBehavior` wraps each request. Input is validated inside the domain (aggregate factories), not by a separate validation behavior.
- **Ports are interfaces** in `*.Application/Abstractions`, implemented in `*.Infrastructure` (EF Core repositories, the biobank XML parser, the uploader's typed `HttpClient` gateways).
- **API style:** ASP.NET Core Minimal API; endpoints only build a Command/Query and call `ISender`, then map `ErrorOr` to HTTP via `ErrorResults`.

## biobank_api

EF Core (Npgsql) + `XmlReader`. Domain `Patient` aggregate with `TissueSample`/`SerumSample`/`GenomeSample` and `DiagnosticSpecimen`; invariants in constructors throw `DomainException`. CQRS: `GetPatientsQuery`, `IngestExportsCommand`. Host `BiobankApi.Web` runs the Minimal API (`serve`, the default) or one-shot ingestion (`-- ingest`). Config via env vars (`POSTGRES_*`, `BIOBANK_*`); see `BiobankOptions`.

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
```

## Conventions

- **Respect layer boundaries.** Domain must not reference Application or Infrastructure. Application depends on Domain and its own port interfaces, never concrete infrastructure. Infrastructure implements the ports.
- **Domain models are `record`s** (value semantics); invariants live in constructors and throw `DomainException`. Mutable sync-state classes are the deliberate exception.
- **New external dependency?** Define an interface in `*.Application/Abstractions` and implement it in `*.Infrastructure`.
- **Handlers return `ErrorOr<T>`**; don't throw for expected failures. Validate input where the data becomes a domain object - the aggregate factory (`Create(...)`) returns an `ErrorOr` validation error - and let the handler/endpoint propagate it via `ErrorResults`.
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

- Do not commit secrets or a real `.env`; configuration comes from environment variables.
- Do not bypass the layers (no EF Core / `HttpClient` / `XmlReader` in `Application` or `Domain`).
- Do not put NuGet versions on individual `PackageReference`s - use central package management.
- Do not loosen analyzer/format settings to silence errors; fix the code instead.
- Do not introduce a shared project between the two services - keep their domains decoupled.
