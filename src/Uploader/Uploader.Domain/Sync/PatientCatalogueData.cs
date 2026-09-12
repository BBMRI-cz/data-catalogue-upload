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

    /// <summary>
    /// Whether the sequencing source answered for this patient. A source that did not answer leaves
    /// its aggregates out of the lists above, which looks exactly like the rows having been
    /// withdrawn - and deleting on that reading throws away data over a network blip. The planner
    /// uses this to tell "gone" apart from "not asked".
    /// <para>
    /// Samples need no such flag: they come from the biobank, and a biobank that does not answer
    /// ends the run rather than yielding an empty patient.
    /// </para>
    /// </summary>
    public bool SequencingComplete { get; init; } = true;

    public bool WsiComplete { get; init; } = true;

    public bool ImagingComplete { get; init; } = true;

    /// <summary>
    /// A patient is only uploaded to the catalogue when they consented and have at least one sample.
    /// The consent half is checked here rather than being left to follow from the biobank refusing to
    /// attach samples to a non-consenting patient — an upload permission deserves its own test.
    /// </summary>
    public bool IsUploadEligible => Patient.HasConsent && Samples.Count > 0;
}
