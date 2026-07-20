using ErrorOr;
using SequencingApi.Application.Abstractions.DataSource;
using SequencingApi.Domain.Common;
using SequencingApi.Domain.Panels;
using SequencingApi.Domain.Runs;
using SequencingApi.Domain.Samples;

namespace SequencingApi.Infrastructure.DataSource.Mmci;

/// <summary>
/// Reads MMCI's organised sequencing tree into domain aggregates — the first implementation of
/// <see cref="ISequencingDataSource"/>, and the only place this service knows anything about how MMCI
/// arranges its files.
/// </summary>
/// <remarks>
/// The tree is organised by run: year, then instrument, then (for MiSeq) how complete the run is, then
/// one folder per run holding one folder per sample. The domain is organised by sample, because a
/// sample is routinely sequenced more than once. Reading therefore happens run-by-run and is then
/// regrouped by sample at the end.
/// <para>
/// The guiding rule throughout, taken from the source survey: the folder tree is the truth about what
/// data exists, not the sample sheet, and almost every "always" — always paired-end, always two read
/// files, one run in one place — is violated somewhere in the corpus.
/// </para>
/// </remarks>
internal sealed class MmciSequencingDataSource : ISequencingDataSource
{
    private const string SamplesFolder = "Samples";
    private const string NextSeqFolder = "NextSeq";
    private const string MiSeqFolder = "MiSEQ";

    /// <summary>
    /// Top-level folders that are not part of the year tree. <c>errors/</c> holds runs that failed to
    /// organise — every one of which also exists, properly organised, in the tree — and <c>backups/</c>
    /// holds raw copies of runs already processed. Serving either would double-count the corpus.
    /// </summary>
    private static readonly string[] ExcludedTopLevelFolders = ["backups", "errors", "logs"];

    private readonly string _organisedRunsPath;
    private readonly string _librariesPath;
    private readonly string _mappingTablePath;

    public MmciSequencingDataSource(string organisedRunsPath, string librariesPath, string mappingTablePath)
    {
        _organisedRunsPath = organisedRunsPath;
        _librariesPath = librariesPath;
        _mappingTablePath = mappingTablePath;
    }

    public string Name => $"mmci:{_organisedRunsPath}";

