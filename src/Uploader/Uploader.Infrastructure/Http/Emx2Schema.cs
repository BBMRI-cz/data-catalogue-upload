using System.Text.Json.Nodes;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace Uploader.Infrastructure.Http;

/// <summary>
/// What the live catalogue schema says about the columns being written, read once per run and
/// applied to every payload on its way out.
/// <para>
/// It does two jobs the records cannot do for themselves. A reference column takes a nested key
/// object rather than a string (<c>{"belongsToPerson":{"personalIdentifier":"..."}}</c>), and an
/// ontology column is a foreign key into a lookup table, so a term that is not already there
/// rejects the whole row, transactionally. Both are described in
/// <c>docs/catalogue-api-contract.md</c>.
/// </para>
/// <para>
/// The column layout is read from EMX2 rather than hard-coded, so a schema that gains a column or
/// changes one from a string to a lookup does not need this class edited. What it cannot absorb is
/// a column being renamed - that is a record change, and the run will say so by dropping the value.
/// </para>
/// </summary>
internal sealed class Emx2Schema
{
    private const string OntologyKey = "name";

    private readonly Emx2Client _client;
    private readonly Dictionary<string, Dictionary<string, Column>> _columns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _terms = new(StringComparer.Ordinal);
    private bool _loaded;

    public Emx2Schema(Emx2Client client) => _client = client;

    private sealed record Column(string Kind, string? RefTable, string RefKey);

    /// <summary>
    /// Rewrites a table's rows into what EMX2 accepts: references become nested key objects, and
    /// ontology values that are not terms are dropped with a warning rather than left to fail the
    /// row. Scalars are untouched.
    /// </summary>
    public async Task<ErrorOr<Success>> PrepareAsync(
        string table,
        IReadOnlyList<JsonNode> rows,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var loaded = await EnsureLoadedAsync(cancellationToken);
        if (loaded.IsError)
        {
            return loaded.Errors;
        }

        if (!_columns.TryGetValue(table, out var columns))
        {
            return Error.Failure("Catalogue.UnknownTable", $"The catalogue has no table named '{table}'.");
        }

        foreach (var row in rows.OfType<JsonObject>())
        {
            foreach (var property in row.ToList())
            {
                if (!columns.TryGetValue(property.Key, out var column))
                {
                    // A column the schema does not have would fail the whole mutation, and one
                    // record drifting out of step should not cost the rest of the row.
                    logger.LogWarning(
                        "Dropping '{Column}': {Table} has no such column in the catalogue",
                        property.Key,
                        table);
                    row.Remove(property.Key);
                    continue;
                }

                var rewritten = await RewriteAsync(table, property.Key, property.Value, column, logger, cancellationToken);
                if (rewritten.IsError)
                {
                    return rewritten.Errors;
                }

                if (rewritten.Value is null)
                {
                    row.Remove(property.Key);
                }
                else
                {
                    row[property.Key] = rewritten.Value;
                }
            }
        }

        return Result.Success;
    }

    /// <summary>Returns the value to store, or null to drop the column.</summary>
    private async Task<ErrorOr<JsonNode?>> RewriteAsync(
        string table,
        string columnName,
        JsonNode? value,
        Column column,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (value is null)
        {
            return Drop;
        }

        switch (column.Kind)
        {
            case "REF":
            case "ONTOLOGY":
                {
                    var text = AsText(value);
                    if (text is null)
                    {
                        return Drop;
                    }

                    var kept = await KeepAsync(table, columnName, text, column, logger, cancellationToken);
                    if (kept.IsError)
                    {
                        return kept.Errors;
                    }

                    return kept.Value
                        ? ErrorOrFactory.From<JsonNode?>(new JsonObject { [column.RefKey] = text })
                        : Drop;
                }

            case "REF_ARRAY":
            case "ONTOLOGY_ARRAY":
                {
                    var wrapped = new JsonArray();
                    foreach (var text in AsTextList(value))
                    {
                        var kept = await KeepAsync(table, columnName, text, column, logger, cancellationToken);
                        if (kept.IsError)
                        {
                            return kept.Errors;
                        }

                        if (kept.Value)
                        {
                            wrapped.Add(new JsonObject { [column.RefKey] = text });
                        }
                    }

                    // An empty list is not the same as an absent column to EMX2's validator, and an
                    // ontology array we could not fill has nothing to say.
                    return wrapped.Count == 0 ? Drop : ErrorOrFactory.From<JsonNode?>(wrapped);
                }

            default:
                return ErrorOrFactory.From<JsonNode?>(value.DeepClone());
        }
    }

