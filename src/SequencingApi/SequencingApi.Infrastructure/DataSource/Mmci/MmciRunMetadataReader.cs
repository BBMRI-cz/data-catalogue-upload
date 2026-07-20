using System.Xml;
using System.Xml.Linq;
using ErrorOr;
using SequencingApi.Domain.Runs;

namespace SequencingApi.Infrastructure.DataSource.Mmci;

/// <summary>
/// Builds a <see cref="SequencingRunAggregate"/> from the metadata files sitting directly inside a
/// run folder: <c>RunInfo.xml</c>, <c>runParameters.xml</c>/<c>RunParameters.xml</c>,
/// <c>CompletedJobInfo.xml</c> (MiSeq), <c>RunCompletionStatus.xml</c> (NextSeq) and the sample
/// sheet's <c>[Header]</c>.
/// </summary>
/// <remarks>
/// No single file holds the whole picture, and which files exist depends on the instrument and its
/// software generation — the parameters file is even spelled with a different initial letter on old
/// MiSeq. Every field is therefore read defensively and independently: a missing file costs the
/// fields it would have supplied, never the run.
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
        var completionStatus = LoadXml(runPath, "RunCompletionStatus.xml");

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
            startedAt: MmciSourceValues.Timestamp(Value(completedJob, "StartTime", "RunStartDate")),
            completedAt: MmciSourceValues.Timestamp(
                Value(completedJob, "CompletionTime") ?? Value(completionStatus, "CompletionTime")),
            // ponytail: %Q30 is not stated anywhere in this tree. GenerateFASTQRunStatistics.xml holds
            // per-tile cluster counts, and deriving a run-level Q30 from them would be inventing a
            // number the source never claimed. Left null until a source states one.
            percentageQ30: null);
    }

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