    public ErrorOr<RecordReadResult> ReadRecords(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_organisedRunsPath))
        {
            return Error.Failure(
                "Sequencing.DirectoryMissing",
                $"sequencing data directory not found: {_organisedRunsPath}");
        }

        var errors = new List<RecordReadError>();
        var runFolders = DiscoverRunFolders(errors);
        if (runFolders.Count == 0)
        {
            return Error.Failure("Sequencing.NoRuns", $"no sequencing runs found in: {_organisedRunsPath}");
        }

        // Both auxiliary tables are optional. Without them panels go unresolved and samples carry no
        // subject reference, which the model can express — unlike a failed ingest, which loses the
        // sequencing data too.
        var (libraryRows, libraryProblems) = MmciLibrariesTableReader.ReadDirectory(_librariesPath);
        Report(errors, _librariesPath, libraryProblems);

        var mappingTable = ReadMappingTable(errors);

        var runs = new List<SequencingRunAggregate>();
        var runSamplesByExternalId = new Dictionary<string, List<RunSample>>(StringComparer.Ordinal);

        foreach (var folder in runFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadRun(folder, libraryRows, runs, runSamplesByExternalId, errors);
        }

        var samples = BuildSamples(runSamplesByExternalId, mappingTable, errors);
        var panels = BuildPanels(libraryRows, errors);

        return new RecordReadResult(samples, runs, panels, errors);
    }

    /// <summary>
    /// Walk the year tree and return one entry per run, de-duplicated by run id.
    /// </summary>
    /// <remarks>
    /// De-duplication is not optional: a run id can appear under two different completeness folders,
    /// and the aggregate rejects a sample carrying the same run twice. The copy with more sample
    /// folders wins, because the reason a run is filed twice is that one copy is the incomplete one.
    /// </remarks>
    private List<MmciRunFolder> DiscoverRunFolders(List<RecordReadError> errors)
    {
        var byRunId = new Dictionary<string, MmciRunFolder>(StringComparer.Ordinal);

        foreach (var yearPath in Subdirectories(_organisedRunsPath))
        {
            var year = Path.GetFileName(yearPath);
            if (ExcludedTopLevelFolders.Contains(year, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            // Year buckets are four digits; anything else at this level is not part of the tree.
            if (year.Length != 4 || !year.All(char.IsAsciiDigit))
            {
                continue;
            }

            foreach (var machinePath in Subdirectories(yearPath))
            {
                var machine = Path.GetFileName(machinePath);
                var isNextSeq = machine.Equals(NextSeqFolder, StringComparison.OrdinalIgnoreCase);
                var isMiSeq = machine.Equals(MiSeqFolder, StringComparison.OrdinalIgnoreCase);
                if (!isNextSeq && !isMiSeq)
                {
                    continue;
                }

                // NextSeq runs sit directly under the machine; MiSeq adds a completeness level.
                if (isNextSeq)
                {
                    foreach (var runPath in Subdirectories(machinePath))
                    {
                        Keep(byRunId, new MmciRunFolder(runPath, "NextSeq", "nextseq"), errors);
                    }

                    continue;
                }

                foreach (var subtypePath in Subdirectories(machinePath))
                {
                    var subtype = Path.GetFileName(subtypePath);
                    foreach (var runPath in Subdirectories(subtypePath))
                    {
                        Keep(byRunId, new MmciRunFolder(runPath, "MiSeq", subtype), errors);
                    }
                }
            }
        }

        return [.. byRunId.Values.OrderBy(folder => folder.RunId, StringComparer.Ordinal)];
    }

    private void Keep(Dictionary<string, MmciRunFolder> byRunId, MmciRunFolder folder, List<RecordReadError> errors)
    {
        if (!byRunId.TryGetValue(folder.RunId, out var existing))
        {
            byRunId[folder.RunId] = folder;
            return;
        }

        var (winner, loser) = folder.SampleFolderCount > existing.SampleFolderCount
            ? (folder, existing)
            : (existing, folder);

        byRunId[folder.RunId] = winner;
        errors.Add(new RecordReadError(
            Name,
            Relative(loser.Path),
            $"run {folder.RunId} found in more than one place; kept {Relative(winner.Path)} "
            + $"({winner.SampleFolderCount} sample folders) over {loser.SampleFolderCount}"));
    }

    private void ReadRun(
        MmciRunFolder folder,
        IReadOnlyList<MmciLibraryRow> libraryRows,
        List<SequencingRunAggregate> runs,
        Dictionary<string, List<RunSample>> runSamplesByExternalId,
        List<RecordReadError> errors)
    {
        var sheet = ReadSampleSheet(folder.Path);

        var run = MmciRunMetadataReader.Read(folder.Path, folder.RunId, folder.InstrumentModel, folder.SourceClass, sheet);
        if (run.IsError)
        {
            errors.Add(new RecordReadError(Name, Relative(folder.Path), Describe(run.Errors)));
            return;
        }

        runs.Add(run.Value);

        var samplesPath = Path.Join(folder.Path, SamplesFolder);
        var sheetRows = sheet.Rows.ToDictionary(row => row.SampleId, StringComparer.OrdinalIgnoreCase);
        var seenInTree = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var samplePath in Subdirectories(samplesPath))
        {
            var externalId = Path.GetFileName(samplePath);
            seenInTree.Add(externalId);

            var sheetRow = sheetRows.GetValueOrDefault(externalId);

            var runSample = MmciSampleFolderReader.Read(samplePath, folder.RunId, sheetRow, _organisedRunsPath);
            if (runSample.IsError)
            {
                errors.Add(new RecordReadError(Name, Relative(samplePath), Describe(runSample.Errors)));
                continue;
            }

            // The orphan case: a folder the sheet never mentioned that turned out to hold no
            // sequencing either. Reported so the source's own inconsistency stays visible, but not
            // ingested — there is nothing here to record. Judged on what was read rather than on
            // whether the directory is literally empty, because a folder can hold files that are not
            // sequencing data, and "no data" is the fact that actually matters.
            if (sheetRow is null && runSample.Value.Files.Count == 0 && runSample.Value.Analyses.Count == 0)
            {
                errors.Add(new RecordReadError(
                    Name,
                    Relative(samplePath),
                    "sample folder absent from the sample sheet holds no sequencing data"));
                continue;
            }

            var withPanel = AttachPanel(runSample.Value, samplePath, libraryRows, sheet, run.Value.RunDate);
            ReportMissingReads(withPanel, run.Value, samplePath, errors);

            if (!runSamplesByExternalId.TryGetValue(externalId, out var list))
            {
                list = [];
                runSamplesByExternalId[externalId] = list;
            }

            list.Add(withPanel);
        }

        // The other half of the reconciliation: the sheet promised a sample the tree never produced.
        foreach (var missing in sheet.Rows.Where(row => !seenInTree.Contains(row.SampleId)))
        {
            errors.Add(new RecordReadError(
                Name,
                Relative(Path.Join(samplesPath, missing.SampleId)),
                "sample listed in the sample sheet has no folder in the run"));
        }
    }

    /// <summary>
    /// Resolve the sample's panel and re-create the run-sample carrying it. Done here rather than in
    /// the folder reader because matching needs the libraries table and the run's date, neither of
    /// which is a property of the sample folder.
    /// </summary>
    private static RunSample AttachPanel(
        RunSample runSample,
        string samplePath,
        IReadOnlyList<MmciLibraryRow> libraryRows,
        MmciSampleSheet sheet,
        DateOnly? runDate)
    {
        var matched = MmciPanelMatcher.Match(
            libraryRows,
            ReadParametersFile(samplePath),
            sheet.ExperimentName,
            runDate);

        if (MmciPanelMatcher.ToLibraryPreparation(matched) is not { } preparation)
        {
            return runSample;
        }

        var withPanel = RunSample.Create(
            runSample.RunId.Value,
            sampleIndex: runSample.SampleIndex,
            sampleType: runSample.SampleType,
            laneCount: runSample.LaneCount,
            libraryPreparation: preparation,
            files: runSample.Files,
            analyses: runSample.Analyses);

        return withPanel.IsError ? runSample : withPanel.Value;
    }

    /// <summary>
    /// Flag a sample that produced fewer read files than the run's own read structure implies. The
    /// expectation is derived from what the instrument actually did — reads times lanes — never from
    /// an assumption that sequencing is paired-end, because a large minority of these runs are not.
    /// </summary>
    private void ReportMissingReads(
        RunSample runSample,
        SequencingRunAggregate run,
        string samplePath,
        List<RecordReadError> errors)
    {
        if (run.ExpectedFastqFilesPerSample is not { } expected || expected <= 0)
        {
            return;
        }

        var actual = runSample.Files.Count(file => file.Role == Domain.FileRole.Fastq);

        // Zero is its own, well-understood state (a sample folder with no reads at all, which over a
        // hundred of them are) and is left to HasFastq rather than reported as a shortfall.
        if (actual > 0 && actual < expected)
        {
            errors.Add(new RecordReadError(
                Name,
                Relative(samplePath),
                $"expected {expected} read files from the run's read structure, found {actual}"));
        }
    }

    /// <summary>
    /// Regroup the run-samples into sample aggregates — the point at which a re-sequenced sample stops
    /// being several unrelated folders and becomes one sample with a history.
    /// </summary>
    private List<SampleAggregate> BuildSamples(
        Dictionary<string, List<RunSample>> runSamplesByExternalId,
        MmciMappingTable mappingTable,
        List<RecordReadError> errors)
    {
        var samples = new List<SampleAggregate>();

        foreach (var (externalId, runSamples) in runSamplesByExternalId.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var sample = SampleAggregate.Create(
                externalId,
                idScheme: MmciIdScheme,
                // The tree only ever knows the pseudonymized number; the real one, which is what the
                // patient service stores, exists solely in the mapping table.
                subjectRef: mappingTable.RealPredictiveNumber(externalId),
                runSamples: runSamples);

            if (sample.IsError)
            {
                errors.Add(new RecordReadError(Name, externalId, Describe(sample.Errors)));
                continue;
            }

            samples.Add(sample.Value);
        }

        return samples;
    }

    private List<PanelAggregate> BuildPanels(IReadOnlyList<MmciLibraryRow> rows, List<RecordReadError> errors)
    {
        var panels = new List<PanelAggregate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (!seen.Add(row.PanelId.Value))
            {
                continue;
            }

            var panel = PanelAggregate.Create(
                row.PanelId.Value,
                name: row.PanelName,
                abbreviation: row.Abbreviation,
                vendor: row.Vendor,
                // ponytail: the libraries table has no assay column. The run's sample sheet does, and
                // that is where assay is recorded instead.
                assay: null,
                catalogueCode: row.CatalogueCode,
                genes: row.Genes,
                targetRegionsRef: row.BedFile,
                availableFrom: row.AvailableFrom,
                availableTo: row.AvailableTo);

            if (panel.IsError)
            {
                errors.Add(new RecordReadError(Name, row.PanelName, Describe(panel.Errors)));
                continue;
            }

            panels.Add(panel.Value);
        }

        return panels;
    }

    private MmciMappingTable ReadMappingTable(List<RecordReadError> errors)
    {
        var path = Path.Join(_mappingTablePath, MmciMappingTableReader.FileName);
        if (!File.Exists(path))
        {
            errors.Add(new RecordReadError(Name, path, "predictive-number mapping table not found"));
            return MmciMappingTable.Empty;
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException exception)
        {
            errors.Add(new RecordReadError(Name, path, $"mapping table unreadable: {exception.Message}"));
            return MmciMappingTable.Empty;
        }

        var (table, problems) = MmciMappingTableReader.Read(json);
        Report(errors, path, problems);
        return table;
    }

    private static string? ReadParametersFile(string samplePath)
    {
        var analysisPath = Path.Join(samplePath, "Analysis");
        if (!Directory.Exists(analysisPath))
        {
            return null;
        }

        var options = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive };
        var path = Directory.EnumerateFiles(analysisPath, "*_Parameters.txt", options)
            .OrderBy(candidate => candidate, StringComparer.Ordinal)
            .FirstOrDefault();

        return path is null ? null : MmciSourceValues.ReadLegacyText(path);
    }

    private void Report(List<RecordReadError> errors, string reference, IReadOnlyList<string> problems) =>
        errors.AddRange(problems.Select(problem => new RecordReadError(Name, reference, problem)));

    private static IEnumerable<string> Subdirectories(string path)
    {
        if (!Directory.Exists(path))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateDirectories(path).OrderBy(entry => entry, StringComparer.Ordinal);
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

    private static MmciSampleSheet ReadSampleSheet(string runPath)
    {
        var path = Path.Join(runPath, MmciSampleSheetReader.FileName);
        return File.Exists(path) && MmciSourceValues.ReadLegacyText(path) is { } content
            ? MmciSampleSheetReader.Read(content)
            : MmciSampleSheet.Empty;
    }

    private string Relative(string path) =>
        Path.GetRelativePath(_organisedRunsPath, path).Replace('\\', '/');

    private static string Describe(IReadOnlyList<Error> errors) =>
        string.Join("; ", errors.Select(error => error.Description));

    /// <summary>
    /// Names the system the sample ids belong to. The domain never interprets the id itself; this is
    /// what tells a consumer whose naming scheme it is looking at.
    /// </summary>
    private const string MmciIdScheme = "mmci_predictive";
}
