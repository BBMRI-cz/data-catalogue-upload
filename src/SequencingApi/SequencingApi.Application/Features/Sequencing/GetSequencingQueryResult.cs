using SequencingApi.Domain.Panels;
using SequencingApi.Domain.Runs;
using SequencingApi.Domain.Samples;

namespace SequencingApi.Application.Features.Sequencing;

/// <summary>
/// Everything the service knows about one predictive number: the matching samples plus the runs and
/// panels they reference.
/// </summary>
/// <remarks>
/// Three flat lists rather than one stitched-together graph, because the aggregate roots reference
/// each other by identity only — joining them is a projection decision, and projection belongs to
/// whoever is rendering, not here. Runs and panels are the ones actually referenced by these
/// samples, never the whole corpus, and a reference with nothing behind it is simply absent.
/// </remarks>
/// <param name="Samples">The samples carrying the predictive number; empty when none does.</param>
/// <param name="Runs">The known runs among those the samples were sequenced in.</param>
/// <param name="Panels">The known panels among those the samples' libraries were enriched for.</param>
public sealed record GetSequencingQueryResult(
    IReadOnlyList<SampleAggregate> Samples,
    IReadOnlyList<SequencingRunAggregate> Runs,
    IReadOnlyList<PanelAggregate> Panels);
