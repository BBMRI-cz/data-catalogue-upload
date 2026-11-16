using System.Globalization;
using SequencingApi.Domain;
using SequencingApi.Domain.Common;
using SequencingApi.Domain.Panels;
using SequencingApi.Domain.Runs;
using SequencingApi.Domain.Samples;
using TrainingSeeder.Vocabulary;

namespace TrainingSeeder.Generation;

/// <summary>
/// Builds the sequencing side through the domain factories: the real panel table, runs drawn whole
/// from the real run configurations, and samples whose predictive numbers come from the biobank's,
/// so the join between the two APIs finds data.
/// </summary>
internal sealed class SequencingGenerator
{
    internal const string IdScheme = "mmci_predictive";

    // Edge cases counts cannot measure, at a fixed share. Every Nth sample or case rather than a
    // chance of one in N, so even the smallest dataset has them - the first sample, cases 1 and 2.
    internal const int ResequencedEvery = 20;
    internal const int OtherFormsEvery = 10;

    // Run dates are not exported. Each run gets one inside its panel's availability, within this span.
    private static readonly DateOnly FirstRunDate = new(2015, 1, 1);
    private static readonly DateOnly LastRunDate = new(2026, 10, 31);

    // A run's sample count varies this far either side of its configuration's real mean.
    private const double RunSizeSpread = 0.25;

    // The only read-file format the export holds.
    private const string FastqFormat = "fastq.gz";

    private readonly SequencingVocabulary _vocabulary;
    private readonly SeededRandom _random;
    private readonly UniqueNumbers _predictiveNumbers;
    private readonly Dictionary<string, PanelRow> _panels;
    private readonly Dictionary<int, Weighted<RunSampleProfile>> _runSampleProfiles;
    private readonly Weighted<int> _resequencedRuns;
    private readonly Weighted<int> _caseSize;

    public SequencingGenerator(SequencingVocabulary vocabulary, SeededRandom random, UniqueNumbers predictiveNumbers)
    {
        _vocabulary = vocabulary;
        _random = random;
        _predictiveNumbers = predictiveNumbers;
        _panels = vocabulary.Panels.ToDictionary(panel => panel.PanelId);
        _runSampleProfiles = vocabulary.RunSampleProfiles
            .GroupBy(profile => profile.RunProfile)
            .ToDictionary(group => group.Key, group => new Weighted<RunSampleProfile>(group.Select(p => (p.Profile, p.Count))));
        var resequenced = vocabulary.PerParent.Histogram("sample.run_samples").Where(runs => runs > 1);
        _resequencedRuns = resequenced.IsEmpty ? new Weighted<int>([(2, 1)]) : resequenced;
        _caseSize = vocabulary.PerParent.Histogram("predictive_key.samples");
    }

    /// <param name="scale">The dataset's size relative to the real one.</param>
    /// <param name="biobankNumbers">The patients' predictive numbers, handed out to sequencing cases first.</param>
    public SequencingDataset Generate(double scale, IReadOnlyList<PredictiveNumber> biobankNumbers)
    {
        var panels = _vocabulary.Panels.Select(ToPanel).ToList();
        var plans = PlanRuns(scale);
        NumberRuns(plans);
        var runs = plans.Select(ToRun).ToList();

        var slots = plans.SelectMany(plan => plan.Slots).ToArray();
        _random.Shuffle(slots);
        var cases = GroupIntoCases(GroupIntoSamples(slots));
        var numbers = AssignNumbers(cases, biobankNumbers);

        var samples = new List<SampleAggregate>(slots.Length);
        for (var caseIndex = 0; caseIndex < cases.Count; caseIndex++)
        {
            foreach (var sample in cases[caseIndex].Samples)
            {
                samples.Add(ToSample(sample, numbers[caseIndex], caseIndex));
            }
        }

        return new SequencingDataset(panels, runs, samples);
    }

    // A predictive number belongs to a case, which can span several sample folders.
    private List<Case> GroupIntoCases(List<List<Slot>> samples)
    {
        var cases = new List<Case>();
        for (var next = 0; next < samples.Count;)
        {
            var size = Math.Clamp(_caseSize.Draw(_random), 1, samples.Count - next);
            var members = samples.GetRange(next, size);
            cases.Add(new Case(members, members.SelectMany(slots => slots).Min(slot => slot.Run.Date).Year));
            next += size;
        }

        return cases;
    }

