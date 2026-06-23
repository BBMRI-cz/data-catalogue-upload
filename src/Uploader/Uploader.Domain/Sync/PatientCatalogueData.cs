namespace Uploader.Domain;

/// <summary>
/// All catalogue aggregates assembled for one patient in a single run: the
/// <see cref="PatientAggregate"/> plus the separate aggregates that reference it by id. This is the
/// planner's input — a transient grouping, not an aggregate itself.
/// </summary>
public sealed record PatientCatalogueData
{
    public required PatientAggregate Patient { get; init; }
    public IReadOnlyList<SampleAggregate> Samples { get; init; } = [];
    public IReadOnlyList<SequencingAggregate> Sequencings { get; init; } = [];
    public IReadOnlyList<WsiAggregate> Wsis { get; init; } = [];
    public IReadOnlyList<ImagingStudyAggregate> ImagingStudies { get; init; } = [];

    /// <summary>A patient is only uploaded to the catalogue when it has at least one sample.</summary>
    public bool IsUploadEligible => Samples.Count > 0;
}
