namespace SequencingApi.Infrastructure.DataSource.Mmci;

/// <summary>
/// A parsed Illumina sample sheet — the run's own index of what it was set up to sequence, plus the
/// operator-entered metadata that appears nowhere else.
/// </summary>
/// <remarks>
/// Never treated as the truth about what data exists: the corpus contains runs whose sheet lists
/// sixteen samples while the tree holds thirty-two folders, and sample folders with no reads at all.
/// The sheet is reconciled against the folders, and disagreements are reported.
/// </remarks>
internal sealed class MmciSampleSheet
{
    /// <summary>A sheet that was absent or unreadable: no header values, no rows.</summary>
    public static MmciSampleSheet Empty { get; } = new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        [],
        []);

    private readonly IReadOnlyDictionary<string, string> _header;

    public MmciSampleSheet(
        IReadOnlyDictionary<string, string> header,
        IReadOnlyList<int> readLengths,
        IReadOnlyList<MmciSampleSheetRow> rows)
    {
        _header = header;
        ReadLengths = readLengths;
        Rows = rows;
    }

    /// <summary>Cycle counts from <c>[Reads]</c>. Informational — the run's read structure comes from RunInfo.</summary>
    public IReadOnlyList<int> ReadLengths { get; }

    public IReadOnlyList<MmciSampleSheetRow> Rows { get; }

    /// <summary>
    /// Drives panel matching when no per-sample parameters file exists. Free-form by nature: the
    /// corpus holds sixteen-plus mutually inconsistent spellings of it.
    /// </summary>
    public string? ExperimentName => Header("Experiment Name");

    public string? Assay => Header("Assay");

    public string? Workflow => Header("Workflow");

    /// <summary>The control software's own name for what it ran, e.g. "FASTQ Only".</summary>
    public string? Application => Header("Application");

    public string? Chemistry => Header("Chemistry");

    /// <summary>A <c>[Header]</c> value by key, case-insensitively; null when the sheet omits it.</summary>
    public string? Header(string key) =>
        _header.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
