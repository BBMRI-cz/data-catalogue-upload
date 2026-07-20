using SequencingApi.Application.Abstractions.DataSource;
using SequencingApi.Domain.Common;
using SequencingApi.Domain.Panels;

namespace SequencingApi.Application.Abstractions.Repositories;

/// <summary>
/// Persistence port for the <see cref="PanelAggregate"/> root (implemented by the db layer).
/// </summary>
public interface IPanelRepository
{
    /// <summary>
    /// Persist panels idempotently (delete-then-insert per panel id). Returns the panels that
    /// failed to persist so the caller can report them rather than aborting.
    /// </summary>
    Task<IReadOnlyList<RecordReadError>> SavePanelsAsync(
        IReadOnlyList<PanelAggregate> panels,
        CancellationToken cancellationToken);

    /// <summary>Load one panel by id, or null when it is unknown.</summary>
    Task<PanelAggregate?> GetPanelAsync(PanelId id, CancellationToken cancellationToken);
}
