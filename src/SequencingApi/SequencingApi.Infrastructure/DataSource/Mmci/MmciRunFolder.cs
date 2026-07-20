namespace SequencingApi.Infrastructure.DataSource.Mmci;

/// <summary>
/// A run folder found in the tree, together with what its position in the tree says about it.
/// </summary>
/// <remarks>
/// The path carries information the files inside do not: which instrument family produced the run,
/// and how the source classified its completeness. Both are folder levels, so they have to be carried
/// forward from the walk rather than read from the run itself.
/// </remarks>
internal sealed class MmciRunFolder
{
    public MmciRunFolder(string path, string instrumentModel, string sourceClass)
    {
        Path = path;
        RunId = System.IO.Path.GetFileName(path);
        InstrumentModel = instrumentModel;
        SourceClass = sourceClass;
    }

    public string Path { get; }

    /// <summary>The folder name, which is the Illumina run id.</summary>
    public string RunId { get; }

    public string InstrumentModel { get; }

    /// <summary>How the source filed this run — for MiSeq the completeness folder, else the machine.</summary>
    public string SourceClass { get; }

    /// <summary>
    /// How many sample folders the run holds. The tie-breaker when one run id is filed in two places:
    /// the duplicate is there because one copy is missing its data, so the fuller copy is the real one.
    /// </summary>
    public int SampleFolderCount
    {
        get
        {
            var samplesPath = System.IO.Path.Join(Path, "Samples");
            try
            {
                return Directory.Exists(samplesPath) ? Directory.EnumerateDirectories(samplesPath).Count() : 0;
            }
            catch (IOException)
            {
                return 0;
            }
        }
    }
}
