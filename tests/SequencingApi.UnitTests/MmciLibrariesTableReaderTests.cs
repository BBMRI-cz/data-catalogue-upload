using SequencingApi.Infrastructure.DataSource.Mmci;
using Xunit;

namespace SequencingApi.UnitTests;

/// <summary>
/// The libraries table — semicolon-delimited, Windows-1250, Czech booleans, and a column set that
/// genuinely differs between versions of the file.
/// </summary>
public sealed class MmciLibrariesTableReaderTests
{
    private const string Csv = """
        Panel;Text in parameters;code in the molgenis catalogue;Availability Date Range;Genes (*all coding regions covered);Vendor;Abbreviation;Library Preparation Kit;PCR Free;Target Enrichment Kit;UMIs Present;BED file
        HyperCap MOP;MMCI_MOP_2022d;MOP2022D;1.1.2022 - 31.12.2023;BRCA1, BRCA2, TP53;Roche;HC;KAPA HyperPlus;NEPRAVDA;KAPA HyperCap;PRAVDA;MMCI_MOP_2022d_capture_targets.bed
        """;

    [Fact]
    public void ReadsAPanelRowIncludingItsCzechBooleans()
    {
        var row = Assert.Single(MmciLibrariesTableReader.Parse(Csv));

        Assert.Equal("HyperCap MOP", row.PanelName);
        Assert.Equal("MMCI_MOP_2022d", row.ParametersText);
        Assert.Equal("MOP2022D", row.CatalogueCode);
        Assert.Equal("Roche", row.Vendor);
        Assert.Equal("HC", row.Abbreviation);
        Assert.Equal("KAPA HyperPlus", row.LibraryPrepKit);
        Assert.Equal("KAPA HyperCap", row.TargetEnrichmentKit);
        Assert.Equal(["BRCA1", "BRCA2", "TP53"], row.Genes);
        Assert.Equal("MMCI_MOP_2022d_capture_targets.bed", row.BedFile);
        Assert.False(row.PcrFree);
        Assert.True(row.UmiPresent);
    }

    [Fact]
    public void AQuotedCellContainingTheDelimiterDoesNotShiftTheColumnsAfterIt()
    {
        // The shape of the two TruSight rows in the live table: the gene cell packs a DNA and an RNA
        // list separated by a semicolon, so the spreadsheet quoted it. Splitting on every semicolon
        // gave the row one field too many and slid Vendor, Abbreviation and the BED file along by
        // one — publishing a 400-character gene list as the vendor and "TRUE" as the BED file.
        var row = Assert.Single(MmciLibrariesTableReader.Parse("""
            Panel;Text in parameters;code in the molgenis catalogue;Availability Date Range;Genes (*all coding regions covered);Vendor;Abbreviation;Library Preparation Kit;PCR Free;Target Enrichment Kit;UMIs Present;BED file
            TruSight Oncology 500 v2;no_parameters;FG_0000782;15.10.2025-now;"DNA panel: ABL1*, ABL2*; RNA panel: ALK*, BCR*";Illumina;TSO500v2;TruSight Oncology 500 Assay;NEPRAVDA;TruSight Oncology Enrichment;PRAVDA;TSO500bedTargetVisible.bed
            """));

        Assert.Equal("Illumina", row.Vendor);
        Assert.Equal("TSO500v2", row.Abbreviation);
        Assert.Equal("TruSight Oncology 500 Assay", row.LibraryPrepKit);
        Assert.Equal("TruSight Oncology Enrichment", row.TargetEnrichmentKit);
        Assert.Equal("TSO500bedTargetVisible.bed", row.BedFile);
        Assert.False(row.PcrFree);
        Assert.True(row.UmiPresent);

        // Both lists are kept, and the "DNA panel:" / "RNA panel:" headings are not genes.
        Assert.Equal(["ABL1*", "ABL2*", "ALK*", "BCR*"], row.Genes);
    }

    [Fact]
    public void SplitsTheAvailabilityRangeIntoItsTwoEnds()
    {
        var row = Assert.Single(MmciLibrariesTableReader.Parse(Csv));

        Assert.Equal(new DateOnly(2022, 1, 1), row.AvailableFrom);
        Assert.Equal(new DateOnly(2023, 12, 31), row.AvailableTo);
        Assert.True(row.CoversDate(new DateOnly(2022, 6, 1)));
        Assert.False(row.CoversDate(new DateOnly(2024, 6, 1)));
    }

