using System.Text.Json.Nodes;

namespace Uploader.Application.Json;

/// <summary>
/// Helpers for reading the raw JSON payloads returned by the source gateways — the C# home of
/// the Python <c>domain.utils</c> helpers plus typed accessors that replace <c>dict.get(...)</c>.
/// </summary>
internal static class RawJson
{
    public static IReadOnlyList<JsonNode?> AsList(JsonNode? value) => value switch
    {
        null => [],
        JsonArray array => [.. array],
        _ => [value],
    };

    public static JsonObject? FirstDict(JsonNode? value) => value switch
    {
        JsonObject obj => obj,
        JsonArray array => array.OfType<JsonObject>().FirstOrDefault(),
        _ => null,
    };

    public static bool HasAnyKeys(JsonObject? payload, params string[] keys)
    {
        if (payload is null)
        {
            return false;
        }

        foreach (var key in keys)
        {
            var value = payload[key];
            if (value is null)
            {
                continue;
            }

            if (value is JsonValue stringValue
                && stringValue.TryGetValue(out string? text)
                && text.Length == 0)
            {
                continue;
            }

            if (value is JsonArray array && array.Count == 0)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    public static string ResolveSourceId(JsonObject payload, string? stableFallback = null)
    {
        var node = payload["source_id"] ?? payload["id"];
        return node is not null ? ValueToString(node) : stableFallback ?? "unknown";
    }

    public static string ValueToString(JsonNode node) =>
        node is JsonValue value && value.TryGetValue(out string? text) ? text : node.ToString();

    public static string RequireString(JsonObject payload, string key)
    {
        var node = payload[key]
            ?? throw new InvalidOperationException($"missing required key '{key}'");
        return ValueToString(node);
    }

    public static string? GetString(this JsonObject? payload, string key) =>
        payload?[key] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    public static int? GetInt(this JsonObject? payload, string key) =>
        payload?[key] is JsonValue value && value.TryGetValue(out int result) ? result : null;

    public static double? GetDouble(this JsonObject? payload, string key) =>
        payload?[key] is JsonValue value && value.TryGetValue(out double result) ? result : null;

    public static bool? GetBool(this JsonObject? payload, string key) =>
        payload?[key] is JsonValue value && value.TryGetValue(out bool result) ? result : null;

    public static IReadOnlyList<string>? GetStringList(this JsonObject? payload, string key)
    {
        if (payload?[key] is not JsonArray array)
        {
            return null;
        }

        var items = new List<string>();
        foreach (var element in array)
        {
            if (element is JsonValue value && value.TryGetValue(out string? text))
            {
                items.Add(text);
            }
        }

        return items;
    }

    public static JsonObject? GetObject(this JsonObject? payload, string key) =>
        payload is null ? null : FirstDict(payload[key]);
}
