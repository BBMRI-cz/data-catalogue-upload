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
        bool consent = true,
        SampleDto? sample = null) =>
        new()
        {
            PatientId = id,
            Consent = consent,
            Sex = sex,
            Samples =
            [
                (sample ?? new SampleDto { Type = "tissue" }) with { SampleId = $"S-{id}", Diagnosis = diagnosis },
            ],
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

    /// <summary>
    /// A bare material code means nothing without its sample type, so the one warning a run logs for
    /// it says which sample type and label it came with - the same context it is looked up in.
    /// </summary>
    [Fact]
    public async Task AnUnknownMaterialCodeIsReportedOncePerFieldWithItsContext()
    {
        var logger = new RecordingLogger<RunCatalogueSyncCommandHandler>();
        var unknown = new SampleDto { Type = "tissue", MaterialType = "9" };

        await Run(CreateHandler(
            new FakeSourceDataGateway([Patient("P1", sample: unknown), Patient("P2", sample: unknown)]),
            new FakeCatalogueGateway(),
            logger: logger));

        // Tissue is tissue whatever the code, so only the two columns that read the code itself miss.
        Assert.Equal(2, logger.Warnings.Count);
        Assert.Single(logger.Warnings, warning =>
            warning.Contains("Material.pathologicalState '9' (sampleType: tissue, label: -)", StringComparison.Ordinal));
        Assert.Single(logger.Warnings, warning =>
            warning.Contains("Biospecimens.storageConditions '9' (sampleType: tissue, label: -)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheSamplesRawValuesReachThePayloadAsTerms()
    {
        var catalogue = new FakeCatalogueGateway();
        var sample = new SampleDto { Type = "tissue", MaterialType = "1", AvailableSamplesNo = 0 };

        await Run(CreateHandler(
            new FakeSourceDataGateway([Patient("P1", sample: sample) with { Biobank = "MOU" }]), catalogue));

        var payload = Assert.Single(catalogue.SamplePayloads);
        Assert.Equal("Solid Tissue Specimen", payload.Material!.MaterialType);
        Assert.Equal("Tumor", payload.Material.PathologicalState);
        Assert.Equal("Frozen Tissue", payload.Biospecimen!.BiospecimenForm);
        Assert.Equal("Cryotube 1–2mL Programmable freezing to <-135°C", payload.Biospecimen.StorageConditions);
        Assert.Equal("Depleted", payload.Biospecimen.Availability);
        Assert.Equal("Masaryk Memorial Cancer Institute", payload.Biospecimen.ManagingBiobank);
    }

    /// <summary>
    /// The mapping table's key is the field, the value and the whole context, so each of the four
    /// columns asks about the code together with the sample type and the biobank API's label.
    /// </summary>
    [Fact]
    public async Task TheMaterialCodeIsLookedUpWithItsSampleTypeAndLabel()
    {
        var mapping = new FakeMappingTable();
        var sample = new SampleDto { Type = "serum", MaterialType = "SD", MaterialTypeLabel = "Serum, nitrogen-stored" };

        await Run(CreateHandler(
            new FakeSourceDataGateway([Patient("P1", sample: sample)]), new FakeCatalogueGateway(), mapping));

        var lookups = mapping.Lookups.Where(lookup => lookup.Value == "SD").ToList();
        Assert.Equal(
            [
                OntologyFields.MaterialType,
                OntologyFields.PathologicalState,
                OntologyFields.BiospecimenForm,
                OntologyFields.StorageConditions,
            ],
            lookups.Select(lookup => lookup.Field).Distinct());
        Assert.All(
            lookups,
            lookup => Assert.Equal(TermContext.Material("serum", "Serum, nitrogen-stored"), lookup.Context));
    }

    /// <summary>
    /// Resolving the sample before its fingerprint is what lets a newly approved mapping reach a
    /// published sample: its row changes, so it is sent again, while the patient's row stays skipped.
    /// </summary>
    [Fact]
    public async Task AMappingThatStartsAnsweringForAMaterialCodeUpdatesThePublishedSample()
    {
        var source = new FakeSourceDataGateway(
            [Patient("P1", sample: new SampleDto { Type = "tissue", MaterialType = "9" })]);
        var state = new InMemorySyncStateRepository();
        var mapping = new FakeMappingTable();
        await Run(CreateHandler(source, new FakeCatalogueGateway(), mapping, state));

        mapping.Answers[(OntologyFields.StorageConditions, "9")] = MappingAnswer.Mapped(LiveTerms.StorageConditions[0]);
        var catalogue = new FakeCatalogueGateway();
        var result = await Run(CreateHandler(source, catalogue, mapping, state));

        Assert.Equal(1, result.Value.Uploaded);
        Assert.Equal(1, result.Value.Skipped);
        Assert.Equal(
            LiveTerms.StorageConditions[0],
            Assert.Single(catalogue.SamplePayloads).Biospecimen!.StorageConditions);
    }

    /// <summary>A patient without consent never leaves the uploader - nor do their samples' codes.</summary>
    [Fact]
    public async Task AnIneligiblePatientsSampleValuesAreNeverReported()
    {
        var logger = new RecordingLogger<RunCatalogueSyncCommandHandler>();
        var unknown = new SampleDto { Type = "tissue", MaterialType = "9" };

        await Run(CreateHandler(
            new FakeSourceDataGateway([Patient("P1", consent: false, sample: unknown) with { Biobank = "XYZ" }]),
            new FakeCatalogueGateway(),
            logger: logger));

        Assert.Empty(logger.Warnings);
    }
}

