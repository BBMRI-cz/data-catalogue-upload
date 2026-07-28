using SequencingApi.Domain.Common;
using SequencingApi.Domain.Runs;
using SequencingApi.Infrastructure.Persistence.Entities;

namespace SequencingApi.Infrastructure.Mapping;

/// <summary>Maps between <see cref="SequencingRunAggregate"/> and its EF persistence row.</summary>
internal static class SequencingRunMapper
{
    public static SequencingRunEntity ToEntity(SequencingRunAggregate run) => new()
    {
        RunId = run.Id.Value,
        RunNumber = run.RunNumber,
        InstrumentModel = run.InstrumentModel,
        InstrumentId = run.InstrumentId,
        Platform = run.Platform,
        SourceClass = run.SourceClass,
        RunDate = run.RunDate,
        FlowcellId = run.FlowcellId,
        LaneCount = run.LaneCount,
        // Position is assigned here, not by the database: the read structure is the ordered sequence
        // the instrument actually performed, and which template read is R1 rather than R2 is decided
        // by nothing but that order. (The expected-FASTQ count doesn't care - it only counts the
        // non-index reads - but anything reading the structure back does.)
        Reads =
        [
            .. run.Reads.Select((read, position) => new RunReadEntity
            {
                Position = position,
                NumCycles = read.NumCycles,
                IsIndexedRead = read.IsIndexedRead,
            }),
        ],
        Assay = run.Assay,
        Workflow = run.Workflow,
        ExperimentName = run.ExperimentName,
        Chemistry = run.Chemistry,
        ReagentKit = run.ReagentKit,
        PercentageQ30 = run.PercentageQ30,
        ClusterCountPassingFilter = run.ClusterCountPassingFilter,
        PercentageClustersPassingFilter = run.PercentageClustersPassingFilter,
        ClusterDensity = run.ClusterDensity,
        EstimatedYield = run.EstimatedYield,
        CompletionStatus = run.CompletionStatus,
        ErrorDescription = run.ErrorDescription,
    };

    // Reconstitution from trusted persistence intentionally bypasses SequencingRunAggregate.Create.
    public static SequencingRunAggregate ToDomain(SequencingRunEntity row) => new()
    {
        Id = new SequencingRunId(row.RunId),
        RunNumber = row.RunNumber,
        InstrumentModel = row.InstrumentModel,
        InstrumentId = row.InstrumentId,
        Platform = row.Platform,
        SourceClass = row.SourceClass,
        RunDate = row.RunDate,
        FlowcellId = row.FlowcellId,
        LaneCount = row.LaneCount,
        Reads =
        [
            .. row.Reads.OrderBy(read => read.Position).Select(read => new ReadDefinition
            {
                NumCycles = read.NumCycles,
                IsIndexedRead = read.IsIndexedRead,
            }),
        ],
        Assay = row.Assay,
        Workflow = row.Workflow,
        ExperimentName = row.ExperimentName,
        Chemistry = row.Chemistry,
        ReagentKit = row.ReagentKit,
        PercentageQ30 = row.PercentageQ30,
        ClusterCountPassingFilter = row.ClusterCountPassingFilter,
        PercentageClustersPassingFilter = row.PercentageClustersPassingFilter,
        ClusterDensity = row.ClusterDensity,
        EstimatedYield = row.EstimatedYield,
        CompletionStatus = row.CompletionStatus,
        ErrorDescription = row.ErrorDescription,
    };
}
