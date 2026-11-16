using System.Text.Json;
using TrainingSeeder.Vocabulary;

namespace TrainingSeeder.Generation;

/// <summary>Reads a vocabulary section as something to draw from.</summary>
internal static class Draws
{
    public static Weighted<string?> Strings(this IReadOnlyDictionary<string, IReadOnlyList<ValueCount>> section, string field) =>
        new(section.Field(field).Select(count => (count.Value.ValueKind == JsonValueKind.Null ? null : count.Value.GetString(), count.Count)));

    public static Weighted<bool?> Booleans(this IReadOnlyDictionary<string, IReadOnlyList<ValueCount>> section, string field) =>
        new(section.Field(field).Select(count => (count.Value.ValueKind == JsonValueKind.Null ? (bool?)null : count.Value.GetBoolean(), count.Count)));

    /// <summary>A histogram - k and how many parents have k children - with the counts adjusted.</summary>
    public static Weighted<int> Histogram(
        this IReadOnlyDictionary<string, IReadOnlyList<ValueCount>> section,
        string field,
        Func<int, long, long>? adjust = null)
    {
        var histogram = new Weighted<int>(section.Field(field).Select(count =>
        {
            var k = count.Value.GetInt32();
            return (k, adjust?.Invoke(k, count.Count) ?? count.Count);
        }));
        return histogram.IsEmpty ? new Weighted<int>([(0, 1)]) : histogram;
    }

    /// <summary>How many rows hold <paramref name="value"/> in <paramref name="field"/>.</summary>
    public static long CountOf(this IReadOnlyDictionary<string, IReadOnlyList<ValueCount>> section, string field, bool value) =>
        section.Field(field)
            .Where(count => count.Value.ValueKind is JsonValueKind.True or JsonValueKind.False && count.Value.GetBoolean() == value)
            .Sum(count => count.Count);

    public static T Field<T>(this IReadOnlyDictionary<string, T> section, string field) =>
        section.TryGetValue(field, out var value)
            ? value
            : throw new InvalidOperationException($"The vocabulary has no '{field}'; re-run export-vocabulary.sql.");
}