    // Every number a patient carries goes to a case, so the join always finds sequencing - the real
    // data has more cases than biobank numbers. A case takes one from the year of its first run or
    // up to two years before, as a surgery precedes its sequencing; numbers left after that go to
    // the first cases without one; the remaining cases get numbers the biobank does not know.
    private PredictiveNumber[] AssignNumbers(List<Case> cases, IReadOnlyList<PredictiveNumber> biobankNumbers)
    {
        const int YearsBeforeRun = 2;

        var pool = biobankNumbers.ToArray();
        _random.Shuffle(pool);
        var byYear = pool.GroupBy(number => number.Year).ToDictionary(group => group.Key, group => new Queue<PredictiveNumber>(group));
        var assigned = new PredictiveNumber?[cases.Count];

        for (var i = 0; i < cases.Count; i++)
        {
            for (var year = cases[i].FirstRunYear; year >= cases[i].FirstRunYear - YearsBeforeRun && assigned[i] is null; year--)
            {
                if (byYear.TryGetValue(year, out var numbers) && numbers.Count > 0)
                {
                    assigned[i] = numbers.Dequeue();
                }
            }
        }

        var leftover = byYear.OrderBy(group => group.Key).SelectMany(group => group.Value);
        using (var unassigned = Enumerable.Range(0, cases.Count).Where(i => assigned[i] is null).ToList().GetEnumerator())
        {
            foreach (var number in leftover)
            {
                if (!unassigned.MoveNext())
                {
                    break;
                }

                assigned[unassigned.Current] = number;
            }
        }

        return [.. assigned.Select((number, i) => number ?? NewPredictive(cases[i].FirstRunYear))];
    }

    private static PanelAggregate ToPanel(PanelRow row) =>
        Generated.Valid(PanelAggregate.Create(
            row.PanelId,
            row.Name,
            row.Abbreviation,
            row.Vendor,
            row.Assay,
            row.CatalogueCode,
            row.Genes,
            targetRegionsRef: null,
            row.AvailableFrom,
            row.AvailableTo));

    private List<RunPlan> PlanRuns(double scale)
    {
        // The most common configuration of each source class goes first, so even the smallest dataset
        // has a NextSeq run, a MiSeq run and a MammaPrint run. Profiles are listed most common first.
        var runs = _vocabulary.RunProfiles
            .GroupBy(profile => profile.Profile.SourceClass)
            .Select(group => group.First())
            .ToList();
        var profiles = new Weighted<RunProfileCount>(_vocabulary.RunProfiles.Select(profile => (profile, profile.Count)));
        var count = Math.Max(runs.Count, (int)Math.Round(_vocabulary.Runs * scale));
        while (runs.Count < count)
        {
            runs.Add(profiles.Draw(_random));
        }

        return runs.Select(Plan).ToList();
    }

    private RunPlan Plan(RunProfileCount run)
    {
        if (!_runSampleProfiles.TryGetValue(run.Id, out var candidates))
        {
            return new RunPlan(run.Profile, _random.Date(FirstRunDate, LastRunDate));
        }

        // The first sample's panel decides when the run happened; the rest are drawn from the set-ups
        // whose panels were available then.
        var lead = candidates.Draw(_random);
        var (from, to) = Availability(lead);
        var plan = new RunPlan(run.Profile, _random.Date(from, to));
        var available = candidates.Where(profile => IsAvailable(profile, plan.Date));
        var mean = (double)candidates.Total / run.Count;
        var size = Math.Max(1, (int)Math.Round(mean * (1 - RunSizeSpread + (2 * RunSizeSpread * _random.Fraction()))));

        plan.Slots.Add(new Slot(plan, lead, 1));
        for (var index = 2; index <= size; index++)
        {
            plan.Slots.Add(new Slot(plan, available.IsEmpty ? lead : available.Draw(_random), index));
        }

        return plan;
    }

