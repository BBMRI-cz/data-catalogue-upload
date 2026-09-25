using ErrorOr;
using SequencingApi.Domain;
using SequencingApi.Domain.Panels;
using SequencingApi.Domain.Runs;
using SequencingApi.Domain.Samples;
using Xunit;

namespace SequencingApi.UnitTests;

/// <summary>
/// Value cleaning is asserted through the factories rather than against the internal helper — the
/// observable contract is what a created aggregate holds, not how it got there.
/// </summary>
public sealed class NormalizationTests
{
    // --- sample identity -------------------------------------------------------------

    [Fact]
    public void SampleTrimsExternalIdWithoutChangingCase() =>
        Assert.Equal(
            "mmci_predictive_AbC",
            SampleAggregate.Create("  mmci_predictive_AbC  ", "mmci_predictive").Value.Id.Value);

    [Fact]
    public void SampleLowercasesIdScheme() =>
        Assert.Equal(
            "mmci_predictive",
            SampleAggregate.Create("mmci_predictive_1", " MMCI_Predictive ").Value.IdScheme);

    [Fact]
    public void SampleTrimsBlankPredictiveNumberToNull() =>
        Assert.Null(SampleAggregate
            .Create("mmci_predictive_1", "mmci_predictive", predictiveNumber: "   ")
            .Value.PredictiveNumber);

    // --- predictive key: every party writes the same number its own way --------------

    [Theory]
    [InlineData("2029/5678")]      // the biobank
    [InlineData("2029_5678")]      // a MiSeq sample sheet
    [InlineData("2029-5678")]
    [InlineData("2029_5678_DNA")]  // a NextSeq sample sheet marks the nucleic acid
    [InlineData("2029_5678_rna")]
    [InlineData("DNA_2029_5678")]
    [InlineData("5678-29")]        // an older MiSeq sheet puts the year last
    [InlineData("5678-29_DNA")]
    [InlineData("29-5678")]
    [InlineData("29_5678")]
    [InlineData("  2029/5678  ")]
    public void SamplePredictiveKeyIsTheSameForEveryWrittenFormOfTheNumber(string predictiveNumber) =>
        Assert.Equal("2029-5678", SampleWithPredictiveNumber(predictiveNumber).PredictiveKey);

    [Theory]
    [InlineData("2022/0846")]
    [InlineData("846-22")]
    public void SamplePredictiveKeyDropsLeadingZeros(string predictiveNumber) =>
        Assert.Equal("2022-846", SampleWithPredictiveNumber(predictiveNumber).PredictiveKey);

    [Fact]
    public void SamplePredictiveKeyReadsTheYearFirstWhenBothHalvesAreTwoDigits() =>
        // Ambiguous on its face; the pseudonymizer that produced these numbers read it this way.
        Assert.Equal("2012-19", SampleWithPredictiveNumber("12-19").PredictiveKey);

    [Theory]
    [InlineData("patient-4711")]
    [InlineData("mmci_predictive_00000000-0000-4000-8000-000000000000")]
    [InlineData("ST12-23")]
    [InlineData("2022_control1")]
    [InlineData("123")]
    [InlineData("   ")]
    public void SamplePredictiveKeyIsNullWithoutARecognisableForm(string predictiveNumber) =>
        Assert.Null(SampleWithPredictiveNumber(predictiveNumber).PredictiveKey);

    private static SampleAggregate SampleWithPredictiveNumber(string predictiveNumber) =>
        SampleAggregate.Create("mmci_predictive_1", "mmci_predictive", predictiveNumber: predictiveNumber).Value;

    // --- run id is the de-duplication key, so both sides must canonicalise identically

    [Theory]
    [InlineData("  240104_m02340_0399_lcbrw  ")]
    [InlineData("240104_M02340_0399_LCBRW")]
    public void RunIdIsTrimmedAndUpperCased(string rawRunId) =>
        Assert.Equal("240104_M02340_0399_LCBRW", SequencingRunAggregate.Create(rawRunId).Value.Id.Value);

    [Fact]
    public void RunSampleAndRunNormalizeRunIdIdentically() =>
        Assert.Equal(
            SequencingRunAggregate.Create(" 240104_M02340_0399_LCBRW ").Value.Id,
            RunSample.Create("240104_m02340_0399_lcbrw").Value.RunId);