    [Fact]
    public void AnOpenEndedRangeCoversEverythingAfterItsStart()
    {
        var row = Assert.Single(MmciLibrariesTableReader.Parse("""
            Panel;Availability Date Range
            TruSight;1.1.2021 -
            """));

        Assert.Equal(new DateOnly(2021, 1, 1), row.AvailableFrom);
        Assert.Null(row.AvailableTo);
        Assert.True(row.CoversDate(new DateOnly(2099, 1, 1)));
        Assert.False(row.CoversDate(new DateOnly(2020, 1, 1)));
    }

    [Fact]
    public void ThePanelIdIsStableAcrossReadsSoReIngestingProducesTheSameRows()
    {
        var first = Assert.Single(MmciLibrariesTableReader.Parse(Csv));
        var second = Assert.Single(MmciLibrariesTableReader.Parse(Csv));

        Assert.Equal(first.PanelId, second.PanelId);
        Assert.Equal("hypercap-mop-20220101", first.PanelId.Value);
    }

    [Fact]
    public void ColumnsAbsentFromThisVersionAreSimplyNull()
    {
        // Newer versions of the real table dropped these three columns entirely.
        var row = Assert.Single(MmciLibrariesTableReader.Parse(Csv));

        Assert.Null(row.InputAmount);
        Assert.Null(row.IntendedInsertSize);
        Assert.Null(row.IntendedReadLength);
    }

    [Fact]
    public void ReadsTheColumnsOnlyOlderVersionsCarry()
    {
        var row = Assert.Single(MmciLibrariesTableReader.Parse("""
            Panel;Input Amount;Intended Insert Size;Intended Read Length
            HyperCap MOP;250;350;151
            """));

        Assert.Equal(250, row.InputAmount);
        Assert.Equal(350, row.IntendedInsertSize);
        Assert.Equal(151, row.IntendedReadLength);
    }

    [Fact]
    public void RowsWithoutAPanelNameAreSkipped()
    {
        var rows = MmciLibrariesTableReader.Parse("""
            Panel;Vendor
            ;Roche
            HyperCap MOP;Roche
            """);

        Assert.Equal("HyperCap MOP", Assert.Single(rows).PanelName);
    }

    [Fact]
    public void AnEmptyOrHeaderOnlyTableYieldsNoRows()
    {
        Assert.Empty(MmciLibrariesTableReader.Parse(string.Empty));
        Assert.Empty(MmciLibrariesTableReader.Parse("Panel;Vendor"));
    }

    [Fact]
    public void AMissingLibrariesDirectoryIsReportedRatherThanThrowing()
    {
        var (rows, problems) = MmciLibrariesTableReader.ReadDirectory(
            Path.Join(Path.GetTempPath(), "no-such-libraries-directory"));

        Assert.Empty(rows);
        Assert.Single(problems);
    }

    [Fact]
    public void TheNewestVersionWinsButBackFillsTheColumnsItDropped()
    {
        // The real hazard: reading only the newest file guarantees the three dropped columns are
        // always empty, because newer versions of the table stopped carrying them.
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var older = Path.Join(directory, "LibrariesV240101.csv");
            var newer = Path.Join(directory, "LibrariesV250101.csv");

            File.WriteAllText(older, """
                Panel;Text in parameters;Input Amount;Intended Insert Size;Intended Read Length;Vendor
                HyperCap MOP;MMCI_MOP_2022d;250;350;151;Roche
                """, MmciSourceValues.LegacyEncoding);

            File.WriteAllText(newer, """
                Panel;Text in parameters;Vendor;BED file
                HyperCap MOP;MMCI_MOP_2024a;Roche;MMCI_MOP_2024a_capture_targets.bed
                """, MmciSourceValues.LegacyEncoding);

            // Modification times deliberately contradict the version in the name: the 2024 table is
            // touched last, as happens whenever someone opens an old version in a spreadsheet, or
            // whenever a checkout writes every file at once. Which revision a file holds is what its
            // name says, so the 2025 table must still win.
            File.SetLastWriteTimeUtc(older, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(newer, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            var (rows, _) = MmciLibrariesTableReader.ReadDirectory(directory);

            var row = Assert.Single(rows);
            Assert.Equal("MMCI_MOP_2024a", row.ParametersText);           // newest version wins
            Assert.Equal("MMCI_MOP_2024a_capture_targets.bed", row.BedFile);
            Assert.Equal(250, row.InputAmount);                            // back-filled
            Assert.Equal(350, row.IntendedInsertSize);
            Assert.Equal(151, row.IntendedReadLength);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
