---
name: testing
description: Test conventions for the data-catalogue-upload .NET solution. Use when writing, running, or fixing tests in the four test projects under tests/ - unit-testing domain aggregates/factories, the FingerprintSyncPlanner, the SourceMapper and the sync handler with hand-written fakes; integration-testing EF Core repositories against in-memory SQLite (the SqliteDatabase helper) and the biobank API with WebApplicationFactory<Program>. xUnit, plain Assert, no mocking libraries.
---

# Testing (data-catalogue-upload)

Tests live under `tests/`, split into four projects - two per service, **unit** and **integration**:

```
tests/
├── BiobankApi.UnitTests          DomainModelsTests (aggregates/factories), XmlPatientReaderTests (pure XML->domain)
├── BiobankApi.IntegrationTests   ApiTests, RepositoryTests, MapperTests, XmlValueReaderTests, XmlExportParserTests
├── Uploader.UnitTests            FingerprintSyncPlannerTests, FingerprintTests, SourceMapperTests, RunCatalogueSyncHandlerTests
└── Uploader.IntegrationTests     SyncStateRepositoryTests
```

Framework is **xUnit** (`[Fact]` / `[Theory]`) with plain `Assert.*`. **No FluentAssertions, no Moq /
NSubstitute** - assertions are explicit and ports are faked by hand.

- **UnitTests** reference `*.Application` + `*.Domain` (and, for BiobankApi, `*.Infrastructure` so the internal,
  pure `XmlPatientReader` can be driven from an inline `XElement`): still pure, no I/O.
- **IntegrationTests** reference `*.Infrastructure` (+ `*.Web` for the API): they touch a real EF Core engine
  (in-memory SQLite), read real export files from disk (`XmlExportParserTests` over `TestData/Exports`), or
  spin up the web host.

## Running

```bash
dotnet test DataCatalogueUpload.slnx                                  # everything
dotnet test tests/Uploader.UnitTests/Uploader.UnitTests.csproj        # one project
dotnet test --filter "FullyQualifiedName~FingerprintSyncPlannerTests" # one class
dotnet test --filter "FullyQualifiedName~SkipsUnchangedPatient"       # one test
dotnet test -l "console;verbosity=detailed"                           # full assert/log output
```

## Unit tests

**Domain factories / invariants.** A factory returns `ErrorOr<T>`; assert success or the specific validation
error:

```csharp
[Fact]
public void Create_RejectsBirthYearOutOfRange()
{
    var result = PatientAggregate.Create("P1", birthYear: 1800);

    Assert.True(result.IsError);
    Assert.Equal(ErrorType.Validation, result.Errors[0].Type);
}
```

**The planner.** `FingerprintSyncPlannerTests` drives `FingerprintSyncPlanner.Plan(data, existing)` and
asserts the `SyncOp` per entity: no prior -> CREATE, unchanged fingerprint -> SKIP, changed -> UPDATE,
soft-deleted prior -> CREATE, prior-but-now-absent -> DELETE. Build the prior state and assert the returned
operations.

**XML parsing.** `XmlPatientReaderTests` (unit) feeds inline `XElement.Parse(...)` patient XML and asserts the
mapped `PatientAggregate` per schema category, plus whole-patient-atomic failure (`result.IsError`).
`XmlExportParserTests` (integration) runs `XmlExportParser` over the dummy `*.xml` files in `TestData/Exports`
(copied to the test output) and asserts valid patients parse, invalid/malformed files are reported as
`ExportParseError`s, and a missing directory yields an empty result.

**FluentValidation validators.** When a command gains an `AbstractValidator<TCommand>`, unit-test it directly -
`new TCommandValidator().Validate(command)` then assert `result.IsValid` / inspect `result.Errors` with plain
`Assert` (no FluentValidation test helpers). None exist yet - the current commands are parameterless.

## Faking the ports (uploader)

The application depends on interfaces (`ISourceDataGateway`, `ICatalogueGateway`, `ISyncStateRepository`,
`ISyncRunRepository`), so tests use the hand-written fakes in `tests/Uploader.UnitTests/Fakes.cs` - no mocking
framework:

- `FakeSourceDataGateway(IReadOnlyList<PatientDto>)` - returns canned source data.
- `FakeCatalogueGateway` - records `Upserts`/`Deletes`; add an entity type to `FailUpsertTypes` to make that
  upsert return an `Error.Failure` (this is how `RunCatalogueSyncHandlerTests` exercises the `Failed` path
  and the `SyncStatus.Failed` + `LastError` recording).
- `InMemorySyncStateRepository` - dictionaries of `*SyncState` keyed by id; implements the subtree load and
  the soft-delete/mark-missing behaviour.
- `FakeSyncRunRepository` - captures the finished `RunCatalogueSyncCommandResult`.

Inject these where the composition root would inject the real adapters; a class that implements the interface
is all that's needed.

## Integration tests: EF Core repositories

Repositories are tested against a **real engine**, not a mocked `DbContext`. Use the `SqliteDatabase` helper
(one per service's IntegrationTests) - an in-memory SQLite connection held open for the test, with
`EnsureCreated()` building the schema and foreign keys enabled:

```csharp
using var db = new SqliteDatabase();
await new SqlBiobankRepository(db.NewContext()).SavePatientsAsync([PatientAggregate.Create("P1").Value], default);
var loaded = await new SqlBiobankRepository(db.NewContext()).ListPatientsAsync(default);
Assert.Equal("P1", Assert.Single(loaded).Id.Value);
```

Call `NewContext()` again for the read to prove the round-trip went through the DB, not an in-memory tracked
graph. The schema only uses column types SQLite supports, so this stays representative of PostgreSQL. Keep
pure mapping logic (domain <-> EF entity) in `MapperTests` where it needs no engine.

## Integration tests: the biobank API

`ApiTests` uses `WebApplicationFactory<Program>` and replaces the repository with a `FakeBiobankRepository`
via `ConfigureTestServices` (no live database):

```csharp
var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    builder.ConfigureTestServices(services =>
    {
        services.RemoveAll<IBiobankRepository>();
        services.AddScoped<IBiobankRepository>(_ => new FakeBiobankRepository(patients));
    }));
var client = factory.CreateClient();
```

JSON is **snake_case** (`JsonNamingPolicy.SnakeCaseLower`) - match that when deserializing responses in tests.

## CI

CI runs `dotnet test DataCatalogueUpload.slnx --configuration Release` on every push/PR to `master` (all four
projects). Keep tests green before pushing - see the `github-workflow` skill.
