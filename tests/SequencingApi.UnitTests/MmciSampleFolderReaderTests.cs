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

            var (runSample, _) = MmciSampleFolderReader.Read(sample, "240104_M02340_0399_LCBRW", null, root);
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

    /// <summary>
    /// Lay out a <c>FASTQ/</c> folder holding the named files and return the run-sample built from it.
    /// </summary>
    private static (RunSample Sample, IReadOnlyList<string> Problems) ReadReads(string sampleId, params string[] fastqNames)
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var sample = Path.Join(root, sampleId);
            Directory.CreateDirectory(Path.Join(sample, "FASTQ"));
            foreach (var name in fastqNames)
            {
                File.WriteAllText(Path.Join(sample, "FASTQ", name), "not really gzipped reads");
            }

            var (runSample, problems) = MmciSampleFolderReader.Read(sample, "240104_M02340_0399_LCBRW", null, root);
            Assert.False(runSample.IsError);
            return (runSample.Value, problems);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AReadFileWithNoLaneInItsNameStillYieldsItsReadNumber()
    {
        // One NextSeq run writes two files per sample with no _L00N_ segment at all, beside the eight
        // per-lane ones. Requiring the lane failed the whole match and discarded the read number too,
        // even though the filename states _R1_ / _R2_ plainly.
        var (runSample, _) = ReadReads(
            "p0001",
            "p0001_S13_R1_001.fastq.gz",
            "p0001_S13_R2_001.fastq.gz");

        Assert.Equal(2, runSample.Files.Count);
        Assert.Equal([1, 2], runSample.Files.Select(file => file.Read));
        Assert.All(runSample.Files, file => Assert.Null(file.Lane));

        // The sample index is stamped in the same name and must survive as well.
        Assert.Equal(13, runSample.SampleIndex);

        // With no lane stated anywhere, the lane count is absent rather than invented as zero.
        Assert.Null(runSample.LaneCount);
    }

    [Fact]
    public void ALaneStampedReadFileStillYieldsLaneAndRead()
    {
        var (runSample, _) = ReadReads(
            "p0001",
            "p0001_S7_L001_R1_001.fastq.gz",
            "p0001_S7_L002_R2_001.fastq.gz");

        Assert.Equal([1, 2], runSample.Files.Select(file => file.Lane));
        Assert.Equal([1, 2], runSample.Files.Select(file => file.Read));
        Assert.Equal(7, runSample.SampleIndex);
        Assert.Equal(2, runSample.LaneCount);
    }

    [Fact]
    public void AReadFileNamingADifferentSampleIsReportedAndSkipped()
    {
        // The real shapes: the pseudonymizer spliced this folder's id into another sample's id, and
        // in one case appended a stray character. The reads belong to whoever the filename names, not
        // to whoever's folder they landed in — ingesting them would serve one patient's reads under
        // another's predictive number.
        var (runSample, problems) = ReadReads(
            "p0001",
            "p0001_S16_L001_R1_001.fastq.gz",              // this sample's own
            "p0002p0001p0003_S19_L001_R1_001.fastq.gz",    // another id with p0001 spliced in
            "p0001x_S20_L001_R1_001.fastq.gz");            // a stray character appended

        // Only the file that names this sample survives.
        Assert.Single(runSample.Files);
        Assert.EndsWith("p0001_S16_L001_R1_001.fastq.gz", runSample.Files[0].Path, StringComparison.Ordinal);

        // ...and the two that do not are reported rather than dropped in silence.
        Assert.Equal(2, problems.Count);
        Assert.All(problems, problem => Assert.Contains("names a different sample", problem, StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("p0002p0001p0003_S19", StringComparison.Ordinal));
    }

    [Fact]
    public void AReadFileWithNoDemultiplexerSuffixIsKeptRatherThanJudged()
    {
        // Without the _S<n>_ suffix there is no id to compare against the folder, so there is no
        // evidence of a mismatch — and absence of evidence must not become a skip.
        var (runSample, problems) = ReadReads("p0001", "undetermined.fastq.gz");

        Assert.Single(runSample.Files);
        Assert.Empty(problems);
    }

    [Fact]
    public void AWholeFolderOfForeignReadsYieldsNoReadsAtAll()
    {
        // The severe case: every read in the folder names someone else. The sample must end up with
        // no reads rather than with another sample's, and every file must be accounted for.
        var (runSample, problems) = ReadReads(
            "p0001",
            "p0009_S1_L001_R1_001.fastq.gz",
            "p0009_S1_L001_R2_001.fastq.gz");

        Assert.Empty(runSample.Files);
        Assert.False(runSample.HasFastq);
        Assert.Equal(2, problems.Count);
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
