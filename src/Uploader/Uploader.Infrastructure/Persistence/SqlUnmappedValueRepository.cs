using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Uploader.Application.Abstractions;
using Uploader.Application.Mapping;
using Uploader.Infrastructure.Persistence.Entities;

namespace Uploader.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IUnmappedValueRepository"/>.</summary>
internal sealed class SqlUnmappedValueRepository : IUnmappedValueRepository
{
    private static readonly JsonSerializerOptions ContextJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly UploaderDbContext _context;
    private readonly TimeProvider _timeProvider;

    public SqlUnmappedValueRepository(UploaderDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public Task ReplaceAsync(
        string runId,
        IReadOnlyDictionary<UnmappedValue, int> misses,
        CancellationToken cancellationToken) =>
        WriteAsync(runId, misses, removeTheRest: true, cancellationToken);

    public Task MergeAsync(
        string runId,
        IReadOnlyDictionary<UnmappedValue, int> misses,
        CancellationToken cancellationToken) =>
        WriteAsync(runId, misses, removeTheRest: false, cancellationToken);

    public async Task<IReadOnlyList<UnmappedListEntry>> GetAllAsync(CancellationToken cancellationToken)
    {
        var entries = await _context.UnmappedValues.AsNoTracking().ToListAsync(cancellationToken);
        return [.. entries.Select(ToEntry)];
    }

    /// <summary>
    /// The list is a few hundred rows at most - one per distinct value nobody has mapped - so it is read
    /// whole and written back in one save, which is also one transaction: a reader never sees a list
    /// half replaced.
    /// </summary>
    private async Task WriteAsync(
        string runId,
        IReadOnlyDictionary<UnmappedValue, int> misses,
        bool removeTheRest,
        CancellationToken cancellationToken)
    {
        var listed = await _context.UnmappedValues.ToDictionaryAsync(
            entity => (entity.Field, entity.Value, entity.Context), cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var met = new HashSet<(string Field, string Value, string Context)>();

        foreach (var (miss, rows) in misses)
        {
            (string Field, string Value, string Context) key =
                (miss.Field.ToString(), miss.Value, WriteContext(miss.Context));
            met.Add(key);

            if (listed.TryGetValue(key, out var entity))
            {
                entity.Count = rows;
                entity.LastRunId = runId;
                continue;
            }

            _context.UnmappedValues.Add(new UnmappedValueEntity
            {
                Field = key.Field,
                Value = key.Value,
                Context = key.Context,
                Count = rows,
                FirstSeenAt = now,
                LastRunId = runId,
            });
        }

        if (removeTheRest)
        {
            _context.UnmappedValues.RemoveRange(
                listed.Where(pair => !met.Contains(pair.Key)).Select(pair => pair.Value));
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static UnmappedListEntry ToEntry(UnmappedValueEntity entity)
    {
        // A field is stored as its Table.Column name; a table id has no dot in it.
        var field = entity.Field.Split('.', 2);
        return new UnmappedListEntry(
            new OntologyField(field[0], field[1]),
            entity.Value,
            ReadContext(entity.Context),
            entity.Count,
            entity.FirstSeenAt);
    }

    /// <summary>
    /// The context as the keys that are set, in a fixed order, so one context is always one string -
    /// it is part of the key. The empty context is <c>{}</c>.
    /// </summary>
    private static string WriteContext(TermContext context) =>
        JsonSerializer.Serialize(new ContextColumn(context.SampleType, context.Label), ContextJson);

    /// <summary>
    /// The material-code context is the only one with keys, and a material context with none set is
    /// the empty one, so this reads back exactly the context that was written.
    /// </summary>
    private static TermContext ReadContext(string json)
    {
        var column = JsonSerializer.Deserialize<ContextColumn>(json, ContextJson)
            ?? throw new InvalidOperationException($"Unreadable unmapped-value context: {json}");
        return column is { SampleType: null, Label: null }
            ? TermContext.Empty
            : TermContext.Material(column.SampleType, column.Label);
    }

    private sealed record ContextColumn(string? SampleType, string? Label);
}
