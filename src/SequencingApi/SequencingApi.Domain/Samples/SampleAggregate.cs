using ErrorOr;
using SequencingApi.Domain.Common;

namespace SequencingApi.Domain.Samples;

/// <summary>
/// A sequenced sample — the aggregate root, and the unit everything about sequencing is asked for.
/// Gathers every occasion the same sample was put on a sequencer, since a sample is routinely
/// sequenced more than once.
/// </summary>
/// <remarks>
/// Identified by an opaque <c>external_id</c> supplied by the source biobank, qualified by
/// <see cref="IdScheme"/>. The domain never interprets the id: it is a join key, and the sequencing
/// service deliberately holds no patient or clinical data — that is served by the patient API, and
/// <see cref="SubjectRef"/> is only a pointer into it.
/// <para>
/// Runs and panels are referenced by identity, never held: both are shared by many samples and are
/// their own aggregate roots.
/// </para>
/// </remarks>
public sealed class SampleAggregate : AggregateRoot<SampleId>
{
    // Internal (not private) so the persistence mapper can reconstitute loaded rows; the public
    // creation path stays Create. See SequencingApi.Domain InternalsVisibleTo.
    internal SampleAggregate()
    {
    }

    /// <summary>
    /// Which naming system <see cref="Entity{TId}.Id"/> belongs to. The single extension point that
    /// keeps the id opaque: another biobank supplies ids in its own scheme, and nothing downstream
    /// has to assume this one's shape. Kept beside the id rather than folded into it — a composite
    /// identifier string is the very thing a typed id exists to prevent.
    /// </summary>
    public required string IdScheme { get; init; }

    /// <summary>
    /// Opaque pointer to the subject this sample was taken from, resolved by the patient API. Null
    /// when the source carries no such link.
    /// </summary>
    public string? SubjectRef { get; init; }

    /// <summary>
    /// Every run this sample was sequenced in, at most one entry per run. Empty is legal: a sample
    /// can be known without any usable sequencing having been found for it.
    /// </summary>
    public IReadOnlyList<RunSample> RunSamples { get; init; } = [];

    /// <summary>Whether any of this sample's sequencing was analysed, as opposed to being reads only.</summary>
    public bool HasAnalysis => RunSamples.Any(runSample => runSample.HasAnalysis);

    public static ErrorOr<SampleAggregate> Create(
        string externalId,
        string idScheme,
        string? subjectRef = null,
        IReadOnlyList<RunSample>? runSamples = null)
    {
        // Trimmed but not case-folded: the id is opaque, and another scheme's ids may be case-significant.
        if (Normalize.Text(externalId) is not { } cleanExternalId)
        {
            return Error.Validation("Sample.ExternalId", "external_id must not be empty");
        }

        if (Normalize.Lower(idScheme) is not { } cleanIdScheme)
        {
            return Error.Validation("Sample.IdScheme", "id_scheme must not be empty");
        }

        var resolvedRunSamples = runSamples ?? [];

        // A run can be discovered more than once in one source tree — the same run id turns up both
        // under a failed-processing folder and in the organised output, and one run id even lives
        // under two different subtype folders. Without this guard the duplicate would be stored
        // twice and RunSample's run-scoped identity would stop being unique inside the aggregate.
        var seenRunIds = new HashSet<SequencingRunId>();
        foreach (var runSample in resolvedRunSamples)
        {
            if (!seenRunIds.Add(runSample.RunId))
            {
                return Error.Validation(
                    "Sample.RunSamples",
                    $"duplicate run_id in sample runs: {runSample.RunId}");
            }
        }

        return new SampleAggregate
        {
            Id = new SampleId(cleanExternalId),
            IdScheme = cleanIdScheme,
            SubjectRef = Normalize.Text(subjectRef),
            RunSamples = resolvedRunSamples,
        };
    }
}
