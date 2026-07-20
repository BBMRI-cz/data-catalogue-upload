using SequencingApi.Infrastructure.DataSource.Mmci;
using Xunit;

namespace SequencingApi.UnitTests;

/// <summary>
/// The pseudonymized → real predictive-number mapping. This is the only route from a sample folder
/// name to the number the patient service knows the sample by, so a silent failure here would quietly
/// disconnect the two services.
/// </summary>
public sealed class MmciMappingTableReaderTests
{
    private const string Json = """
        {
            "predictive": [
                { "predictive_number": "4-21",  "pseudo_number": "mmci_predictive_40365e78-0d3f-49a1-9d5b-ab4f01f74f80" },
                { "predictive_number": "79-21", "pseudo_number": "mmci_predictive_99597ef6-6a6d-4223-b24f-7cde65d82bcf" }
            ]
        }
        """;

    [Fact]
    public void MapsThePseudonymizedFolderNameToTheRealPredictiveNumber()
    {
        var (table, problems) = MmciMappingTableReader.Read(Json);

        Assert.Empty(problems);
        Assert.Equal(2, table.Count);
        Assert.Equal("4-21", table.RealPredictiveNumber("mmci_predictive_40365e78-0d3f-49a1-9d5b-ab4f01f74f80"));
        Assert.Equal("79-21", table.RealPredictiveNumber("mmci_predictive_99597ef6-6a6d-4223-b24f-7cde65d82bcf"));
    }

    [Fact]
    public void AnUncoveredSampleIsAMissNotAFailure()
    {
        var (table, _) = MmciMappingTableReader.Read(Json);

        // Routine: NextSeq samples in particular are often absent from the mapping.
        Assert.Null(table.RealPredictiveNumber("mmci_predictive_unknown"));
    }

    [Fact]
    public void DuplicatePseudonymizedIdsAreReportedAndFirstWins()
    {
        var (table, problems) = MmciMappingTableReader.Read("""
            {
                "predictive": [
                    { "predictive_number": "1-21", "pseudo_number": "mmci_predictive_dup" },
                    { "predictive_number": "2-21", "pseudo_number": "mmci_predictive_dup" }
                ]
            }
            """);

        // Taking the later one would attach the sequencing to whichever patient was written last.
        Assert.Equal("1-21", table.RealPredictiveNumber("mmci_predictive_dup"));
        Assert.Contains(problems, problem => problem.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EntriesMissingEitherHalfAreReportedAndSkipped()
    {
        var (table, problems) = MmciMappingTableReader.Read("""
            {
                "predictive": [
                    { "predictive_number": "", "pseudo_number": "mmci_predictive_a" },
                    { "predictive_number": "5-21", "pseudo_number": "mmci_predictive_b" }
                ]
            }
            """);

        Assert.Equal(1, table.Count);
        Assert.Null(table.RealPredictiveNumber("mmci_predictive_a"));
        Assert.Single(problems);
    }

    [Fact]
    public void MalformedJsonYieldsAnEmptyTableAndAReasonRatherThanThrowing()
    {
        var (table, problems) = MmciMappingTableReader.Read("{ not json");

        Assert.Equal(0, table.Count);
        Assert.Single(problems);
    }

    [Fact]
    public void AnEmptyDocumentIsReportedAsHavingNoEntries()
    {
        var (table, problems) = MmciMappingTableReader.Read("""{ "predictive": [] }""");

        Assert.Equal(0, table.Count);
        Assert.Single(problems);
    }
}
