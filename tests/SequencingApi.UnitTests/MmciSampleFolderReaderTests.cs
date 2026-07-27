using SequencingApi.Domain;
using SequencingApi.Domain.Samples;
using SequencingApi.Infrastructure.DataSource.Mmci;
using Xunit;

namespace SequencingApi.UnitTests;

/// <summary>
/// Reading one sample folder: which files count as artifacts, and what the pipeline's own statistics
/// header says about the reference it aligned against.
/// </summary>
/// <remarks>
/// Builds a throwaway folder per test rather than leaning on the integration fixture, so a case can
/// carry a reference build that does not appear anywhere in MMCI's corpus.
/// </remarks>
public sealed class MmciSampleFolderReaderTests
{
    /// <summary>
    /// Lay out a minimal <c>Samples/&lt;id&gt;/Analysis/</c> holding a statistics header plus one
    /// artifact, and read it. <paramref name="extraFiles"/> are created empty in <c>Analysis/</c>.
    /// </summary>
    private static Analysis? ReadAnalysis(string statInfo, params string[] extraFiles)
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var sample = Path.Join(root, "p0001");
            var analysis = Path.Join(sample, "Analysis");
            Directory.CreateDirectory(analysis);

            File.WriteAllText(Path.Join(analysis, "p0001_StatInfo.txt"), statInfo, MmciSourceValues.LegacyEncoding);
            File.WriteAllText(Path.Join(analysis, "p0001.bam"), "not really a bam");
            foreach (var name in extraFiles)
            {
                File.WriteAllText(Path.Join(analysis, name), "settings or report body");
            }

            var runSample = MmciSampleFolderReader.Read(sample, "240104_M02340_0399_LCBRW", null, root);
            Assert.False(runSample.IsError);
            return runSample.Value.Analyses.SingleOrDefault();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>The real header shape: a marker line, then the loaded reference as a Windows path.</summary>
    private static string StatInfoFor(string referencePath) =>
        "NextGENe V2.4.2.2\r\n"
        + "[Reference File(s)]:\r\n"
        + referencePath + "\r\n"
        + "[Alignment Statistics]\r\n"
        + "Average Read Length: 151\r\n"
        + "Reference Length: 2938626560\r\n";

    [Theory]
    [InlineData(@"E:\SoftGenetics\NextGene\References\Human_v37p10_dbsnp135", "GRCh37")]
    [InlineData(@"C:\NextGENe\hg19.fasta", "GRCh37")]
    [InlineData(@"E:\SoftGenetics\NextGene\References\Human_v38p1", "GRCh38")]
    [InlineData(@"C:\NextGENe\hg38.fasta", "GRCh38")]
    public void TheLoadedReferenceBecomesTheCatalogueAccession(string referencePath, string expected)
    {
        // The catalogue's field is a controlled vocabulary, so the build is translated rather than
        // passed through — a local file path would be rejected on arrival.
        var analysis = ReadAnalysis(StatInfoFor(referencePath));

        Assert.Equal(expected, analysis!.ReferenceGenome);
    }

    [Fact]
    public void AReferenceLengthIsNotMistakenForAReferenceBuild()
    {
        // The bug this replaced: every path in the header opens with a drive letter, so a key/value
        // split yields the key "E" and the build is never seen. The first key that then contains
        // "Reference" is "Reference Length", and its base-pair count was published as the build name
        // for 3077 of 3101 analyses.
        var analysis = ReadAnalysis(StatInfoFor(@"E:\SoftGenetics\NextGene\References\Human_v37p10_dbsnp135"));

        Assert.Equal("GRCh37", analysis!.ReferenceGenome);
        Assert.NotEqual("2938626560", analysis.ReferenceGenome);
    }

    [Fact]
    public void AnUnrecognisedBuildIsLeftUnsetRatherThanGuessed()
    {
        // Publishing a value the catalogue's vocabulary does not contain is worse than publishing
        // none, and inventing GRCh37 for an unknown build would be asserting something unevidenced.
        var analysis = ReadAnalysis(StatInfoFor(@"E:\References\SomeMouseGenome_v3"));

        Assert.Null(analysis!.ReferenceGenome);
    }

    [Fact]
    public void AHeaderWithNoReferenceMarkerLeavesTheBuildUnset()
    {
        var analysis = ReadAnalysis("NextGENe V2.4.2.2\r\nAverage Read Length: 151\r\n");

        Assert.Null(analysis!.ReferenceGenome);
    }

    [Theory]
    [InlineData("p0001_Mutation_Report1.txt", FileRole.VariantReport)]
    [InlineData("p0001_Coverage_Curve_Report1.txt", FileRole.CoverageReport)]
    public void TheTabularReportsAreRecordedAsArtifacts(string fileName, FileRole expected)
    {
        var analysis = ReadAnalysis(StatInfoFor(@"C:\NextGENe\hg19.fasta"), fileName);

        Assert.Contains(analysis!.Files, file => file.Role == expected);
    }

    [Theory]
    [InlineData("p0001_Mutation_Report1_settings.txt")]
    [InlineData("p0001_Mutation_Report1_Filtered_settings.txt")]
    [InlineData("p0001_Coverage_Curve_Report1_Settings.txt")]
    [InlineData("p0001_Mutation_Report1_Statistics.txt")]
    [InlineData("p0001_Coverage_Curve_Report1_Statistics.txt")]
    [InlineData("bamconversion.log")]
    public void TheFilesBesideAReportAreNotThemselvesReports(string fileName)
    {
        // The pipeline writes a `_settings` dump naming its .ini template next to every report, and a
        // `_Statistics` summary whose numbers are stored as quality metrics instead. Matching on the
        // report's name alone swept both up: 9222 rows, a fifth of the file table.
        var analysis = ReadAnalysis(StatInfoFor(@"C:\NextGENe\hg19.fasta"), fileName);

        Assert.DoesNotContain(
            analysis!.Files,
            file => file.Path.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
    }
}
