using SequencingApi.Infrastructure.DataSource.Mmci;
using Xunit;

namespace SequencingApi.UnitTests;

/// <summary>
/// Panel resolution. A sample's panel is recorded nowhere in its run, so it is inferred — reliably
/// from the pipeline's parameters file, and best-effort from the hand-typed experiment name, which
/// appears across the corpus in more than a dozen mutually inconsistent spellings.
/// </summary>
public sealed class MmciPanelMatcherTests
{
    private const string LibrariesCsv = """
        Panel;Text in parameters;code in the molgenis catalogue;Availability Date Range;Genes (*all coding regions covered);Vendor;Abbreviation;Library Preparation Kit;PCR Free;Target Enrichment Kit;UMIs Present;BED file
        HyperCap MOP;MMCI_MOP_2022d;MOP2022D;1.1.2022 - 31.12.2023;BRCA1, BRCA2;Roche;HC;KAPA HyperPlus;NEPRAVDA;KAPA HyperCap;PRAVDA;MMCI_MOP_2022d_capture_targets.bed
        HyperCap MOP;MMCI_MOP_2024a;MOP2024A;1.1.2024 - 31.12.2025;BRCA1, BRCA2, TP53;Roche;HC;KAPA HyperPlus;NEPRAVDA;KAPA HyperCap;PRAVDA;MMCI_MOP_2024a_capture_targets.bed
        TruSight Oncology 500;TSO500_v2;TSO500;1.1.2021 - ;ALK, EGFR;Illumina;TSO;TSO500 DNA;NEPRAVDA;TSO500;NEPRAVDA;TSO500bedTargetVisible.bed
        EliGene Prostate;ELIGENE_PROST;EGPROST;1.1.2020 - 31.12.2025;AR;Elisabeth Pharmacon;EG;EliGene;PRAVDA;EliGene;NEPRAVDA;eligene.bed
        MammaPrint;;MP;1.1.2019 - ;MMP1;Agendia;MP;MammaPrint;NEPRAVDA;MammaPrint;NEPRAVDA;mp.bed
        SeqCap_2019;SeqCapPanelSummer19;SC2019;26.8.2019 - 23.7.2020;BRCA1;Roche;CAP2019;SeqCap EZ;NEPRAVDA;SeqCap EZ;NEPRAVDA;MMCI_2019_capture_targets.bed
        Accel BRCA;AccelBRCA;ACBRCA;13.9.2019 - 21.9.2022;BRCA1;Swift;AC-BRCA;Accel;NEPRAVDA;Accel;NEPRAVDA;AccelBRCA.bed
        Accel Custom;Accel_Custom;ACCUST;20.5.2020 - 3.11.2022;KRAS;Swift;AC-CUSTOM;Accel;NEPRAVDA;Accel;NEPRAVDA;Accel_custom_merged.bed
        Accel_ALLinONE;manual;ACALL;;BRCA1, KRAS;Swift;AC;Accel;NEPRAVDA;Accel;NEPRAVDA;accel_all.bed
        """;

    private static readonly IReadOnlyList<MmciLibraryRow> Rows = MmciLibrariesTableReader.Parse(LibrariesCsv);

    [Fact]
    public void TheParametersFileWinsOverTheExperimentName()
    {
        // The parameters file is machine-written; the experiment name is typed by a human. Here they
        // disagree, and the reliable one must decide.
        var match = MmciPanelMatcher.Match(
            Rows,
            parametersText: "Alignment settings\nReference: MMCI_MOP_2024a",
            experimentName: "TSO500_Run2024_9",
            runDate: new DateOnly(2024, 6, 1));

        Assert.Equal("MMCI_MOP_2024a", match!.ParametersText);
    }

    [Theory]
    [InlineData("HyperCap_241210")]        // underscore separator
    [InlineData("HyperCap241217")]         // date fused onto the name
    [InlineData("HyperCap-EP-240103")]     // hyphens, three parts
    [InlineData("HypCap_240301")]          // family spelled short
    [InlineData("SeqCapH240101")]          // the old name of this family, fused date
    public void EveryExperimentNameSpellingResolvesTheSameFamily(string experimentName)
    {
        var match = MmciPanelMatcher.Match(Rows, parametersText: null, experimentName, new DateOnly(2024, 6, 1));

        Assert.NotNull(match);
        Assert.Equal("HyperCap MOP", match!.PanelName);
    }

    [Fact]
    public void ASeqCapRunResolvesToASeqCapPanelRatherThanToHyperCap()
    {
        // "SeqCapH" is the pre-rename spelling of HyperCap, but bare "SeqCap" is its own family with
        // its own panels. Aliasing the two together sent SeqCap runs looking among HyperCap panels,
        // none of which existed when they were sequenced, and they resolved to nothing at all.
        var match = MmciPanelMatcher.Match(Rows, null, "SeqCap190328", new DateOnly(2019, 9, 30));

        Assert.Equal("SeqCap_2019", match!.PanelName);
    }