    private (DateOnly From, DateOnly To) Availability(RunSampleProfile profile)
    {
        if (profile.Library?.PanelId is { } id && _panels.TryGetValue(id, out var panel))
        {
            var from = Later(panel.AvailableFrom ?? FirstRunDate, FirstRunDate);
            var to = Earlier(panel.AvailableTo ?? LastRunDate, LastRunDate);
            if (from <= to)
            {
                return (from, to);
            }
        }

        return (FirstRunDate, LastRunDate);
    }

    private bool IsAvailable(RunSampleProfile profile, DateOnly date) =>
        profile.Library?.PanelId is not { } id
        || !_panels.TryGetValue(id, out var panel)
        || ((panel.AvailableFrom is null || panel.AvailableFrom <= date) && (panel.AvailableTo is null || date <= panel.AvailableTo));

    // Run numbers count up per instrument in date order, as the instrument numbers them.
    private void NumberRuns(List<RunPlan> plans)
    {
        foreach (var instrument in plans.GroupBy(plan => plan.Profile.InstrumentId ?? "UNKNOWN"))
        {
            var number = _random.Between(1, 99);
            foreach (var plan in instrument.OrderBy(plan => plan.Date))
            {
                plan.Number = number;
                number += _random.Between(1, 3);
                plan.Flowcell = plan.IsNextSeq
                    ? Invariant($"AH{_random.Characters(4)}BGX{_random.Characters(1)}")
                    : Invariant($"000000000-{_random.Characters(5)}");
                plan.Id = Invariant($"{plan.Date:yyMMdd}_{instrument.Key}_{plan.Number:0000}_{plan.Flowcell}");
            }
        }
    }

    private SequencingRunAggregate ToRun(RunPlan plan)
    {
        var profile = plan.Profile;
        var ranges = _vocabulary.Ranges;
        return Generated.Valid(SequencingRunAggregate.Create(
            runId: plan.Id,
            runNumber: plan.Number,
            instrumentModel: profile.InstrumentModel,
            instrumentId: profile.InstrumentId,
            platform: profile.Platform,
            sourceClass: profile.SourceClass,
            runDate: plan.Date,
            flowcellId: plan.Flowcell,
            laneCount: profile.LaneCount,
            reads: plan.Reads,
            assay: profile.Assay,
            workflow: profile.Workflow,
            experimentName: ExperimentName(plan),
            chemistry: profile.Chemistry,
            reagentKit: profile.ReagentKit,
            percentageQ30: profile.HasPercentageQ30
                ? Math.Round(_random.AroundMedian(ranges.Field("sequencing_run.percentage_q30")), 1)
                : null,
            clusterCountPassingFilter: profile.HasClusterCount
                ? (long)Math.Round(_random.AroundMedian(ranges.Field("sequencing_run.cluster_count_passing_filter")))
                : null,
            percentageClustersPassingFilter: profile.HasClusterStats
                ? Math.Round(_random.AroundMedian(ranges.Field("sequencing_run.percentage_clusters_passing_filter")), 5)
                : null,
            clusterDensity: profile.HasClusterStats
                ? Math.Round(_random.AroundMedian(ranges.Field("sequencing_run.cluster_density")), 6)
                : null,
            estimatedYield: profile.HasClusterStats
                ? Math.Round(_random.AroundMedian(ranges.Field("sequencing_run.estimated_yield")), 6)
                : null,
            completionStatus: profile.CompletionStatus));
    }

    // Experiment names stay on the server; these follow their look - the panel and the run date -
    // and keep the MP_ prefix the uploader recognises MammaPrint by.
    private string ExperimentName(RunPlan plan)
    {
        if (plan.Profile.MammaPrintName)
        {
            return Invariant($"MP_{plan.Number}");
        }

        var panel = plan.Slots.Count > 0 && plan.Slots[0].Profile.Library?.PanelId is { } id && _panels.TryGetValue(id, out var row)
            ? row.Abbreviation ?? row.Name
            : null;
        var label = (panel ?? plan.Profile.Assay ?? "Run").Replace(' ', '-');
        return Invariant($"{label}_{plan.Date:yyMMdd}");
    }

