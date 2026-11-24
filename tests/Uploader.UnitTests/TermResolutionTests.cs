using ErrorOr;
using Microsoft.Extensions.Logging;
using Uploader.Application.Abstractions;
using Uploader.Application.Dtos;
using Uploader.Application.Features.Sync;
using Uploader.Application.Mapping;
using Uploader.Domain.Services;
using Xunit;

namespace Uploader.UnitTests;

/// <summary>
/// Ontology terms resolved through the run, at the handler: the source's raw values in, payloads and
/// the run's reports out. The resolver is always the real one, over the small term lists of
/// <see cref="LiveTerms"/>; a test that needs the mapping table to say something in particular writes
/// it into a <see cref="FakeMappingTable"/>, and every other test runs today's rules.
/// </summary>
public sealed class TermResolutionTests
{
    private static RunCatalogueSyncCommandHandler CreateHandler(
        FakeSourceDataGateway source,
        FakeCatalogueGateway catalogue,
        IMappingTable? mapping = null,
        InMemorySyncStateRepository? state = null,
        FakeSyncRunRepository? runs = null,
        RecordingLogger<RunCatalogueSyncCommandHandler>? logger = null) =>
        new(
            source,
            catalogue,
            mapping ?? new RuleMappingTable(),
            state ?? new InMemorySyncStateRepository(),
            runs ?? new FakeSyncRunRepository(),
            new FingerprintSyncPlanner(),
            new FakePseudonymMap(),
            new CatalogueStudy("study_1", "Test Study"),
            TimeProvider.System,
            logger ?? new RecordingLogger<RunCatalogueSyncCommandHandler>());

    private static PatientDto Patient(
        string id,
        string? sex = null,
        string? diagnosis = null,
        bool consent = true) =>
        new()
        {
            PatientId = id,
            Consent = consent,
            Sex = sex,
            Samples = [new SampleDto { SampleId = $"S-{id}", Type = "tissue", Diagnosis = diagnosis }],
        };

    private static async Task<ErrorOr<RunCatalogueSyncCommandResult>> Run(
        RunCatalogueSyncCommandHandler handler) =>
        await handler.Handle(new RunCatalogueSyncCommand(), CancellationToken.None);

    [Fact]
    public async Task TheTermListsAreReadOnceAfterTheStudyAndBeforeAnyPatient()
    {
        var catalogue = new FakeCatalogueGateway();

        await Run(CreateHandler(new FakeSourceDataGateway([Patient("P1"), Patient("P2")]), catalogue));

        var read = Assert.Single(catalogue.TermListReads);
        Assert.Equal(["study:study_1"], read.UpsertsSoFar);

        // Every ontology field the uploader fills, so none of them is resolved against a guess.
        Assert.Equal(18, read.Fields.Distinct().Count());
    }

    /// <summary>
    /// A save replaces the whole row, so a run resolving against a list it could not read would erase
    /// every published term of that field. It stops before the first patient instead, and says why.
    /// </summary>
    [Fact]
    public async Task TermListsThatCannotBeReadEndTheRunBeforeAnyPatient()
    {
        var catalogue = new FakeCatalogueGateway
        {
            TermListsError = Error.Failure(
                "Catalogue.NotAnOntologyColumn", "Personal.genderAtBirth is a STRING column, not an ontology."),
        };
        var runs = new FakeSyncRunRepository();
        var state = new InMemorySyncStateRepository();

        var result = await Run(CreateHandler(
            new FakeSourceDataGateway([Patient("P1")]), catalogue, state: state, runs: runs));

        Assert.True(result.IsError);
        Assert.Equal(runs.Finished!.RunId, runs.Started);
        Assert.Equal(
            "Cannot read the catalogue's term lists: Personal.genderAtBirth is a STRING column, not an ontology.",
            runs.Finished.Failure);
        Assert.Equal(["study:study_1"], catalogue.Upserts);
        Assert.Empty(state.Patients);
    }

    [Fact]
    public async Task AMappingTableThatCannotBeLoadedEndsTheRunBeforeAnyPatient()
    {
        var catalogue = new FakeCatalogueGateway();
        var runs = new FakeSyncRunRepository();
        var mapping = new FakeMappingTable { LoadError = Error.Failure(description: "mapping table unreachable") };

        var result = await Run(CreateHandler(
            new FakeSourceDataGateway([Patient("P1")]), catalogue, mapping, runs: runs));

        Assert.True(result.IsError);
        Assert.Equal("Cannot load the mapping table: mapping table unreachable", runs.Finished!.Failure);
        Assert.Equal(["study:study_1"], catalogue.Upserts);
    }

    [Fact]
    public async Task ThePatientsRawValuesReachThePayloadAsTerms()
    {
        var catalogue = new FakeCatalogueGateway();

        await Run(CreateHandler(
            new FakeSourceDataGateway([Patient("P1", sex: "female", diagnosis: "C504") with { Biobank = "MOU" }]),
            catalogue));

        var payload = Assert.Single(catalogue.PatientPayloads);
        Assert.Equal("assigned female at birth", payload.Personal!.GenderAtBirth);
        Assert.Equal("Masaryk Memorial Cancer Institute", payload.Personal.PrimaryAffiliatedInstitute);
        Assert.Equal(["C50.4"], payload.Clinical!.Diagnosis);
    }