    [Fact]
    public void SampleRejectsDuplicateRunIdsThatDifferOnlyByCase()
    {
        var result = SampleAggregate.Create(
            "mmci_predictive_1",
            "mmci_predictive",
            runSamples:
            [
                RunSample.Create("240430_M02340_0430_X").Value,
                RunSample.Create(" 240430_m02340_0430_x ").Value,
            ]);

        AssertValidationError(result);
    }

    // --- run text --------------------------------------------------------------------

    [Theory]
    [InlineData("HyperCap-EP-240103", "HyperCap-EP-240103")]
    [InlineData("  HyperCap_240103 ", "HyperCap_240103")]
    [InlineData("TSO500  Run2024_9", "TSO500 Run2024_9")]
    public void ExperimentNameOnlyLosesStrayWhitespace(string raw, string expected) =>
        Assert.Equal(
            expected,
            SequencingRunAggregate.Create("240104_M02340_0399_LCBRW", experimentName: raw).Value.ExperimentName);

    [Fact]
    public void InstrumentIdIsUpperCased() =>
        Assert.Equal(
            "NB552710",
            SequencingRunAggregate.Create("230101_N0000000", instrumentId: " nb552710 ").Value.InstrumentId);

    [Fact]
    public void FlowcellIdIsUpperCased() =>
        Assert.Equal(
            "AHG7LGBGXV",
            SequencingRunAggregate.Create("230101_N0000000", flowcellId: "ahg7lgbgxv").Value.FlowcellId);

    [Fact]
    public void SourceClassIsLowerCased() =>
        Assert.Equal(
            "miseq/complete-runs",
            SequencingRunAggregate.Create("240104_M02340_0399_LCBRW", sourceClass: " MiSEQ/Complete-Runs ")
                .Value.SourceClass);

    [Fact]
    public void BlankRunTextBecomesNull() =>
        Assert.Null(SequencingRunAggregate.Create("240104_M02340_0399_LCBRW", chemistry: "   ").Value.Chemistry);

    // --- analysis --------------------------------------------------------------------

    [Fact]
    public void ReferenceGenomeKeepsItsCase() =>
        Assert.Equal(
            "GRCh37",
            Analysis.Create(AnalysisType.VariantCalling, "NextGENe", referenceGenome: " GRCh37 ")
                .Value.ReferenceGenome);

    [Fact]
    public void PipelineNameLosesStrayWhitespace() =>
        Assert.Equal(
            "NextGENe V2",
            Analysis.Create(AnalysisType.VariantCalling, "  NextGENe   V2 ").Value.PipelineName);

    // --- files -----------------------------------------------------------------------

    [Fact]
    public void SequencingFilePathIsTrimmed() =>
        Assert.Equal("R1.fastq.gz", SequencingFile.Create(FileRole.Fastq, " R1.fastq.gz ").Value.Path);

    [Fact]
    public void SequencingFileFormatIsLowerCased() =>
        Assert.Equal(
            "fastq.gz",
            SequencingFile.Create(FileRole.Fastq, "R1.fastq.gz", format: " FASTQ.GZ ").Value.Format);

    // --- library preparation ---------------------------------------------------------

    [Fact]
    public void LibraryPrepKitLosesStrayWhitespace() =>
        Assert.Equal(
            "KAPA HyperPlus",
            LibraryPreparation.Create(libraryPrepKit: " KAPA   HyperPlus ").Value.LibraryPrepKit);

    // --- panel -----------------------------------------------------------------------

    [Fact]
    public void PanelGenesAreUpperCasedDedupedAndOrderPreserved() =>
        Assert.Equal(
            ["BRCA1", "BRCA2"],
            PanelAggregate.Create("hypercap", "HyperCap", genes: ["brca1", " BRCA2 ", "  ", "BRCA1"]).Value.Genes);

    [Fact]
    public void PanelGenesDefaultToEmpty() =>
        Assert.Empty(PanelAggregate.Create("hypercap", "HyperCap").Value.Genes);

    [Fact]
    public void PanelNameLosesStrayWhitespace() =>
        Assert.Equal("Hyper Cap", PanelAggregate.Create("hypercap", " Hyper   Cap ").Value.Name);

    [Fact]
    public void PanelAbbreviationIsUpperCased() =>
        Assert.Equal("MP", PanelAggregate.Create("mammaprint", "MammaPrint", abbreviation: " mp ").Value.Abbreviation);

    private static void AssertValidationError<T>(ErrorOr<T> result)
    {
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }
}
