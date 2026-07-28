using System.Xml;
using System.Xml.Linq;
using ErrorOr;
using SequencingApi.Domain.Runs;

namespace SequencingApi.Infrastructure.DataSource.Mmci;

/// <summary>
/// Builds a <see cref="SequencingRunAggregate"/> from the metadata files sitting directly inside a
/// run folder: <c>RunInfo.xml</c>, <c>runParameters.xml</c>/<c>RunParameters.xml</c>,
/// <c>CompletedJobInfo.xml</c> and <c>GenerateFASTQRunStatistics.xml</c> (MiSeq),
/// <c>RunCompletionStatus.xml</c> (NextSeq) and the sample sheet's <c>[Header]</c>.
/// </summary>
/// <remarks>
/// No single file holds the whole picture, and which files exist depends on the instrument and its
/// software generation — the parameters file is even spelled with a different initial letter on old
/// MiSeq. Every field is therefore read defensively and independently: a missing file costs the
/// fields it would have supplied, never the run.
/// <para>
/// <strong>The run carries no start or completion time, deliberately.</strong> Nothing in this tree
/// states either. <c>CompletedJobInfo.xml</c> looks like it does, but its <c>StartTime</c> and
/// <c>CompletionTime</c> belong to MiSeq Reporter's secondary analysis: they run the day after the
/// run for 270 of the 277 folders that have them, and span about ten minutes where a run takes the
/// better part of a day. Reading them as the run's own clock is a plausible wrong answer, which is
/// worse than none. <c>RunParameters.xml</c>'s <c>&lt;RunStartDate&gt;</c> is genuine but adds
/// nothing: it equals the run folder's date for all 358 runs, which is already
/// <see cref="SequencingRunAggregate.RunDate"/>. And no <c>RunCompletionStatus.xml</c> in the corpus
/// carries a <c>&lt;CompletionTime&gt;</c> at all.
/// </para>
/// </remarks>
internal static class MmciRunMetadataReader
{
    /// <summary>
    /// Every run in this tree came off an Illumina instrument. A constant rather than a parsed value:
    /// the control software's <c>ApplicationName</c> names the software, not the platform, and belongs
    /// in the run's workflow instead.
    /// </summary>
    private const string Platform = "Illumina";

    /// <summary>
    /// Read a run folder's metadata. <paramref name="runId"/> is the folder name, which the tree walk
    /// has already de-duplicated on; <paramref name="sourceClass"/> is how the source filed the run.
    /// </summary>
    public static ErrorOr<SequencingRunAggregate> Read(
        string runPath,
        string runId,
        string instrumentModel,
        string sourceClass,
        MmciSampleSheet sheet)
    {
        var runInfo = LoadXml(runPath, "RunInfo.xml");
        var parameters = LoadXml(runPath, "RunParameters.xml") ?? LoadXml(runPath, "runParameters.xml");
        var completedJob = LoadXml(runPath, "CompletedJobInfo.xml");
        var runCompletion = LoadXml(runPath, "RunCompletionStatus.xml");
        var fastqStats = LoadXml(runPath, "GenerateFASTQRunStatistics.xml");

        var run = runInfo?.Descendants("Run").FirstOrDefault();
        var flowcellLayout = runInfo?.Descendants("FlowcellLayout").FirstOrDefault();

        return SequencingRunAggregate.Create(
            runId,
            runNumber: Int(Attribute(run, "Number")) ?? Int(Value(parameters, "RunNumber", "ScanNumber")),
            instrumentModel: instrumentModel,
            instrumentId: Element(runInfo, "Instrument") ?? Value(parameters, "ScannerID", "InstrumentID"),
            platform: Platform,
            sourceClass: sourceClass,
            // The run folder name opens with the run date, which is why it is the fallback: it is the
            // one place the date is guaranteed to exist even when no metadata file survived.
            runDate: MmciSourceValues.Date(Element(runInfo, "Date"))
                ?? MmciSourceValues.ShortDate(Element(runInfo, "Date"))
                ?? MmciSourceValues.ShortDate(RunIdDatePart(runId)),
            flowcellId: Element(runInfo, "Flowcell") ?? Value(parameters, "Barcode", "FlowCellBarcode"),
            laneCount: Int(Attribute(flowcellLayout, "LaneCount")),
            reads: Reads(runInfo),
            assay: sheet.Assay,
            workflow: sheet.Workflow ?? Value(completedJob, "Workflow", "WorkflowType") ?? sheet.Application,
            experimentName: sheet.ExperimentName ?? Value(parameters, "ExperimentName"),
            chemistry: sheet.Chemistry ?? Value(parameters, "Chemistry"),
            reagentKit: Value(parameters, "ReagentKitVersion", "ReagentKitBarcode", "ChemistryVersion"),
            percentageQ30: PercentageQ30(runPath),
            // The two instrument families report the run's cluster statistics in different files and
            // in different units, so each field comes from exactly one of them and stays null on the
            // other. See the remarks on ClusterCountPassingFilter for why they are not merged.
            clusterCountPassingFilter: ClusterCountPassingFilter(fastqStats),
            percentageClustersPassingFilter: MmciSourceValues.Number(Element(runCompletion, "ClustersPassingFilter")),
            clusterDensity: MmciSourceValues.Number(Element(runCompletion, "ClusterDensity")),
            estimatedYield: MmciSourceValues.Number(Element(runCompletion, "EstimatedYield")),
            completionStatus: Element(runCompletion, "CompletionStatus"),
            errorDescription: NoErrorToNull(Element(runCompletion, "ErrorDescription")));
    }