    [Fact]
    public async Task AValueThatIsAlreadyALiveTermIsUsedWithoutAskingTheMappingTable()
    {
        var catalogue = new FakeCatalogueGateway();
        var mapping = new FakeMappingTable();

        await Run(CreateHandler(
            new FakeSourceDataGateway([Patient("P1", sex: "assigned female at birth")]), catalogue, mapping));

        Assert.Equal("assigned female at birth", Assert.Single(catalogue.PatientPayloads).Personal!.GenderAtBirth);
        Assert.DoesNotContain(mapping.Lookups, lookup => lookup.Field == OntologyFields.GenderAtBirth);
    }

    [Fact]
    public async Task AMissIsLeftOutOfARowThatStillGoesOut()
    {
        var catalogue = new FakeCatalogueGateway();
        var logger = new RecordingLogger<RunCatalogueSyncCommandHandler>();

        var result = await Run(CreateHandler(
            new FakeSourceDataGateway([Patient("P1", sex: "x", diagnosis: "C504")]), catalogue, logger: logger));

        var payload = Assert.Single(catalogue.PatientPayloads);
        Assert.Null(payload.Personal!.GenderAtBirth);
        Assert.Equal(["C50.4"], payload.Clinical!.Diagnosis);
        Assert.Equal(0, result.Value.Failed);
        Assert.Single(logger.Warnings, warning => warning.Contains("Personal.genderAtBirth 'x'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ANoTermAnswerLeavesTheFieldOutWithoutReportingIt()
    {
        var catalogue = new FakeCatalogueGateway();
        var logger = new RecordingLogger<RunCatalogueSyncCommandHandler>();
        var mapping = new FakeMappingTable();
        mapping.Answers[(OntologyFields.GenderAtBirth, "x")] = MappingAnswer.NoTerm;

        await Run(CreateHandler(new FakeSourceDataGateway([Patient("P1", sex: "x")]), catalogue, mapping, logger: logger));

        Assert.Null(Assert.Single(catalogue.PatientPayloads).Personal!.GenderAtBirth);
        Assert.Empty(logger.Warnings);
    }

    /// <summary>
    /// Only an exact live term may reach a payload, so an answer the catalogue does not list - a
    /// stale entry, a term the ontology dropped - is reported like any other miss rather than sent.
    /// </summary>
    [Fact]
    public async Task AMappingAnswerThatIsNotALiveTermIsAMiss()
    {
        var catalogue = new FakeCatalogueGateway();
        var logger = new RecordingLogger<RunCatalogueSyncCommandHandler>();
        var mapping = new FakeMappingTable();
        mapping.Answers[(OntologyFields.GenderAtBirth, "x")] = MappingAnswer.Mapped("assigned x at birth");

        await Run(CreateHandler(new FakeSourceDataGateway([Patient("P1", sex: "x")]), catalogue, mapping, logger: logger));

        Assert.Null(Assert.Single(catalogue.PatientPayloads).Personal!.GenderAtBirth);
        var warning = Assert.Single(logger.Warnings);
        Assert.Contains("Personal.genderAtBirth 'x'", warning, StringComparison.Ordinal);
        Assert.Contains("'assigned x at birth'", warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EachDistinctMissIsReportedOncePerRun()
    {
        var logger = new RecordingLogger<RunCatalogueSyncCommandHandler>();

        await Run(CreateHandler(
            new FakeSourceDataGateway(
            [
                Patient("P1", sex: "x", diagnosis: "C5091"),
                Patient("P2", sex: "x", diagnosis: "C5091"),
            ]),
            new FakeCatalogueGateway(),
            logger: logger));

        Assert.Equal(2, logger.Warnings.Count);
        Assert.Single(logger.Warnings, warning => warning.Contains("Personal.genderAtBirth 'x'", StringComparison.Ordinal));
        Assert.Single(logger.Warnings, warning => warning.Contains("Clinical.diagnosis 'C5091'", StringComparison.Ordinal));
    }

    /// <summary>A patient without consent never leaves the uploader, not even as an unmapped value.</summary>
    [Fact]
    public async Task AnIneligiblePatientsValuesAreNeverReported()
    {
        var logger = new RecordingLogger<RunCatalogueSyncCommandHandler>();

        await Run(CreateHandler(
            new FakeSourceDataGateway([Patient("P1", sex: "x", consent: false), Patient("P2", sex: "y")]),
            new FakeCatalogueGateway(),
            logger: logger));

        var warning = Assert.Single(logger.Warnings);
        Assert.Contains("'y'", warning, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolving before the fingerprint is what lets a newly approved mapping reach a published
    /// patient: the row changes, so its fingerprint does, so the patient is sent again. The sample's
    /// row is untouched and stays skipped.
    /// </summary>
    [Fact]
    public async Task AMappingThatStartsAnsweringUpdatesThePublishedPatient()
    {
        var source = new FakeSourceDataGateway([Patient("P1", sex: "x")]);
        var state = new InMemorySyncStateRepository();
        var mapping = new FakeMappingTable();
        await Run(CreateHandler(source, new FakeCatalogueGateway(), mapping, state));

        mapping.Answers[(OntologyFields.GenderAtBirth, "x")] = MappingAnswer.Mapped("assigned female at birth");
        var catalogue = new FakeCatalogueGateway();
        var result = await Run(CreateHandler(source, catalogue, mapping, state));

        Assert.Equal(1, result.Value.Uploaded);
        Assert.Equal(1, result.Value.Skipped);
        Assert.Equal("assigned female at birth", Assert.Single(catalogue.PatientPayloads).Personal!.GenderAtBirth);
    }
}
