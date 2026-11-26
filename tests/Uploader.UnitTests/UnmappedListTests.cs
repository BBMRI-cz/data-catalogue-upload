using ErrorOr;
using Uploader.Application.Abstractions;
using Uploader.Application.Dtos;
using Uploader.Application.Features.Sync;
using Uploader.Application.Mapping;
using Uploader.Domain.Services;
using Xunit;

namespace Uploader.UnitTests;

/// <summary>
/// The unmapped list a run leaves behind, at the handler: source records in, the write to
/// <see cref="IUnmappedValueRepository"/> and the run summary out. The resolver is the real one over
/// <see cref="LiveTerms"/> and today's rules, so a value is a miss exactly as it would be in production.
/// </summary>
public sealed class UnmappedListTests
{
    private static RunCatalogueSyncCommandHandler CreateHandler(
        FakeSourceDataGateway source,
        FakeUnmappedValueRepository unmapped,
        FakeSyncRunRepository? runs = null,
        FakeCatalogueGateway? catalogue = null) =>
        new(
            source,
            catalogue ?? new FakeCatalogueGateway(),
            new RuleMappingTable(),
            new InMemorySyncStateRepository(),
            runs ?? new FakeSyncRunRepository(),
            unmapped,
            new FingerprintSyncPlanner(),
            new FakePseudonymMap(),
            new CatalogueStudy("study_1", "Test Study"),
            TimeProvider.System,
            new RecordingLogger<RunCatalogueSyncCommandHandler>());

    private static PatientDto Patient(string id, string? sex = null, bool consent = true, params SampleDto[] samples) =>
        new()
        {
            PatientId = id,
            Consent = consent,
            Sex = sex,
            Biobank = "MOU",
            Samples = samples.Length == 0
                ? [new SampleDto { SampleId = $"S-{id}", Type = "tissue", MaterialType = "1" }]
                : samples,
        };

    private static SampleDto Tissue(string id, string? diagnosis = null) =>
        new() { SampleId = id, Type = "tissue", MaterialType = "1", Diagnosis = diagnosis };

    private static async Task<ErrorOr<RunCatalogueSyncCommandResult>> Run(RunCatalogueSyncCommandHandler handler) =>
        await handler.Handle(new RunCatalogueSyncCommand(), CancellationToken.None);

    /// <summary>The counts of the run's one write, by field and value.</summary>
    private static Dictionary<(string Field, string Value), int> Written(FakeUnmappedValueRepository unmapped) =>
        Assert.Single(unmapped.Writes).Misses
            .ToDictionary(pair => (pair.Key.Field.ToString(), pair.Key.Value), pair => pair.Value);

    /// <summary>
    /// <c>count</c> is how many catalogue rows hold the value, so the student can work on the values
    /// that matter most first. Two patients' <c>Personal</c> rows hold the sex; one patient's two samples
    /// carry the same diagnosis, which is one <c>Clinical</c> row, so it counts once.
    /// </summary>
    [Fact]
    public async Task EachMissIsWrittenWithTheNumberOfRowsThatHoldIt()
    {
        var unmapped = new FakeUnmappedValueRepository();
        var source = new FakeSourceDataGateway(
        [
            Patient("P1", sex: "x", samples: [Tissue("S1", diagnosis: "C5091"), Tissue("S2", diagnosis: "C5091")]),
            Patient("P2", sex: "x"),
        ]);

        await Run(CreateHandler(source, unmapped));

        Assert.Equal(
            new Dictionary<(string Field, string Value), int>
            {
                [("Personal.genderAtBirth", "x")] = 2,
                [("Clinical.diagnosis", "C5091")] = 1,
            },
            Written(unmapped));
    }

    /// <summary>
    /// A patient without consent never leaves the uploader, not even as an unmapped value or a count:
    /// the value only one ineligible patient holds is not on the list, and the one both hold counts
    /// the eligible patient's row alone.
    /// </summary>
    [Fact]
    public async Task AnIneligiblePatientsValuesAreNotWrittenNorCounted()
    {
        var unmapped = new FakeUnmappedValueRepository();
        var source = new FakeSourceDataGateway(
        [
            Patient("P1", sex: "x", consent: false, samples: [Tissue("S1", diagnosis: "C5091")]),
            Patient("P2", sex: "x"),
        ]);

        await Run(CreateHandler(source, unmapped));

        Assert.Equal(
            new Dictionary<(string Field, string Value), int> { [("Personal.genderAtBirth", "x")] = 1 },
            Written(unmapped));
    }

    /// <summary>
    /// The run summary says how many distinct values went unmapped, so a mapping gap shows in
    /// <c>GET /admin/sync</c> without reading the logs. Distinct is per field, value and context: one
    /// unknown tissue code misses in two columns and is two values.
    /// </summary>
    [Fact]
    public async Task TheRunSummaryCountsTheDistinctValuesLeftUnmapped()
    {
        var runs = new FakeSyncRunRepository();
        var unknownCode = new SampleDto { SampleId = "S2", Type = "tissue", MaterialType = "9" };
        var source = new FakeSourceDataGateway(
        [
            Patient("P1", sex: "x", samples: [Tissue("S1"), unknownCode]),
            Patient("P2", sex: "x"),
        ]);

        var result = await Run(CreateHandler(source, new FakeUnmappedValueRepository(), runs));

        // 'x' once, and '9' as Material.pathologicalState and as Biospecimens.storageConditions.
        Assert.Equal(3, result.Value.Unmapped);
        Assert.Equal(3, runs.Finished!.Unmapped);
    }

    /// <summary>
    /// A run that heard from every source saw every value there is, so it replaces the list - even
    /// with nothing, which is how a value that left the sources drops off it.
    /// </summary>
    [Fact]
    public async Task ARunThatHeardFromEverySourceReplacesTheList()
    {
        var unmapped = new FakeUnmappedValueRepository();
        var source = new FakeSourceDataGateway([Patient("P1", sex: "female")]);

        await Run(CreateHandler(source, unmapped));

        var write = Assert.Single(unmapped.Writes);
        Assert.True(write.Replaced);
        Assert.Empty(write.Misses);
    }

    /// <summary>
    /// A source that did not answer - unreachable, or answering something unreadable - hid the values
    /// it would have served rather than withdrawing them. Replacing would empty its entries while the
    /// student's job reads them, so the run only adds and updates.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ARunASourceDidNotAnswerMergesIntoTheList(bool unreachable)
    {
        var unmapped = new FakeUnmappedValueRepository();
        var source = new FakeSourceDataGateway(
            [Patient("P1", sex: "x", samples: [Tissue("S1") with { PredictiveNumber = "PRED1" }])]);
        if (unreachable)
        {
            source.Unreachable("sequencing");
        }
        else
        {
            source.Unreadable("sequencing");
        }

        await Run(CreateHandler(source, unmapped));

        var write = Assert.Single(unmapped.Writes);
        Assert.False(write.Replaced);
        Assert.Equal(1, Assert.Single(write.Misses, miss => miss.Key.Value == "x").Value);
    }

    /// <summary>A run that stops before its first patient met no value, so it leaves the list as it was.</summary>
    [Fact]
    public async Task ARunThatStopsBeforeAnyPatientLeavesTheListAlone()
    {
        var unmapped = new FakeUnmappedValueRepository();
        var source = new FakeSourceDataGateway([Patient("P1", sex: "x")]);
        source.Unreachable("biobank");

        var result = await Run(CreateHandler(source, unmapped));

        Assert.True(result.IsError);
        Assert.Empty(unmapped.Writes);
    }
}
