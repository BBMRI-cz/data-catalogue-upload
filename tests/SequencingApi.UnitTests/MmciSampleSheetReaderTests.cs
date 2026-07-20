using SequencingApi.Infrastructure.DataSource.Mmci;
using Xunit;

namespace SequencingApi.UnitTests;

/// <summary>
/// Sample-sheet parsing. The sheet is an INI-style CSV whose sections mean different things, and whose
/// <c>[Data]</c> columns differ by instrument — NextSeq adds the DNA/RNA column that MiSeq has no
/// concept of — so every column is read by name.
/// </summary>
public sealed class MmciSampleSheetReaderTests
{
    private const string MiSeqSheet = """
        [Header]
        IEMFileVersion,4
        Experiment Name,HyperCap-EP-240103
        Workflow,GenerateFASTQ
        Application,FASTQ Only
        Assay,KAPA HyperPlus
        Chemistry,Amplicon

        [Reads]
        151
        151

        [Settings]
        Adapter,CTGTCTCTTATACACATCT

        [Data]
        Sample_ID,Sample_Name,I7_Index_ID,index
        mmci_predictive_0001,S1,N701,TAAGGCGA
        mmci_predictive_0002,S2,N702,CGTACTAG
        """;

    private const string NextSeqSheet = """
        [Header]
        Experiment Name,TSO500_Run2024_9

        [Data]
        Sample_ID,Sample_Type,Pair_ID,index
        mmci_predictive_0001,DNA,P1,TAAGGCGA
        mmci_predictive_0009,RNA,P1,CGTACTAG
        """;

    [Fact]
    public void ReadsTheHeaderValuesThatAppearNowhereElse()
    {
        var sheet = MmciSampleSheetReader.Read(MiSeqSheet);

        Assert.Equal("HyperCap-EP-240103", sheet.ExperimentName);
        Assert.Equal("GenerateFASTQ", sheet.Workflow);
        Assert.Equal("FASTQ Only", sheet.Application);
        Assert.Equal("KAPA HyperPlus", sheet.Assay);
        Assert.Equal("Amplicon", sheet.Chemistry);
    }

    [Fact]
    public void ReadsTheDataRowsInOrderWithTheirPosition()
    {
        var sheet = MmciSampleSheetReader.Read(MiSeqSheet);

        Assert.Equal(["mmci_predictive_0001", "mmci_predictive_0002"], sheet.Rows.Select(row => row.SampleId));
        Assert.Equal([1, 2], sheet.Rows.Select(row => row.Position));
    }

    [Fact]
    public void ReadsTheReadLengths()
    {
        var sheet = MmciSampleSheetReader.Read(MiSeqSheet);

        Assert.Equal([151, 151], sheet.ReadLengths);
    }

    [Fact]
    public void MiSeqSheetsHaveNoSampleTypeAtAll()
    {
        var sheet = MmciSampleSheetReader.Read(MiSeqSheet);

        Assert.All(sheet.Rows, row => Assert.Null(row.SampleType));
    }

    [Fact]
    public void ReadsTheNextSeqSampleTypeColumn()
    {
        // Load-bearing: within one run a sample is either DNA or RNA, and they feed different analyses.
        var sheet = MmciSampleSheetReader.Read(NextSeqSheet);

        Assert.Equal("DNA", sheet.Rows[0].SampleType);
        Assert.Equal("RNA", sheet.Rows[1].SampleType);
    }

    [Fact]
    public void ColumnsAreFoundByNameNotByPosition()
    {
        // Same columns, different order: reading by index would silently swap the values.
        var reordered = MmciSampleSheetReader.Read("""
            [Data]
            index,Sample_Type,Sample_ID
            TAAGGCGA,RNA,mmci_predictive_0042
            """);

        var row = Assert.Single(reordered.Rows);
        Assert.Equal("mmci_predictive_0042", row.SampleId);
        Assert.Equal("RNA", row.SampleType);
    }

    [Fact]
    public void QuotedCellsKeepTheirCommas()
    {
        var sheet = MmciSampleSheetReader.Read("""
            [Header]
            Experiment Name,"HyperCap, second attempt"
            """);

        Assert.Equal("HyperCap, second attempt", sheet.ExperimentName);
    }

    [Fact]
    public void UnknownSectionsAreSkippedRatherThanFailingTheParse()
    {
        var sheet = MmciSampleSheetReader.Read("""
            [SomethingNew]
            whatever,1

            [Data]
            Sample_ID
            mmci_predictive_0001
            """);

        Assert.Single(sheet.Rows);
    }

    [Fact]
    public void AnEmptyOrHeaderlessSheetYieldsNoRowsRatherThanThrowing()
    {
        Assert.Empty(MmciSampleSheetReader.Read(string.Empty).Rows);
        Assert.Empty(MmciSampleSheetReader.Read("[Data]").Rows);
        Assert.Null(MmciSampleSheetReader.Read(string.Empty).ExperimentName);
    }

    [Fact]
    public void ToleratesMixedLineEndings()
    {
        var sheet = MmciSampleSheetReader.Read("[Data]\r\nSample_ID\rmmci_predictive_0001\nmmci_predictive_0002");

        Assert.Equal(2, sheet.Rows.Count);
    }
}
