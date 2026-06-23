---
name: dotnet-dev
description: Coding patterns and architecture rules for the data-catalogue-upload .NET solution. Use when writing or modifying C# under src/ in either service (BiobankApi or Uploader) - adding domain aggregates/value objects, application use cases (Mediator commands/queries) and ports, infrastructure adapters (EF Core repositories, the biobank XML reader, the uploader's typed HttpClient gateways), or Mapperly mappers. Covers the solution layout, Clean Architecture layer boundaries, ErrorOr + DomainException, central package management, and the build/format/test loop.
---

# .NET development (data-catalogue-upload)

One **.NET 10 solution** (`DataCatalogueUpload.slnx`) with two services under `src/`, each its own set of
projects following **Clean Architecture + DDD**. Keep changes inside the right layer and the right service,
and validate with `dotnet format` + `dotnet build` before finishing.

## Solution layout

```
src/
├── BiobankApi/   BiobankApi.{Domain,Application,Infrastructure,Web}
└── Uploader/     Uploader.{Domain,Application,Infrastructure,Host}
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
| Application | `<Service>.Application/` | Mediator commands/queries + handlers (`Features/`), ports (`Abstractions/`), DTOs, Mapperly mappers (`Mapping/`), `Behaviors/` | concrete infrastructure types |
| Infrastructure | `<Service>.Infrastructure/` | EF Core `DbContext` + repositories (`Persistence/`), biobank `XmlValueReader`/parser (`Xml/`), uploader typed `HttpClient` gateways (`Http/`), `Configuration/` options | - |

The host (`Web/Program.cs`, `Host/Program.cs`) is the **composition root** - the only place that wires
concrete infrastructure to the application via `AddApplication()` / `AddInfrastructure()` DI extensions.

## Patterns

**Domain models are `record`s; invariants live in a factory.** New aggregates/value objects validate in a
static `Create(...)` that returns `ErrorOr<T>` (collect validation errors), or throw `DomainException` for
truly unreachable invariant breaks. Mutable `*SyncState` classes in the uploader are the deliberate exception.

```csharp
public static ErrorOr<PatientAggregate> Create(PatientId id, int? birthYear /* ... */)
{
    if (birthYear is < 1900 or > 2100)
        return Error.Validation("Patient.BirthYear", "birth year out of range");
    return new PatientAggregate(id /* ... */);
}
```

**Use cases are Mediator commands/queries.** One `ICommand<ErrorOr<T>>` / `IQuery<...>` + handler per use
case under `Features/...`, dispatched via `ISender`. Handlers return `ErrorOr<T>` - do **not** throw for
expected failures.

**Ports are interfaces** in `<Service>.Application/Abstractions`, implemented in `<Service>.Infrastructure`.
To add an external dependency: define the interface in `Abstractions/`, implement it in `Infrastructure/`,
register it in that service's `DependencyInjection.cs`.

```csharp
public interface IBiobankRepository
{
    Task<IReadOnlyList<PatientAggregate>> ListPatientsAsync(CancellationToken ct);
    Task SavePatientsAsync(IReadOnlyList<PatientAggregate> patients, CancellationToken ct);
}
```

**Mapping is source-generated with Mapperly.** DTO -> domain (uploader `SourceMapper`) and domain <-> EF
entity (`PatientMapper`, `SyncStateMapper`) use `[Mapper]` partial classes. Reconstitute aggregates through
their internal constructor; don't re-run `Create` validation on data already persisted.

**API endpoints are Minimal API.** An endpoint only builds a Command/Query, calls `ISender`, and maps the
`ErrorOr` to HTTP via `ErrorResults`. No business logic in endpoints; JSON is snake_case (matches the
consumers).

**Uploader change detection is aggregate behaviour.** Each aggregate exposes `ComputeFingerprint()` (SHA-256
over canonical JSON via `Fingerprint.Of(...)`); `FingerprintSyncPlanner` only compares fingerprints to decide
CREATE/UPDATE/SKIP/DELETE. Reuse `Fingerprint.Of` - don't hand-roll hashing.

## What this codebase deliberately does NOT use

- **No FluentValidation / no `ValidationBehavior`.** Input is validated in the domain factory and surfaced as
  an `ErrorOr` validation error. The only pipeline behavior is `LoggingBehavior`.
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