    private List<List<Slot>> GroupIntoSamples(Slot[] slots)
    {
        var samples = new List<List<Slot>>();
        var taken = new bool[slots.Length];
        for (var i = 0; i < slots.Length; i++)
        {
            if (taken[i])
            {
                continue;
            }

            taken[i] = true;
            var sample = new List<Slot> { slots[i] };
            var runs = samples.Count % ResequencedEvery == 0 ? _resequencedRuns.Draw(_random) : 1;
            for (var j = i + 1; j < slots.Length && sample.Count < runs; j++)
            {
                var candidate = slots[j];
                if (!taken[j] && sample.TrueForAll(slot => slot.Run != candidate.Run))
                {
                    taken[j] = true;
                    sample.Add(candidate);
                }
            }

            samples.Add(sample);
        }

        return samples;
    }

    // A case the biobank does not know: its number is from the year of its first run or the one before.
    private PredictiveNumber NewPredictive(int firstRunYear)
    {
        var year = Math.Clamp(firstRunYear - _random.Between(0, 1), BiobankGenerator.FirstYear, BiobankGenerator.LastYear);
        return new PredictiveNumber(year, _predictiveNumbers.Next(_random, year));
    }

    private SampleAggregate ToSample(List<Slot> slots, PredictiveNumber number, int caseIndex)
    {
        var externalId = Invariant($"{IdScheme}_{_random.Guid():D}");
        var rna = Parse<SampleType>(slots[0].Profile.SampleType) == SampleType.Rna;

        // Most cases carry the biobank's form; one in ten a sample sheet's, one in ten the old MiSeq one.
        var predictive = (caseIndex % OtherFormsEvery) switch
        {
            1 => number.SampleSheet(rna),
            2 => number.MiSeq ?? number.Biobank,
            _ => number.Biobank,
        };

        var runSamples = slots.Select(slot => ToRunSample(slot, externalId)).ToList();
        return Generated.Valid(SampleAggregate.Create(externalId, IdScheme, predictive, runSamples));
    }

    private RunSample ToRunSample(Slot slot, string sampleId)
    {
        var profile = slot.Profile;
        var plan = slot.Run;
        var folder = Invariant($"{plan.Folder}/Samples/{sampleId}");

        var library = profile.Library is { } setup
            ? Generated.Valid(LibraryPreparation.Create(
                panelId: setup.PanelId is { } panel ? new PanelId(panel) : null,
                inputAmount: setup.HasInputAmount
                    ? (int)Math.Round(_random.AroundMedian(_vocabulary.Ranges.Field("library_preparation.input_amount")))
                    : null,
                libraryPrepKit: setup.LibraryPrepKit,
                pcrFree: setup.PcrFree,
                targetEnrichmentKit: setup.TargetEnrichmentKit,
                umiPresent: setup.UmiPresent,
                intendedInsertSize: setup.IntendedInsertSize,
                intendedReadLength: setup.IntendedReadLength))
            : null;

        IReadOnlyList<Analysis> analyses = profile.Analysis is { } analysis
            ? [ToAnalysis(analysis, folder, sampleId, profile.Library?.IntendedReadLength)]
            : [];

        return Generated.Valid(RunSample.Create(
            runId: plan.Id,
            sampleIndex: slot.Index,
            sampleType: Parse<SampleType>(profile.SampleType),
            laneCount: profile.LaneCount,
            libraryPreparation: library,
            files: Fastq(profile, plan, folder, sampleId, slot.Index),
            analyses: analyses));
    }

    // Read files named the way the instrument names them, one per read per lane, chunked past that.
    private List<SequencingFile> Fastq(RunSampleProfile profile, RunPlan plan, string folder, string sampleId, int index)
    {
        var reads = Math.Max(1, plan.Reads.Count(read => !read.IsIndexedRead));
        var lanes = Math.Max(1, profile.LaneCount ?? plan.Profile.LaneCount ?? 1);
        var files = new List<SequencingFile>(profile.FastqFiles);
        for (var i = 0; i < profile.FastqFiles; i++)
        {
            var read = (i % reads) + 1;
            var lane = (i / reads % lanes) + 1;
            var chunk = (i / (reads * lanes)) + 1;
            files.Add(Generated.Valid(SequencingFile.Create(
                FileRole.Fastq,
                Invariant($"{folder}/FASTQ/{sampleId}_S{index}_L{lane:000}_R{read}_{chunk:000}.fastq.gz"),
                FastqFormat,
                lane: i < profile.FastqWithoutLane ? null : lane,
                read: read,
                sizeBytes: Size(FileRole.Fastq))));
        }

        return files;
    }