    /// <summary>
    /// "Leave this column out." Spelled as a property because <c>ErrorOrFactory.From</c> cannot
    /// tell a bare null apart from its error-list overload.
    /// </summary>
    private static ErrorOr<JsonNode?> Drop
    {
        get
        {
            JsonNode? nothing = null;
            return ErrorOrFactory.From(nothing);
        }
    }

    /// <summary>
    /// Whether a value may be sent. A data reference is trusted - the uploader wrote the row it
    /// points at earlier in the same run - while an ontology term has to exist already, because
    /// nothing creates one.
    /// </summary>
    private async Task<ErrorOr<bool>> KeepAsync(
        string table,
        string columnName,
        string value,
        Column column,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!column.Kind.StartsWith("ONTOLOGY", StringComparison.Ordinal) || column.RefTable is null)
        {
            return true;
        }

        var terms = await TermsAsync(column.RefTable, cancellationToken);
        if (terms.IsError)
        {
            return terms.Errors;
        }

        if (terms.Value.Contains(value))
        {
            return true;
        }

        logger.LogWarning(
            "Dropping {Table}.{Column}: '{Value}' is not a term in the {Ontology} ontology",
            table,
            columnName,
            value,
            column.RefTable);
        return false;
    }

    private async Task<ErrorOr<HashSet<string>>> TermsAsync(string ontology, CancellationToken cancellationToken)
    {
        if (_terms.TryGetValue(ontology, out var cached))
        {
            return cached;
        }

        var answered = await _client.QueryAsync($"{{ {ontology} {{ {OntologyKey} }} }}", cancellationToken);
        if (answered.IsError)
        {
            return answered.Errors;
        }

        var terms = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in answered.Value?[ontology] as JsonArray ?? [])
        {
            if (node?[OntologyKey]?.GetValue<string>() is { } term)
            {
                terms.Add(term);
            }
        }

        _terms[ontology] = terms;
        return terms;
    }

    private async Task<ErrorOr<Success>> EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return Result.Success;
        }

        var answered = await _client.QueryAsync(
            "{ _schema { tables { id columns { id columnType refTableName key } } } }", cancellationToken);
        if (answered.IsError)
        {
            return answered.Errors;
        }

        var tables = answered.Value?["_schema"]?["tables"] as JsonArray ?? [];

        // Two passes: the key column of every table first, because a reference needs to know the
        // key of the table it points at, and a table can be referenced before it is read.
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var table in tables.OfType<JsonObject>())
        {
            if (table["id"]?.GetValue<string>() is { } id && KeyColumn(table) is { } key)
            {
                keys[id] = key;
            }
        }

        foreach (var table in tables.OfType<JsonObject>())
        {
            if (table["id"]?.GetValue<string>() is not { } id)
            {
                continue;
            }

            var columns = new Dictionary<string, Column>(StringComparer.Ordinal);
            foreach (var column in table["columns"] as JsonArray ?? [])
            {
                if (column?["id"]?.GetValue<string>() is not { } name)
                {
                    continue;
                }

                var kind = column["columnType"]?.GetValue<string>() ?? "STRING";
                var refTable = column["refTableName"]?.GetValue<string>();
                var refKey = kind.StartsWith("ONTOLOGY", StringComparison.Ordinal)
                    ? OntologyKey
                    : refTable is not null && keys.TryGetValue(refTable, out var found) ? found : OntologyKey;

                columns[CamelCase(name)] = new Column(kind, refTable, refKey);
            }

            _columns[id] = columns;
        }

        _loaded = true;
        return Result.Success;
    }

    /// <summary>
    /// The table's primary key, as a GraphQL input field name. EMX2 marks it <c>key = 1</c>: the
    /// UniqueID element for a data table, and <c>name</c> for an ontology.
    /// </summary>
    private static string? KeyColumn(JsonObject table)
    {
        foreach (var column in table["columns"] as JsonArray ?? [])
        {
            if (column?["key"]?.GetValue<int?>() == 1 && column["id"]?.GetValue<string>() is { } name)
            {
                return CamelCase(name);
            }
        }

        return null;
    }

    /// <summary>
    /// EMX2's schema metadata is PascalCase while its GraphQL input fields are camelCase, and the
    /// records are serialized camelCase, so this is what makes the two line up.
    /// </summary>
    private static string CamelCase(string name) =>
        name.Length == 0 || char.IsLower(name[0]) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    private static string? AsText(JsonNode node) =>
        node.GetValueKind() == System.Text.Json.JsonValueKind.String ? node.GetValue<string>() : null;

    private static IEnumerable<string> AsTextList(JsonNode node) =>
        node is JsonArray array
            ? array.Select(element => element is null ? null : AsText(element)).OfType<string>()
            : AsText(node) is { } single ? [single] : [];
}
