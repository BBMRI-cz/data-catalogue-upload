---
name: testing
description: Test conventions for the data-catalogue-upload .NET solution. Use when writing, running, or fixing tests in the test projects under tests/ (unit + integration per service) - unit-testing domain aggregates/factories, the FingerprintSyncPlanner, the uploader's source mappers and the sync handler with hand-written fakes; integration-testing EF Core repositories against in-memory SQLite (the SqliteDatabase helper), source contracts against a recorded response, and the API hosts with WebApplicationFactory<Program>. xUnit, plain Assert, no mocking libraries.
---

# Testing (data-catalogue-upload)

Tests live under `tests/`, two projects per service - **unit** and **integration**:

```
tests/
├── BiobankApi.UnitTests             DomainModelsTests (aggregates/factories), XmlPatientReaderTests (pure XML->domain)
├── BiobankApi.IntegrationTests      ApiTests, RepositoryTests, MapperTests, XmlValueReaderTests, XmlExportParserTests
├── SequencingApi.UnitTests          DomainModelsTests, NormalizationTests (aggregates/factories), Mmci*Tests (pure source parsers)
├── SequencingApi.IntegrationTests   ApiTests, SequencingEndpointTests, RepositoryTests, MapperTests, StatsReaderTests, MmciSequencingDataSourceTests, IngestEndToEndTests
├── Uploader.UnitTests               FingerprintSyncPlannerTests, FingerprintTests, UploaderMapperTests, UploaderMapperFieldParityTests, BiobankMappingTests, SequencingMapperTests, SequencingMappingTests, RunCatalogueSyncHandlerTests
└── Uploader.IntegrationTests        SyncStateRepositoryTests, SyncStateMappersTests, BiobankFetchTests, BiobankContractParityTests, SequencingFetchTests, SequencingContractParityTests
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

**FluentValidation validators.** When a request gains an `AbstractValidator<TRequest>`, unit-test it directly -
`new TRequestValidator().Validate(request)` then assert `result.IsValid` / inspect `result.Errors` with plain
`Assert` (no FluentValidation test helpers). `GetSequencingQueryValidatorTests` is the pattern; the behaviour
it guards (a malformed request must be a 400, not an empty 200) is also asserted end-to-end in
`SequencingEndpointTests`.

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
await new SqlPatientRepository(db.NewContext()).SavePatientsAsync([PatientAggregate.Create("P1").Value], default);
var loaded = await new SqlPatientRepository(db.NewContext()).ListPatientsAsync(default);
Assert.Equal("P1", Assert.Single(loaded).Id.Value);
```

Call `NewContext()` again for the read to prove the round-trip went through the DB, not an in-memory tracked
graph. The schema only uses column types SQLite supports, so this stays representative of PostgreSQL - SQLite
also honours `CHECK` constraints and `ON DELETE CASCADE`, so both are worth asserting there. Keep pure mapping
logic (domain <-> EF entity) in `MapperTests` where it needs no engine.

**Repositories are per aggregate root** (`SqlPatientRepository`, `SqlSampleRepository`,
`SqlSequencingRunRepository`, `SqlPanelRepository`), so each gets its own round-trip test. Cover the
idempotent re-save as well as the happy path: these repositories delete-then-insert, and a broken cascade only
shows up as duplicate children on the *second* save.