    private Analysis ToAnalysis(AnalysisProfile profile, string folder, string sampleId, int? intendedReadLength)
    {
        var files = profile.Files
            .Select((kind, i) =>
            {
                var role = Enum.Parse<FileRole>(kind.Role, ignoreCase: true);
                return Generated.Valid(SequencingFile.Create(
                    role,
                    Invariant($"{folder}/Analysis/{sampleId}_{kind.Role}_{i + 1}.{kind.Format ?? "dat"}"),
                    kind.Format,
                    sizeBytes: Size(role)));
            })
            .ToList();

        QualityMetrics? quality = null;
        if (profile.HasQualityMetrics)
        {
            var depth = _vocabulary.Ranges.Field("quality_metrics.median_read_depth");
            var length = _vocabulary.Ranges.Field("quality_metrics.observed_read_length");
            int? observed = _random.Chance(length.FilledShare) ? (int)Math.Round(_random.AroundMedian(length)) : null;
            quality = Generated.Valid(QualityMetrics.Create(
                medianReadDepth: _random.Chance(depth.FilledShare) ? Math.Round(_random.AroundMedian(depth), 2) : null,
                // A read never comes back longer than it was set up to be.
                observedReadLength: observed is { } bases && intendedReadLength is { } intended ? Math.Min(bases, intended) : observed));
        }

        return Generated.Valid(Analysis.Create(
            Enum.Parse<AnalysisType>(profile.AnalysisType, ignoreCase: true),
            profile.PipelineName,
            profile.ReferenceGenome,
            files,
            quality));
    }

    private long Size(FileRole role) => role switch
    {
        FileRole.Fastq => _random.Between(20_000_000L, 900_000_000L),
        FileRole.Bam => _random.Between(200_000_000L, 6_000_000_000L),
        FileRole.BamIndex => _random.Between(1_000_000L, 10_000_000L),
        FileRole.Vcf or FileRole.VcfFiltered => _random.Between(50_000L, 20_000_000L),
        _ => _random.Between(10_000L, 5_000_000L),
    };

    private static List<ReadDefinition> ReadsOf(string? structure) =>
        structure is null
            ? []
            : [.. structure.Split(',').Select(read => Generated.Valid(ReadDefinition.Create(
                int.Parse(read.TrimEnd('i'), CultureInfo.InvariantCulture),
                read.EndsWith('i'))))];

    private static T? Parse<T>(string? value)
        where T : struct, Enum =>
        value is null ? null : Enum.Parse<T>(value, ignoreCase: true);

    private static DateOnly Later(DateOnly a, DateOnly b) => a > b ? a : b;

    private static DateOnly Earlier(DateOnly a, DateOnly b) => a < b ? a : b;

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A run being planned; its number and id are given once every run's date is known.</summary>
    private sealed class RunPlan
    {
        public RunPlan(RunProfile profile, DateOnly date)
        {
            Profile = profile;
            Date = date;
            Reads = ReadsOf(profile.ReadStructure);
        }

        public RunProfile Profile { get; }

        public DateOnly Date { get; }

        public List<ReadDefinition> Reads { get; }

        public List<Slot> Slots { get; } = [];

        public int Number { get; set; }

        public string Flowcell { get; set; } = string.Empty;

        public string Id { get; set; } = string.Empty;

        public bool IsNextSeq => Profile.InstrumentModel?.Contains("NextSeq", StringComparison.OrdinalIgnoreCase) == true;

        // Where the organised run tree files the run: by year, then instrument and, for MiSeq, subtype.
        public string Folder => IsNextSeq
            ? Invariant($"{Date.Year}/NextSeq/{Id}")
            : Invariant($"{Date.Year}/MiSEQ/{Profile.SourceClass}/{Id}");
    }

    /// <summary>One sample's place on a run: the run, the sample's set-up there, and its index.</summary>
    private sealed record Slot(RunPlan Run, RunSampleProfile Profile, int Index);

    /// <summary>The sample folders that share one predictive number, each a list of its runs.</summary>
    private sealed record Case(List<List<Slot>> Samples, int FirstRunYear);
}
