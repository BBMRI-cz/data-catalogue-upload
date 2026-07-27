using System.Text.RegularExpressions;
using ErrorOr;
using SequencingApi.Domain;
using SequencingApi.Domain.Samples;

namespace SequencingApi.Infrastructure.DataSource.Mmci;

/// <summary>
/// Reads one <c>Samples/&lt;predictive number&gt;/</c> folder into a <see cref="RunSample"/>: the read
/// files, and the analysis of them where one exists.
/// </summary>
/// <remarks>
/// The folder, not the sample sheet, decides what this sample actually has. A folder may hold no
/// reads at all (over a hundred do), and only MiSeq complete-runs carry an <c>Analysis/</c> — every
/// other sample in the corpus is reads-only. Both are represented, neither is an error.
/// </remarks>
internal static partial class MmciSampleFolderReader
{
    private const string FastqFolder = "FASTQ";
    private const string AnalysisFolder = "Analysis";

    /// <summary>The pipeline that produced every analysis in this tree, named in its own reports.</summary>
    private const string PipelineName = "NextGENe";

    public static ErrorOr<RunSample> Read(
        string samplePath,
        string runId,
        MmciSampleSheetRow? sheetRow,
        string rootPath)
    {
        var files = ReadFastqFiles(samplePath, rootPath);
        var analyses = ReadAnalyses(samplePath, rootPath);

        return RunSample.Create(
            runId,
            sampleIndex: SampleIndex(files) ?? sheetRow?.Position,
            sampleType: ParseSampleType(sheetRow?.SampleType),
            // Counted from the files that arrived, not from the run's flowcell: a sample is not
            // guaranteed to have reads on every lane the flowcell has.
            laneCount: LaneCount(files),
            // Filled in by the panel matcher, which needs the libraries table and the run date.
            libraryPreparation: null,
            files: files,
            analyses: analyses);
    }

