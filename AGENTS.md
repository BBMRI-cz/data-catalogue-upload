# AGENTS.md

Guidance for AI coding agents (primarily Claude Code) working in this repository. `CLAUDE.md` mirrors this file for Claude Code discovery. Project-specific skills live in `.claude/skills/`.

## Project

`data-catalogue-upload` is a **.NET solution** for the data-catalogue sync system. The solution is `DataCatalogueUpload.slnx` at the repo root. The services:

- **uploader** (`src/Uploader`) - a scheduled, one-shot sync job. For each patient it reads from four source APIs (biobank, radiology, sequencing, WSI), aggregates the data into one FAIR Genomes-shaped patient record, compares it against fingerprints stored in PostgreSQL, and upserts/deletes records in a central data catalogue API.
- **biobank_api** (`src/BiobankApi`) - a source API service that parses biobank XML exports and serves the patient/sample/clinical endpoints the uploader consumes.
- **sequencing_api** (`src/SequencingApi`) - source API service for sequencing data. Domain (#54), persistence (#55) and MMCI ingestion (#56/#57) have landed. Same layering and in-process Quartz ingestion as biobank_api.

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

- **Clean Architecture + DDD.** Domain holds aggregates (`PatientAggregate` in biobank_api and the uploader; `SampleAggregate`/`SequencingRunAggregate`/`PanelAggregate` in sequencing_api), value objects, and invariants. The uploader adds the domain service `ISyncPlanner` (`FingerprintSyncPlanner`); change detection is aggregate behaviour (`ComputeFingerprint()` over `Fingerprint.Of(...)`). The biobank has no domain service - XML text cleaning lives in infrastructure (`XmlValueReader`). Domain has no I/O and no framework dependencies.
- **CQRS via Mediator.** Every use case is a `Command`/`Query` with a handler in `*.Application/Features/...`, dispatched through the free `Mediator` source generator (`ISender`). Handlers return `ErrorOr<T>`. A non-trivial result type is named after its request - `<Command>Result` (e.g. `RunCatalogueSyncCommandResult`, `IngestExportsCommandResult`) - in the same `Features/` folder.
- **Pipeline behaviors** (`*.Application/Behaviors`): `LoggingBehavior` wraps each request; `ValidationBehavior` runs any FluentValidation `AbstractValidator<TCommand>` (auto-registered via `AddValidatorsFromAssembly`) and short-circuits to an `ErrorOr` validation error before the handler. Application-level request validation lives here; domain invariants stay in the aggregate `Create(...)` factories. (Current commands are parameterless, so no validators exist yet.)
- **Ports are interfaces** in `*.Application/Abstractions`, implemented in `*.Infrastructure` (EF Core repositories, the biobank XML parser, the uploader's typed `HttpClient` gateways).
- **One aggregate root = one repository, named after the root** - not after the service: port `I<Root>Repository`, EF implementation `Sql<Root>Repository`, test fake `Fake<Root>Repository`. A port that projects *across* aggregates into flat DTOs is a **`I<Thing>Reader`**, never a `Repository` - a repository returns aggregates through the mappers, a reader projects in SQL and never round-trips the domain. Saves are idempotent delete-then-insert on the natural id and report per-record failures instead of aborting; aggregate roots reference each other **by id, with an index but no foreign key**, so ingest order never becomes load-bearing.
- **Don't flatten value objects into their owner's row.** A 0..1 value object gets its own table keyed on the owner's primary key - that is what makes it one-to-one, and absence becomes an absent row instead of a nullable column group needing a marker to tell "not recorded" from "recorded, all fields unknown". An ordered child collection gets rows with an explicit position column. JSON columns are only for **scalar** lists (`patient.AccessionNumbers`, `panel.Genes`).
- **API style:** ASP.NET Core Minimal API; endpoints only build a Command/Query and call `ISender`, then map `ErrorOr` to HTTP via `ErrorResults`.

## biobank_api

EF Core (Npgsql) + LINQ-to-XML (`System.Xml.Linq`). Domain `PatientAggregate` with `TissueSample`/`SerumSample`/`GenomeSample` and `DiagnosticSpecimen`; invariants enforced by `Create(...)` factories returning `ErrorOr`. Ingestion reads exports behind the `IPatientExportSource` port (`XmlExportParser` discovers files, the pure `XmlPatientReader` maps each `<patient>`); there is exactly one source per biobank, and the handler reads it and reports invalid records in an `IngestExportsCommandResult`. The Application ports live in subfolders: `Abstractions/Export/` (`IPatientExportSource`, `ExportParseResult`, `ExportParseError`) and `Abstractions/Repositories/` (`IPatientRepository`, implemented by `SqlPatientRepository`). CQRS: `GetPatientsQuery`, `IngestExportsCommand`. Host `BiobankApi.Web` runs the Minimal API and a **weekly Quartz job** (`IngestionJob`) that dispatches `IngestExportsCommand`; ingestion can also be triggered on demand via `POST /admin/ingest`. Config via env vars (`POSTGRES_*`, `BIOBANK_*` incl. `BIOBANK_INGEST_CRON` for the schedule); see `BiobankOptions`.

## sequencing_api

Mirrors biobank_api (host + ingestion handler persisting through the repositories). Domain is a biobank-agnostic sequencing model derived from [`docs/sequencing-data-report.md`](docs/sequencing-data-report.md) §4, with **three aggregate roots**: `SampleAggregate` (`Samples/`, keyed by an opaque `external_id` + `IdScheme`, owning `RunSample` -> `LibraryPreparation`/`SequencingFile`/`Analysis`), `SequencingRunAggregate` (`Runs/`, + `ReadDefinition`) and `PanelAggregate` (`Panels/`). Runs and panels are shared by many samples, so they are referenced by id, never embedded. `QualityMetrics` and the enums (`SampleType`, `FileRole`, `AnalysisType`) sit at the Domain root. **Individual variant records are deliberately not modelled** - the catalogue consumes none of them; analyses reference their calls as files (`FileRole.Vcf`/`VariantReport`). **`QualityMetrics` holds only the two numbers the catalogue consumes** (`MedianReadDepth`, `ObservedReadLength`) - FAIR Genomes names six quality fields and MMCI's sources state only these two; the same rule that keeps variants out keeps the rest out. **It attaches to `Analysis` only** - both are computed by the pipeline, so the run carries a plain `PercentageQ30` instead and `RunSample` carries no quality at all. Invariants **and value cleaning** live in the `Create(...)` factories via the internal `Common/Normalize` helper (trim/collapse/case-fold/symbol lists); source decoding - text encoding, decimal commas, panel alias matching - stays in Infrastructure. Ingestion reads the `ISequencingDataSource` port (`Abstractions/DataSource/`), implemented per facility under **`Infrastructure/DataSource/<Facility>/` - one directory per source adapter** (`DataSource/Mmci/` is the first): the tree walk plus readers for the run metadata XML, sample sheet, sample folders, NextGENe statistics, the versioned libraries table and the pseudonymizer's `predictive.json`. That mapping file is the only route from the pseudonymized predictive number (the sample folder name, which becomes `SampleAggregate.Id`) to the real one (`SampleAggregate.PredictiveNumber`, the key biobank_api stores as `predictive_number` - the report calls this field `subject_ref`, but it is a predictive number and is named one); the `patient.json`/`samples.json` beside it are never opened. **[`docs/mmci-ingestion-map.md`](docs/mmci-ingestion-map.md) maps every domain field to the exact source file, key and reader that fills it - read it before changing a reader or chasing an empty field.** Missing libraries or mapping tables are reported, not fatal - they cost panels and predictive numbers respectively, not the sequencing data. **Persistence** is nine tables - `sample`, `run_sample`, `library_preparation`, `analysis`, `quality_metrics`, `sequencing_file`, `sequencing_run`, `run_read`, `panel` - behind one repository per aggregate root (`ISampleRepository`, `ISequencingRunRepository`, `IPanelRepository`) plus the cross-aggregate `ISequencingStatsReader`. **Value objects are never flattened into their owner's row:** the 0..1 `LibraryPreparation` and `QualityMetrics` each own a table keyed on the owner's primary key (which is what makes them one-to-one and lets an absent value object simply be an absent row), and a run's ordered read structure is `run_read` rows with an explicit `Position`, not a JSON blob. Only `panel.Genes` is a JSON column, because it is a scalar string list - the same treatment `patient.AccessionNumbers` gets. Sequencing and analysis outputs do share the `sequencing_file` table, told apart by which owner FK is set, with a check constraint enforcing exactly one. Host `SequencingApi.Web` runs the Minimal API (`GET /health`, `GET /sequencing?predictive_number=`, `GET /summary`, `POST /admin/ingest`) and a **weekly Quartz job** (`IngestionJob`). `GET /sequencing` takes the **real** predictive number, serves this service's own model (the uploader maps it to FAIR on its side, as with biobank_api), and answers an unknown number with `200` + empty `samples` rather than `404` - the uploader's gateway calls `EnsureSuccessStatusCode`, so a 404 would throw there; a *missing* parameter is a `400`, via the `GetSequencingQueryValidator`. Config via env vars (`POSTGRES_*`, `SEQUENCING_*` incl. `SEQUENCING_INGEST_CRON`); see `SequencingOptions`. Search for `ponytail:` to find the remaining spots to fill in.

## uploader

EF Core (Npgsql) + typed `HttpClient`s. Domain `PatientAggregate` + `Sample`/sequencing/WSI/radiology value objects and the `*SyncState` types; `FingerprintSyncPlanner` decides CREATE/UPDATE/SKIP/DELETE. CQRS: `RunCatalogueSyncCommand`. **Each source API serves its own vocabulary and the uploader translates** - one mapper per source in `Application/Mapping/`, over DTOs in `Application/Dtos/` whose property names mirror the source's response records (both sides use `SnakeCaseLower`, so matching names are what makes the wire keys line up; `BiobankContractParityTests` guards that). The biobank's `p_tnm`, `morphology`, counts and the rest have no value-object slot yet and are dropped deliberately - each mapper names what it drops and why - and anything the catalogue's own vocabulary shapes (nullflavors, MOLGENIS lookup strings) waits for the catalogue contract. A patient is uploaded only when `PatientCatalogueData.IsUploadEligible`: consented **and** carrying at least one sample. Host `Uploader.Host` applies migrations, runs the sync, prints a JSON summary, and exits `0` (no failures) or `1`. Config via env vars (`POSTGRES_*` + the five `*_API_URL`s); see `UploaderOptions`.

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
dotnet ef migrations add <Name> --project src/SequencingApi/SequencingApi.Infrastructure --startup-project src/SequencingApi/SequencingApi.Web -o Persistence/Migrations
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