**Source adapters get both.** The pure parsers (`MmciSourceValuesTests`, `MmciSampleSheetReaderTests`,
`MmciNextGeneStatsReaderTests`, `MmciPanelMatcherTests`, `MmciMappingTableReaderTests`,
`MmciLibrariesTableReaderTests`) are **unit** tests driven from inline strings, because those readers take
content rather than paths. `MmciSequencingDataSourceTests` is an **integration** test over the miniature
source tree committed at `tests/SequencingApi.IntegrationTests/TestData/` (copied to the test output by a
csproj `Content` glob, as BiobankApi's `TestData/Exports` is). That fixture deliberately encodes the
source's hazards - a run filed under two subtypes, a sample sequenced in three runs, a sample folder with
no reads, an orphan folder absent from the sample sheet, a single-read run, and two libraries-table
versions with differing columns - so read its `README.md` before changing it. Identifiers in it are
shortened because the real ones exceed Windows' 260-character path limit once copied under `bin/`.

`IngestEndToEndTests` runs the whole pipeline over that tree through `POST /admin/ingest` with the real
repositories on SQLite, and **ingests twice** to prove idempotency: these repositories delete-then-insert,
so a broken cascade only shows up as duplicated children on the second save. It doubles as the coverage
for the Quartz `IngestionJob`, which dispatches the identical command.

`SqliteWebHost.Configure(root, connection)` is the shared wiring behind both that and
`SequencingEndpointTests`: the real host over the `TestData` tree with the Postgres `DbContext` swapped for
SQLite and the real repositories kept. The caller owns the root `WebApplicationFactory` and the open
`SqliteConnection` - the `:memory:` database only lives as long as the connection stays open. The read-side
tests ingest **once** via an `IngestedSequencingHost` class fixture, since ingestion is idempotent and they
only read afterwards. Assert HTTP status codes there, not just bodies: `200` + empty vs `404` vs `400` is
the actual contract with the uploader's gateway.

`SequencingApi.IntegrationTests` shares one fixture file, `SequencingFixtures`, across its mapper, repository
and stats tests. Its aggregates set **every** optional field to a distinct non-default value on purpose:
mappers are hand-written, so a dropped field is only caught if the fixture would notice it going missing.

Cross-aggregate read models are `*StatsReader`, not repositories, and are tested against the database too -
their counters are computed by `GROUP BY` on every call, so a test is the only thing between a mis-written
query and a wrong number (`StatsReaderTests`). Always include the empty-database case.

## Integration tests: the biobank API

`ApiTests` uses `WebApplicationFactory<Program>` and replaces the repository with a `FakePatientRepository`
via `ConfigureTestServices` (no live database):

```csharp
var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    builder.ConfigureTestServices(services =>
    {
        services.RemoveAll<IPatientRepository>();
        services.AddScoped<IPatientRepository>(_ => new FakePatientRepository(patients));
    }));
var client = factory.CreateClient();
```

JSON is **snake_case** (`JsonNamingPolicy.SnakeCaseLower`) - match that when deserializing responses in tests.

## Integration tests: a source contract (uploader)

A mapper unit test starts from a DTO that is already built, so it cannot see the failure that matters most
here: a wire key no DTO property lands on, which turns a whole value object silently empty. **Each source gets
a pair of tests, one per half of that failure.** `BiobankFetchTests` and `SequencingFetchTests` start from a
recorded response body instead of a built DTO, drive it through the real `HttpSourceDataGateway` (internal,
reachable via `InternalsVisibleTo`) with a stub `HttpMessageHandler`, and assert the assembled aggregates.
`BiobankContractParityTests` and `SequencingContractParityTests` compare the served keys against the DTO
property names (converted with `JsonNamingPolicy.SnakeCaseLower.ConvertName`) **in both directions** via the
shared `ContractParity.AssertKeysMatch<T>`, so a rename fails as one missing key plus one orphaned property and
an added field fails as a key with nowhere to go. A nested response gets one fact per level (root, sample, run,
library preparation, panel, file, analysis, quality).

Fixtures live in `tests/Uploader.IntegrationTests/TestData/` and are served by `RecordedResponse`, which owns
the stub factory as well as one accessor per fixture. They are **recorded, not hand-written**: run the source
service's own `WebApplicationFactory` over its committed `TestData` tree and dump the body. Refresh from the
real API when a parity test fails - that is the moment to decide what a new field maps to. Two things these
deliberately do not do: reflect over the source service's response records (that would couple the two services'
assemblies) and put `[JsonUnmappedMemberHandling(Disallow)]` on the DTOs (type-level, so production would throw
the first time the source adds a field, when it should log and carry on).

Reading keys off any element is safe because the source APIs leave `DefaultIgnoreCondition` at `Never` - an
unset field is written as an explicit null rather than omitted.

## CI

CI runs `dotnet test DataCatalogueUpload.slnx --configuration Release` on every push/PR to `master` (all four
projects). Keep tests green before pushing - see the `github-workflow` skill.
