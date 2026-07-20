using SequencingApi.Domain;

namespace SequencingApi.Infrastructure.Persistence.Entities;

/// <summary>EF Core persistence row for the <c>run_sample</c> table (one sample in one run).</summary>
public class RunSampleEntity
{
    /// <summary>Surrogate primary key; the business key is (<see cref="SampleExternalId"/>, <see cref="RunId"/>).</summary>
    public long Id { get; set; }

    public string SampleExternalId { get; set; } = default!;

    /// <summary>
    /// The run this sample was sequenced in. Indexed but deliberately <b>not</b> a foreign key:
    /// runs are a separate aggregate root saved independently, so a sample may legitimately be
    /// stored before its run row exists. Same reasoning for <c>library_preparation.PanelId</c>.
    /// </summary>
    public string RunId { get; set; } = default!;

    public int? SampleIndex { get; set; }
    public SampleType? SampleType { get; set; }
    public int? LaneCount { get; set; }

    /// <summary>How the library was prepared; null when the source did not record it.</summary>
    public LibraryPreparationEntity? LibraryPreparation { get; set; }

    /// <summary>Files produced by sequencing itself; an analysis's own files hang off the analysis.</summary>
    public List<SequencingFileEntity> Files { get; set; } = [];

    public List<AnalysisEntity> Analyses { get; set; } = [];
}
