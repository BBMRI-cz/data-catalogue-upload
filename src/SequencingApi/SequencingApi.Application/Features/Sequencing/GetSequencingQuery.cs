using ErrorOr;
using Mediator;
using SequencingApi.Application.Abstractions.Repositories;
using SequencingApi.Domain.Common;

namespace SequencingApi.Application.Features.Sequencing;

/// <summary>
/// The read the uploader arrives with: everything held for one predictive number.
/// </summary>
/// <remarks>
/// This is the <em>real</em> predictive number — the one the patient API serves as a sample's
/// <c>predictive_number</c>, stored here as <c>SampleAggregate.PredictiveNumber</c>. Not the
/// pseudonymized one in the sample id, which the patient API has never seen.
/// </remarks>
public sealed record GetSequencingQuery(string PredictiveNumber) : IQuery<ErrorOr<GetSequencingQueryResult>>;

internal sealed class GetSequencingQueryHandler
    : IQueryHandler<GetSequencingQuery, ErrorOr<GetSequencingQueryResult>>
{
    private readonly ISampleRepository _samples;
    private readonly ISequencingRunRepository _runs;
    private readonly IPanelRepository _panels;

    public GetSequencingQueryHandler(
        ISampleRepository samples,
        ISequencingRunRepository runs,
        IPanelRepository panels)
    {
        _samples = samples;
        _runs = runs;
        _panels = panels;
    }

    public async ValueTask<ErrorOr<GetSequencingQueryResult>> Handle(
        GetSequencingQuery query,
        CancellationToken cancellationToken)
    {
        var samples = await _samples.GetSamplesByPredictiveNumberAsync(query.PredictiveNumber, cancellationToken);

        // No match is a normal answer, not an error: most patients have no sequencing at all, and the
        // uploader asks about every one of them.
        if (samples.Count == 0)
        {
            return new GetSequencingQueryResult([], [], []);
        }

        // Samples hold run and panel identities, never the aggregates themselves, so the referenced
        // ones are fetched here - two round-trips for the whole answer rather than one per reference.
        var runIds = samples
            .SelectMany(sample => sample.RunSamples)
            .Select(runSample => runSample.RunId)
            .Distinct()
            .ToList();

        var panelIds = samples
            .SelectMany(sample => sample.RunSamples)
            .Select(runSample => runSample.LibraryPreparation?.PanelId)
            .OfType<PanelId>()
            .Distinct()
            .ToList();

        var runs = await _runs.GetRunsAsync(runIds, cancellationToken);
        var panels = await _panels.GetPanelsAsync(panelIds, cancellationToken);

        return new GetSequencingQueryResult(samples, runs, panels);
    }
}