    [Fact]
    public void AFamilyThatNamesAPanelLiterallyIgnoresTheAliasTable()
    {
        // The aliases are for the short forms operators type; a family that matches a real panel name
        // must never be redirected by one, whatever the run date. This name is years outside the
        // SeqCap window, and it still must not be attributed to HyperCap.
        var match = MmciPanelMatcher.Match(Rows, null, "SeqCap_240101", new DateOnly(2024, 6, 1));

        Assert.Equal("SeqCap_2019", match!.PanelName);
    }

    [Fact]
    public void AnAmbiguousFamilyFallsBackToItsManualCatchAllRow()
    {
        // Three Accel panels cover 2022-05-04, so the availability window cannot separate them. The
        // table marks one row `manual` — "this family, panel not otherwise determined" — and that is
        // the answer, rather than leaving a resolvable sample panel-less.
        var match = MmciPanelMatcher.Match(Rows, null, "Accel_220504", new DateOnly(2022, 5, 4));

        Assert.Equal("Accel_ALLinONE", match!.PanelName);
    }

    [Fact]
    public void TheCatchAllIsNotFilteredByTheRunDate()
    {
        // The catch-all is typically the row with no availability window at all, which is exactly why
        // the window could not separate the candidates. Filtering it by date would discard it.
        Assert.Null(Rows.Single(row => row.PanelName == "Accel_ALLinONE").AvailableFrom);

        var match = MmciPanelMatcher.Match(Rows, null, "Accel_210101", new DateOnly(2021, 1, 1));

        Assert.Equal("Accel_ALLinONE", match!.PanelName);
    }

    [Fact]
    public void TheAvailabilityWindowPicksBetweenPanelsSharingAName()
    {
        // Two HyperCap MOP rows differ only by when they were in use.
        var older = MmciPanelMatcher.Match(Rows, null, "HyperCap_220601", new DateOnly(2022, 6, 1));
        var newer = MmciPanelMatcher.Match(Rows, null, "HyperCap_240601", new DateOnly(2024, 6, 1));

        Assert.Equal("MMCI_MOP_2022d", older!.ParametersText);
        Assert.Equal("MMCI_MOP_2024a", newer!.ParametersText);
    }

    [Fact]
    public void AnAmbiguousNameStaysUnresolvedRatherThanGuessing()
    {
        // Without a run date there is nothing to choose between the two HyperCap windows. Guessing
        // would attribute a sample's genes to the wrong panel, which nothing downstream could detect.
        Assert.Null(MmciPanelMatcher.Match(Rows, null, "HyperCap", runDate: null));
    }

    [Theory]
    [InlineData("TSO500_Run2024_9", "TruSight Oncology 500")]
    [InlineData("TSO_2025_01", "TruSight Oncology 500")]   // the underscore-separated spelling
    [InlineData("EG_240101", "EliGene Prostate")]
    [InlineData("MP_18_2024", "MammaPrint")]
    public void ShortNamesAreExpandedThroughTheAliasTable(string experimentName, string expectedPanel)
    {
        var match = MmciPanelMatcher.Match(Rows, null, experimentName, new DateOnly(2024, 6, 1));

        Assert.Equal(expectedPanel, match!.PanelName);
    }

    [Fact]
    public void TrailingDigitsThatAreNotADateStayPartOfTheName()
    {
        // "TSO500" must not be trimmed to "TSO": only a six-digit YYMMDD is a fused date.
        var match = MmciPanelMatcher.Match(Rows, null, "TSO500", new DateOnly(2024, 6, 1));

        Assert.Equal("TruSight Oncology 500", match!.PanelName);
    }

    [Fact]
    public void AnUnknownPanelFamilyResolvesToNothing()
    {
        Assert.Null(MmciPanelMatcher.Match(Rows, null, "SomethingElse_240101", new DateOnly(2024, 6, 1)));
        Assert.Null(MmciPanelMatcher.Match(Rows, null, experimentName: null, new DateOnly(2024, 6, 1)));
        Assert.Null(MmciPanelMatcher.Match([], "MMCI_MOP_2024a", "HyperCap", new DateOnly(2024, 6, 1)));
    }

    [Fact]
    public void AMatchedRowBecomesTheLibraryPreparation()
    {
        var match = MmciPanelMatcher.Match(Rows, "Reference: ELIGENE_PROST", null, null);

        var preparation = MmciPanelMatcher.ToLibraryPreparation(match);

        Assert.NotNull(preparation);
        Assert.Equal("eligene-prostate-20200101", preparation!.PanelId!.Value.Value);
        Assert.Equal("EliGene", preparation.LibraryPrepKit);
        Assert.True(preparation.PcrFree);
        Assert.False(preparation.UmiPresent);
    }

    [Fact]
    public void NoMatchMeansNoLibraryPreparationAtAll()
    {
        // The domain makes this nullable precisely because resolution routinely fails.
        Assert.Null(MmciPanelMatcher.ToLibraryPreparation(null));
    }
}
