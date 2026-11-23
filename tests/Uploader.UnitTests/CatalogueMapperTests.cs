using Uploader.Application.Mapping;
using Uploader.Domain;
using Uploader.Domain.Common;
using Xunit;

namespace Uploader.UnitTests;

/// <summary>
/// What the catalogue receives for a sequencing aggregate in place of the real identifiers. The
/// pseudonyms here are the fake map's <c>mmci_&lt;kind&gt;_&lt;id&gt;</c>, so an assertion states both
/// that a field was substituted and which identifier it was substituted from. The patient's and
/// samples' rows are covered by <see cref="CataloguePatientMapperTests"/> and
/// <see cref="CatalogueSampleMapperTests"/>.
/// </summary>
public sealed class CatalogueMapperTests
{
    private const string SamplePseudonym = "mmci_sample_S1";
    private const string BiospecimenPseudonym = "mmci_biospecimen_S1";

    private static SequencingAggregate Sequencing() =>
        SequencingAggregate.Create(
            "4-21",
            new SampleId("BBMs:2022:3249:SD"),
            [
                new SamplePreparation
                {
                    // Already pseudonymized by the source: the run tree's folder name.
                    SampleprepIdentifier = "mmci_sampleprep_abc_RUN1",
                    BelongsToBiospecimen = "BBMs:2022:3249:SD",
                    LibraryPreparationKit = "KAPA",
                    Sequencing = new SequencingRun
                    {
                        SequencingIdentifier = "mmci_predictive_abc_RUN1",
                        BelongsToSamplePreparation = "mmci_sampleprep_abc_RUN1",
                        SequencingPlatform = "Illumina",
                        Analyses =
                        [
                            new Analysis
                            {
                                AnalysisIdentifier = "mmci_analysis_abc_RUN1",
                                BelongsToSequencing = "mmci_predictive_abc_RUN1",
                                DataFormatsStored = ["VCF"],
                            },
                        ],
                    },
                },
            ]).Value;

    [Fact]
    public void TheSequencingChainKeepsTheIdentifiersTheSourceAlreadyPseudonymized()
    {
        var payload = CatalogueMapper.ToPayload(Sequencing(), SamplePseudonym);

        var preparation = Assert.Single(payload.SamplePreparations);
        Assert.Equal("mmci_sampleprep_abc_RUN1", preparation.SampleprepIdentifier);
        Assert.Equal("mmci_predictive_abc_RUN1", preparation.Sequencing!.SequencingIdentifier);
        Assert.Equal("mmci_sampleprep_abc_RUN1", preparation.Sequencing.BelongsToSamplePreparation);

        var analysis = Assert.Single(preparation.Sequencing.Analyses);
        Assert.Equal("mmci_analysis_abc_RUN1", analysis.AnalysisIdentifier);
        Assert.Equal("mmci_predictive_abc_RUN1", analysis.BelongsToSequencing);
    }

    /// <summary>
    /// The one identifier in the sequencing chain the source cannot pseudonymize: it points at the
    /// biobank's stored biospecimen, whose id is the biobank's own. It is derived rather than
    /// copied, so it always equals the biospecimen key the sample payload published.
    /// </summary>
    [Fact]
    public void TheSequencingPayloadIsKeyedOnTheSampleAndPointsAtItsBiospecimen()
    {
        var payload = CatalogueMapper.ToPayload(Sequencing(), SamplePseudonym);

        Assert.Equal(SamplePseudonym, payload.ExternalId);
        Assert.Equal(SamplePseudonym, payload.SampleId);
        Assert.Equal(
            BiospecimenPseudonym, Assert.Single(payload.SamplePreparations).BelongsToBiospecimen);
    }
}
