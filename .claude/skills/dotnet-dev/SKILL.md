---
name: dotnet-dev
description: Coding patterns and architecture rules for the data-catalogue-upload .NET solution. Use when writing or modifying C# under src/ in any service (BiobankApi, SequencingApi, or Uploader) - adding domain aggregates/value objects, application use cases (Mediator commands/queries) and ports, infrastructure adapters (EF Core repositories, the biobank XML reader, the uploader's typed HttpClient gateways), or hand-written mappers. Covers the solution layout, Clean Architecture layer boundaries, ErrorOr validation, FluentValidation request validators, central package management, and the build/format/test loop.
---

# .NET development (data-catalogue-upload)

One **.NET 10 solution** (`DataCatalogueUpload.slnx`) with services under `src/`, each its own set of
projects following **Clean Architecture + DDD**. Keep changes inside the right layer and the right service,
and validate with `dotnet format` + `dotnet build` before finishing.

## Solution layout

```
src/
├── BiobankApi/     BiobankApi.{Domain,Application,Infrastructure,Web}
├── SequencingApi/  SequencingApi.{Domain,Application,Infrastructure,Web}   (domain + schema landed; ingestion still a stub)
└── Uploader/       Uploader.{Domain,Application,Infrastructure,Host}
```

- **TFM `net10.0`**, nullable + implicit usings on, **warnings are errors** (set in `Directory.Build.props`).
- **Central package management.** All NuGet versions live in `Directory.Packages.props`. Add a package with
  `dotnet add <project> package <name>` (it writes a `<PackageVersion>` there). **Never** put `Version=` on
  an individual `<PackageReference>`.
- **No shared kernel** - each service owns its own DDD base types (`Common/` AggregateRoot, Entity,
  ValueObject, strongly-typed ids). Small deliberate duplication; do **not** introduce a shared project.

## Layers and dependency direction

Per service, dependencies only point inward: `Web|Host -> Infrastructure -> Application -> Domain`.

| Layer | Path (per service) | Put here | Never reference |
|-------|--------------------|----------|-----------------|
| Domain | `<Service>.Domain/` | Aggregates, value objects, invariants, domain services (uploader `FingerprintSyncPlanner`) | Application, Infrastructure, EF Core, `HttpClient`, `XmlReader` |
| Application | `<Service>.Application/` | Mediator commands/queries + handlers (`Features/`), ports (`Abstractions/`), DTOs, hand-written mappers (`Mapping/`), `Behaviors/` | concrete infrastructure types |
| Infrastructure | `<Service>.Infrastructure/` | EF Core `DbContext` + repositories (`Persistence/`), biobank `XmlValueReader`/parser (`Xml/`), uploader typed `HttpClient` gateways (`Http/`), `Configuration/` options | - |

The host (`Web/Program.cs`, `Host/Program.cs`) is the **composition root** - the only place that wires
concrete infrastructure to the application via `AddApplication()` / `AddInfrastructure()` DI extensions.

## Patterns

**Domain models are `record`s; invariants live in a factory.** New aggregates/value objects validate in a
static `Create(...)` that returns `ErrorOr<T>` (collect validation errors), or throw `InvalidOperationException`
for truly unreachable invariant breaks (e.g. an exhaustive `switch` default). Mutable `*SyncState` classes in
the uploader are the deliberate exception.

```csharp
public static ErrorOr<PatientAggregate> Create(PatientId id, int? birthYear /* ... */)
{
    if (birthYear is < 1900 or > 2100)
        return Error.Validation("Patient.BirthYear", "birth year out of range");
    return new PatientAggregate(id /* ... */);
}
```

**No primary constructors on classes/structs.** Handlers, services, repositories, gateways, DbContexts,
and test fakes use an explicit constructor assigning `private readonly` fields (`_camelCase`); DbContexts
chain `: base(options)`. Positional **records** (DTOs, value objects, strongly-typed ids) keep their
parameter list - that idiom stays. The `IDE0290` "use primary constructor" suggestion is turned off in
`.editorconfig`.

**Use cases are Mediator commands/queries.** One `ICommand<ErrorOr<T>>` / `IQuery<...>` + handler per use
case under `Features/...`, dispatched via `ISender`. Handlers return `ErrorOr<T>` - do **not** throw for
expected failures. When a use case returns a structured payload, name the result type after the request -
`<Command>Result` (e.g. `RunCatalogueSyncCommandResult`, `IngestExportsCommandResult`) - in the same
`Features/` folder, one type per file.

**Application-level input validation is FluentValidation.** A request validator is an
`AbstractValidator<TCommand>` in the Application layer; `AddValidatorsFromAssembly` auto-registers it and the
`ValidationBehavior<,>` pipeline stage runs it, short-circuiting to an `ErrorOr` validation error before the
handler. This **complements** the domain `Create(...)` invariants, it does not replace them: validate request
shape/options here, aggregate invariants in the factory. (Both services' current commands are parameterless,
so no validators exist yet - the behavior is wired and dormant until a command carries input.)

