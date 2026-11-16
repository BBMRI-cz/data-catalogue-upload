namespace TrainingSeeder.Vocabulary;

/// <summary>
/// <c>sequencing-vocabulary.json</c>: what the real sequencing database holds, as counts, plus the
/// lab's configurations whole - run set-ups, library preparations and the panel table - because
/// those are equipment and vendor data and only make sense together.
/// </summary>
/// <param name="Schema">The last migration the exported database had applied.</param>
/// <param name="Samples">The real sample count.</param>
/// <param name="Runs">The real run count; the dataset scales it with the patient count.</param>
internal sealed record SequencingVocabulary(
    string Source,
    string Schema,
    int Samples,
    int Runs,
    IReadOnlyDictionary<string, IReadOnlyList<ValueCount>> Values,
    IReadOnlyDictionary<string, IReadOnlyList<ValueCount>> PerParent,
    IReadOnlyDictionary<string, NumberRange> Ranges,
    IReadOnlyDictionary<string, FilledCount> Filled,
    MammaPrintCount MammaPrint,
    IReadOnlyList<RunProfileCount> RunProfiles,
    IReadOnlyList<RunSampleProfileCount> RunSampleProfiles,
    IReadOnlyList<PanelRow> Panels);

/// <summary>How many runs the uploader would recognise as MammaPrint, by either of its two tests.</summary>
internal sealed record MammaPrintCount(long BySourceClass, long ByExperimentName, long Either, long Runs);

/// <summary>A run configuration and how many runs used it.</summary>
internal sealed record RunProfileCount(int Id, long Count, RunProfile Profile);

/// <summary>
/// Everything about a run except its identity, date and numbers. The <c>Has*</c> flags say which
/// statistics the run states; their values are drawn from the vocabulary's ranges.
/// </summary>
/// <param name="ReadStructure">The reads in order, e.g. <c>151,8i,8i,151</c>; <c>i</c> marks an index read.</param>
/// <param name="MammaPrintName">Whether the run's experiment name starts with <c>MP_</c>.</param>
/// <param name="HasClusterStats">Whether the run states cluster density, yield and % passing filter.</param>
internal sealed record RunProfile(
    string? Platform,
    string? InstrumentModel,
    string? InstrumentId,
    string? SourceClass,
    string? Chemistry,
    string? ReagentKit,
    string? Workflow,
    string? Assay,
    int? LaneCount,
    string? CompletionStatus,
    string? ReadStructure,
    bool MammaPrintName,
    bool HasPercentageQ30,
    bool HasClusterCount,
    bool HasClusterStats);

/// <summary>A sample's set-up on a run of the given configuration, and how many run samples had it.</summary>
internal sealed record RunSampleProfileCount(int RunProfile, long Count, RunSampleProfile Profile);

/// <summary>What one sample got on one run.</summary>
/// <param name="FastqFiles">How many read files the run sample has.</param>
/// <param name="FastqWithoutLane">How many of those state no lane.</param>
internal sealed record RunSampleProfile(
    string? SampleType,
    int? LaneCount,
    int FastqFiles,
    int FastqWithoutLane,
    LibraryProfile? Library,
    AnalysisProfile? Analysis);

internal sealed record LibraryProfile(
    string? LibraryPrepKit,
    string? TargetEnrichmentKit,
    string? PanelId,
    bool? PcrFree,
    bool? UmiPresent,
    int? IntendedInsertSize,
    int? IntendedReadLength,
    bool HasInputAmount);

/// <param name="Files">The analysis's output files, by role and format only.</param>
internal sealed record AnalysisProfile(
    string AnalysisType,
    string PipelineName,
    string? ReferenceGenome,
    bool HasQualityMetrics,
    IReadOnlyList<FileKind> Files);

internal sealed record FileKind(string Role, string? Format);

/// <summary>A row of the panel table, whole except its target-regions file reference.</summary>
internal sealed record PanelRow(
    string PanelId,
    string Name,
    string? Abbreviation,
    string? Vendor,
    string? Assay,
    string? CatalogueCode,
    DateOnly? AvailableFrom,
    DateOnly? AvailableTo,
    IReadOnlyList<string> Genes);
