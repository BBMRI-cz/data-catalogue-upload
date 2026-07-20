using System.Text.Json;
using System.Text.Json.Serialization;

namespace SequencingApi.Infrastructure.DataSource.Mmci;

/// <summary>
/// Reads the pseudonymizer's <c>predictive.json</c> into an <see cref="MmciMappingTable"/>.
/// </summary>
/// <remarks>
/// The mapping directory also holds <c>patient.json</c> and <c>samples.json</c>. Those are patient
/// and sample identity mappings and are deliberately never opened: this service holds no patient
/// data, and the one value it needs from the directory is the real predictive number.
/// </remarks>
internal static class MmciMappingTableReader
{
    /// <summary>The only file in the mapping directory this service reads.</summary>
    public const string FileName = "predictive.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Parse mapping JSON. Returns the table plus the problems found; a malformed document yields the
    /// empty table and a reason, never an exception — the sequencing data is still worth ingesting
    /// without the mapping, it just leaves every subject reference unresolved.
    /// </summary>
    public static (MmciMappingTable Table, IReadOnlyList<string> Problems) Read(string json)
    {
        var problems = new List<string>();

        MmciPredictiveMappingDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<MmciPredictiveMappingDocument>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            return (MmciMappingTable.Empty, [$"malformed mapping json: {exception.Message}"]);
        }

        if (document?.Predictive is not { Count: > 0 } entries)
        {
            return (MmciMappingTable.Empty, ["mapping json has no predictive entries"]);
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.PseudoNumber) || string.IsNullOrWhiteSpace(entry.PredictiveNumber))
            {
                problems.Add("mapping entry missing pseudo_number or predictive_number");
                continue;
            }

            // First wins, and the collision is reported: two real numbers claiming one pseudonymized
            // id means the pseudonymizer produced something ambiguous, and silently taking the last
            // would attach a sample's sequencing to whichever patient happened to be written later.
            if (!map.TryAdd(entry.PseudoNumber.Trim(), entry.PredictiveNumber.Trim()))
            {
                problems.Add($"duplicate pseudo_number in mapping table: {entry.PseudoNumber.Trim()}");
            }
        }

        return (new MmciMappingTable(map), problems);
    }

    /// <summary>The document shape: one object wrapping a <c>predictive</c> array of id pairs.</summary>
    private sealed record MmciPredictiveMappingDocument(
        [property: JsonPropertyName("predictive")] IReadOnlyList<MmciPredictiveMappingEntry>? Predictive);

    private sealed record MmciPredictiveMappingEntry(
        [property: JsonPropertyName("predictive_number")] string? PredictiveNumber,
        [property: JsonPropertyName("pseudo_number")] string? PseudoNumber);
}