**Ports are interfaces** in `<Service>.Application/Abstractions`, implemented in `<Service>.Infrastructure`.
To add an external dependency: define the interface in `Abstractions/`, implement it in `Infrastructure/`,
register it in that service's `DependencyInjection.cs`. Both API services group their ports into subfolders -
`Abstractions/Export/` or `Abstractions/DataSource/` (source port + parse DTOs) and
`Abstractions/Repositories/`; keep new ports grouped likewise.

**One aggregate root = one repository, named after the root** - never after the service:

| Kind | Name | Returns |
|------|------|---------|
| Port (`Application/Abstractions/Repositories`) | `I<Root>Repository` | aggregates |
| EF implementation (`Infrastructure/Persistence`) | `Sql<Root>Repository` | aggregates |
| Test fake | `Fake<Root>Repository` | aggregates |
| Read-model port for cross-aggregate projections | `I<Thing>Reader` - **never** `Repository` | flat DTOs |

So biobank has `IPatientRepository`; sequencing has `ISampleRepository`, `ISequencingRunRepository` and
`IPanelRepository`, plus `ISequencingStatsReader`. The `Reader` suffix is load-bearing: a repository hands
back aggregates through the mappers, a reader projects in SQL and never round-trips the domain. Do not mix
the two in one interface.

```csharp
public interface IPatientRepository
{
    Task<IReadOnlyList<PatientAggregate>> ListPatientsAsync(CancellationToken ct);
    Task<IReadOnlyList<ExportParseError>> SavePatientsAsync(IReadOnlyList<PatientAggregate> patients, CancellationToken ct);
}
```

**Saves are idempotent delete-then-insert** keyed on the aggregate's natural id, with the failure of one
record reported rather than aborting the run (`Save*Async` returns the per-record errors). `SqlSampleRepository`
and `SqlPatientRepository` also batch, then retry a failed batch one record at a time to isolate the offender;
the small aggregates (runs, panels) skip batching deliberately.

**Aggregate roots reference each other by id, never by foreign key.** `run_sample.RunId` and
`library_preparation.PanelId` are indexed but unconstrained, so a sample can be saved before its run or panel
exists - a real FK would make ingest order load-bearing.

**Never flatten a value object into its owner's row.** A 0..1 value object gets its **own table keyed on the
owner's primary key** - `library_preparation(RunSampleId PK/FK)`, `quality_metrics(AnalysisId PK/FK)`. Sharing
the owner's key is what enforces one-to-one, and it makes absence an absent row rather than a nullable column
group that needs a marker to distinguish "not recorded" from "recorded, every field unknown". An **ordered**
child collection gets rows with an explicit `Position` column (`run_read`), never a JSON blob - order that
lives in a serialized string is order a query cannot restore. JSON columns are reserved for **scalar** lists
(`patient.AccessionNumbers`, `panel.Genes`), stored via a `ValueConverter` + `ValueComparer` pair.

**Mapping is hand-written.** DTO -> domain (uploader `SourceMapper`) and domain <-> EF entity
(`PatientMapper`, `SyncStateMapper`) are plain static/instance classes with explicit `new T { ... }` field
copies. There is no source generator, so a dropped or mis-sourced field is **not** a compile error: keep the
public mapper signatures stable and cover every field with a round-trip / record-equality test (see the
`*MapperTests` / `*FieldParityTests`). Reconstitute aggregates through their internal constructor; don't
re-run `Create` validation on data already persisted.

**API endpoints are Minimal API.** An endpoint only builds a Command/Query, calls `ISender`, and maps the
`ErrorOr` to HTTP via `ErrorResults`. No business logic in endpoints; JSON is snake_case (matches the
consumers).

**Uploader change detection is aggregate behaviour.** Each aggregate exposes `ComputeFingerprint()` (SHA-256
over canonical JSON via `Fingerprint.Of(...)`); `FingerprintSyncPlanner` only compares fingerprints to decide
CREATE/UPDATE/SKIP/DELETE. Reuse `Fingerprint.Of` - don't hand-roll hashing.

## What this codebase deliberately does NOT use

- **No Moq / NSubstitute / FluentAssertions.** Tests use hand-written fakes and plain `Assert` (see the
  `testing` skill).

## Validation loop

After any change, run from the repo root and fix until clean (these mirror CI):

```bash
dotnet format DataCatalogueUpload.slnx --verify-no-changes   # drop the flag to auto-fix
dotnet build DataCatalogueUpload.slnx -c Release             # warnings are errors
dotnet test DataCatalogueUpload.slnx
```

Do not loosen analyzer/format settings to silence errors - fix the code. See `AGENTS.md` for the full
convention list and `DEVELOPMENT.md` for EF Core migration commands.