    /// <summary>
    /// Clusters the run passed through the chastity filter, as an absolute count.
    /// </summary>
    /// <remarks>
    /// A stated zero is read as "not stated", not as zero clusters. The control software stopped
    /// filling this element in partway through the corpus — every run up to 2024-02-05 carries a real
    /// figure and every run from 2024-02-13 onwards carries <c>0</c>, while still producing reads
    /// (those runs hold 1975 FASTQ files between them). Storing the zero would be a fabricated
    /// measurement, and a plausible-looking one; it is the same reason the <c>PercentQ30</c> element
    /// beside it is not used at all.
    /// </remarks>
    private static long? ClusterCountPassingFilter(XDocument? fastqStats) =>
        MmciSourceValues.Integer(RunStat(fastqStats, "NumberOfClustersPF")) is { } count and > 0 ? count : null;

    /// <summary>
    /// A run-level value from <c>GenerateFASTQRunStatistics.xml</c>'s <c>RunStats</c> block.
    /// </summary>
    /// <remarks>
    /// Scoped to that block rather than searched for across the document, because the same element
    /// names repeat inside every per-sample summary further down the file — one real run in this
    /// corpus carries seventeen <c>NumberOfClustersPF</c> elements, and only the first is the run's.
    /// </remarks>
    private static string? RunStat(XDocument? document, string name) =>
        Trimmed(document?.Descendants("RunStats").FirstOrDefault()?.Element(name)?.Value);

    /// <summary>
    /// The control software writes the literal <c>None</c> when a run raised no error. That is a
    /// sentinel, not an error description, so it reads as "no error" — the same treatment the
    /// biobank reader gives its <c>-</c>.
    /// </summary>
    private static string? NoErrorToNull(string? raw) =>
        string.Equals(raw, "None", StringComparison.OrdinalIgnoreCase) ? null : raw;

    /// <summary>
    /// The run's read structure, in order. This is what makes "how many read files should a sample
    /// have" derivable instead of assumed — a large minority of these runs are single-read.
    /// </summary>
    private static IReadOnlyList<ReadDefinition> Reads(XDocument? runInfo)
    {
        if (runInfo is null)
        {
            return [];
        }

        var reads = new List<ReadDefinition>();
        foreach (var element in runInfo.Descendants("Read"))
        {
            if (Int(Attribute(element, "NumCycles")) is not { } numCycles)
            {
                continue;
            }

            var isIndexed = string.Equals(Attribute(element, "IsIndexedRead"), "Y", StringComparison.OrdinalIgnoreCase);
            var read = ReadDefinition.Create(numCycles, isIndexed);
            if (!read.IsError)
            {
                reads.Add(read.Value);
            }
        }

        return reads;
    }

    /// <summary>
    /// The share of bases the run called with a Phred score of at least 30, as the control software's
    /// own analysis log states it.
    /// </summary>
    /// <remarks>
    /// <c>AnalysisLog.txt</c> is the only place in this tree that states a run-level Q30, and it does
    /// so as a percentage on a line of its own (<c>Percent >= Q30: 95.9%</c>). The run statistics XML
    /// beside it does carry <c>PercentQ30</c> elements, but every one of them reads zero in this
    /// corpus: these are FASTQ-only workflows, so the instrument never ran the alignment that would
    /// have filled them in. Roughly seven runs in ten have the log; the rest simply have no Q30.
    /// </remarks>
    private static double? PercentageQ30(string runPath)
    {
        if (MmciSourceValues.ReadLegacyText(Path.Join(runPath, "AnalysisLog.txt")) is not { } log)
        {
            return null;
        }

        foreach (var line in MmciSourceValues.Lines(log))
        {
            var marker = line.IndexOf("Q30", StringComparison.OrdinalIgnoreCase);
            if (marker < 0 || MmciSourceValues.KeyValue(line[marker..]) is not { } pair)
            {
                continue;
            }

            if (MmciSourceValues.Number(pair.Value) is { } percentage and >= 0 and <= 100)
            {
                return percentage;
            }
        }

        return null;
    }

    /// <summary>The leading <c>YYMMDD</c> of a run folder name, or null when it is not shaped that way.</summary>
    private static string? RunIdDatePart(string runId)
    {
        var separator = runId.IndexOf('_', StringComparison.Ordinal);
        return separator == 6 ? runId[..6] : null;
    }

    private static XDocument? LoadXml(string runPath, string fileName)
    {
        var path = Path.Join(runPath, fileName);
        try
        {
            // Zero-byte and truncated files occur in this tree; a broken metadata file must cost only
            // the fields it carried, so it is swallowed here and shows up as missing values instead.
            return File.Exists(path) ? XDocument.Load(path) : null;
        }
        catch (XmlException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string? Element(XDocument? document, string name) =>
        Trimmed(document?.Descendants(name).FirstOrDefault()?.Value);

    /// <summary>
    /// First non-empty value among several candidate element names — the control software renamed
    /// several of these between generations, and both spellings are still on disk.
    /// </summary>
    private static string? Value(XDocument? document, params string[] names) =>
        names.Select(name => Element(document, name)).FirstOrDefault(value => value is not null);

    private static string? Attribute(XElement? element, string name) =>
        Trimmed(element?.Attribute(name)?.Value);

    private static int? Int(string? raw) => MmciSourceValues.Int32(raw);

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