    /// <summary>
    /// The reads. Lane and read number come from the filename the demultiplexer stamped them into,
    /// which is the only place they are recorded per file.
    /// </summary>
    private static IReadOnlyList<SequencingFile> ReadFastqFiles(string samplePath, string rootPath)
    {
        var fastqPath = Path.Join(samplePath, FastqFolder);
        var files = new List<SequencingFile>();

        foreach (var path in EnumerateFiles(fastqPath, "*.fastq.gz"))
        {
            var match = FastqNamePattern().Match(Path.GetFileName(path));
            var file = SequencingFile.Create(
                FileRole.Fastq,
                RelativePath(path, rootPath),
                format: "fastq.gz",
                lane: match.Success ? MmciSourceValues.Int32(match.Groups["lane"].Value) : null,
                read: match.Success ? MmciSourceValues.Int32(match.Groups["read"].Value) : null,
                sizeBytes: SizeBytes(path));

            if (!file.IsError)
            {
                files.Add(file.Value);
            }
        }

        return [.. files.OrderBy(file => file.Path, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The analysis, when one was run. At most one per sample here: the tree holds a single
    /// <c>Analysis/</c> folder, and everything in it is the output of one variant-calling pass.
    /// </summary>
    private static IReadOnlyList<Analysis> ReadAnalyses(string samplePath, string rootPath)
    {
        var analysisPath = Path.Join(samplePath, AnalysisFolder);
        if (!Directory.Exists(analysisPath))
        {
            return [];
        }

        var files = new List<SequencingFile>();
        foreach (var path in EnumerateFiles(analysisPath, "*", recursive: true))
        {
            var name = Path.GetFileName(path);
            var role = RoleOf(name);
            if (role is null)
            {
                continue;
            }

            var file = SequencingFile.Create(
                role.Value,
                RelativePath(path, rootPath),
                format: Format(name),
                sizeBytes: SizeBytes(path));

            if (!file.IsError)
            {
                files.Add(file.Value);
            }
        }

        // A failed metrics parse costs the metrics, not the analysis: the alignment and variant files
        // are still real, and dropping them because one statistics line was malformed loses more.
        var quality = ReadQuality(analysisPath) is { IsError: false } parsed ? parsed.Value : null;

        if (files.Count == 0 && quality is null)
        {
            // An Analysis folder holding nothing this model recognises is not an analysis.
            return [];
        }

        var analysis = Analysis.Create(
            AnalysisType.VariantCalling,
            pipelineName: PipelineName,
            pipelineVersion: null,
            referenceGenome: ReferenceGenome(analysisPath),
            files: [.. files.OrderBy(file => file.Path, StringComparer.Ordinal)],
            quality: quality);

        return analysis.IsError ? [] : [analysis.Value];
    }

    private static ErrorOr<QualityMetrics>? ReadQuality(string analysisPath)
    {
        var reportsPath = Path.Join(analysisPath, "Reports");

        // The mutation statistics are not read: everything they state is a variant summary, and the
        // catalogue has no field for one.
        return MmciNextGeneStatsReader.Read(
            ReadFirst(analysisPath, "*_StatInfo.txt"),
            ReadFirst(reportsPath, "*_Coverage_Curve_Report*_Statistics.txt"));
    }

    /// <summary>
    /// The reference build the alignment used, as the genome accession the data catalogue accepts.
    /// </summary>
    /// <remarks>
    /// The pipeline names the reference by the file it loaded, e.g.
    /// <c>E:\SoftGenetics\NextGene\References\Human_v37p10_dbsnp135</c>. That is a local path, not an
    /// accession, and the catalogue's field is a controlled vocabulary, so the build is translated
    /// rather than passed through — an unrecognised build is left unset instead of published as a
    /// value that would be rejected on arrival.
    /// <para>
    /// Matching on the key alone does not work here: every path in this file opens with a Windows
    /// drive letter, so a key/value split lands on the drive's colon and yields the key <c>E</c>. The
    /// reference is found by its section marker and read from the line beneath it, which is also what
    /// keeps <c>Reference Length</c> — a measurement, in base pairs — from being mistaken for it.
    /// </para>
    /// </remarks>
    private static string? ReferenceGenome(string analysisPath)
    {
        if (ReadFirst(analysisPath, "*_StatInfo.txt") is not { } statInfo)
        {
            return null;
        }

        var lines = MmciSourceValues.Lines(statInfo);
        for (var index = 0; index < lines.Length - 1; index++)
        {
            if (!lines[index].Contains("Reference File", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var build = Path.GetFileNameWithoutExtension(lines[index + 1].Trim().Replace('\\', '/'));
            if (GenomeAccession(build) is { } accession)
            {
                return accession;
            }
        }

        return null;
    }

    /// <summary>
    /// The catalogue's genome accession for a reference build named the way NextGENe names it, or
    /// null when the build is not one this adapter knows.
    /// </summary>
    private static string? GenomeAccession(string build) => build switch
    {
        _ when build.Contains("v37", StringComparison.OrdinalIgnoreCase) => "GRCh37",
        _ when build.Contains("hg19", StringComparison.OrdinalIgnoreCase) => "GRCh37",
        _ when build.Contains("v38", StringComparison.OrdinalIgnoreCase) => "GRCh38",
        _ when build.Contains("hg38", StringComparison.OrdinalIgnoreCase) => "GRCh38",
        _ => null,
    };

    /// <summary>
    /// What an analysis output is, by filename. Anything unrecognised is skipped rather than stored as
    /// <c>Other</c>: the folder is full of logs, settings and intermediate tables that are not
    /// artifacts anyone would ask for, and recording them would bury the ones that matter.
    /// </summary>
    private static FileRole? RoleOf(string fileName)
    {
        if (fileName.EndsWith(".bam", StringComparison.OrdinalIgnoreCase))
        {
            return FileRole.Bam;
        }

        if (fileName.EndsWith(".bam.bai", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".bai", StringComparison.OrdinalIgnoreCase))
        {
            return FileRole.BamIndex;
        }

        if (fileName.EndsWith(".vcf", StringComparison.OrdinalIgnoreCase))
        {
            return fileName.Contains("Filtered", StringComparison.OrdinalIgnoreCase)
                ? FileRole.VcfFiltered
                : FileRole.Vcf;
        }

        if (fileName.EndsWith("_SummaryReport.pdf", StringComparison.OrdinalIgnoreCase))
        {
            return FileRole.SummaryReport;
        }

        // The tabular reports, but never the `_Statistics` summaries beside them (those are metric
        // sources, and their numbers are stored as quality metrics rather than as a file reference)
        // and never the `_settings` dumps either — the pipeline writes one next to every report,
        // naming the .ini template it used. Recording those as reports tripled the variant-report
        // count and doubled the coverage-report count in the production corpus.
        if (fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            && !fileName.Contains("_Statistics", StringComparison.OrdinalIgnoreCase)
            && !fileName.Contains("_settings", StringComparison.OrdinalIgnoreCase))
        {
            if (fileName.Contains("Coverage_Curve", StringComparison.OrdinalIgnoreCase))
            {
                return FileRole.CoverageReport;
            }

            if (fileName.Contains("Mutation_Report", StringComparison.OrdinalIgnoreCase))
            {
                return FileRole.VariantReport;
            }
        }

        return null;
    }

    private static string? Format(string fileName) =>
        fileName.EndsWith(".bam.bai", StringComparison.OrdinalIgnoreCase)
            ? "bam.bai"
            : Path.GetExtension(fileName).TrimStart('.') is { Length: > 0 } extension
                ? extension
                : null;

    /// <summary>
    /// The sample's number within the run, taken from the <c>_S&lt;n&gt;_</c> the demultiplexer stamps
    /// into every read filename. Preferred over the sample sheet's row order because it is what the
    /// instrument actually assigned.
    /// </summary>
    private static int? SampleIndex(IReadOnlyList<SequencingFile> files)
    {
        foreach (var file in files)
        {
            var match = FastqNamePattern().Match(Path.GetFileName(file.Path));
            if (match.Success && MmciSourceValues.Int32(match.Groups["sample"].Value) is { } index and > 0)
            {
                return index;
            }
        }

        return null;
    }

    private static int? LaneCount(IReadOnlyList<SequencingFile> files)
    {
        var lanes = files.Select(file => file.Lane).OfType<int>().Distinct().Count();
        return lanes > 0 ? lanes : null;
    }

    private static SampleType? ParseSampleType(string? raw) => raw?.Trim().ToUpperInvariant() switch
    {
        "DNA" => Domain.SampleType.Dna,
        "RNA" => Domain.SampleType.Rna,
        // Only NextSeq sheets carry the column at all, so absent is the common case, not a fault.
        _ => null,
    };

    private static long? SizeBytes(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string? ReadFirst(string directory, string pattern) =>
        EnumerateFiles(directory, pattern).OrderBy(path => path, StringComparer.Ordinal).FirstOrDefault() is { } path
            ? MmciSourceValues.ReadLegacyText(path)
            : null;

    private static IEnumerable<string> EnumerateFiles(string directory, string pattern, bool recursive = false)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        try
        {
            // Case-insensitive so the same code finds `.FASTQ.GZ` on a case-sensitive Linux server.
            var options = new EnumerationOptions
            {
                MatchCasing = MatchCasing.CaseInsensitive,
                RecurseSubdirectories = recursive,
            };
            return Directory.EnumerateFiles(directory, pattern, options);
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Paths are stored relative to the source root: they are references into a mounted tree, and an
    /// absolute path would bake this host's mount point into every row in the database.
    /// </summary>
    private static string RelativePath(string path, string rootPath) =>
        Path.GetRelativePath(rootPath, path).Replace('\\', '/');

    /// <summary>
    /// <c>&lt;name&gt;_S&lt;n&gt;_L00&lt;lane&gt;_R&lt;read&gt;_001.fastq.gz</c> — the demultiplexer's
    /// naming, and the only record of which lane and which read of the pair a file holds.
    /// </summary>
    [GeneratedRegex(
        @"_S(?<sample>\d+)_L(?<lane>\d+)_R(?<read>\d+)_",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex FastqNamePattern();
}
