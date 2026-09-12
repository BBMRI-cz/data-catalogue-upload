using System.Text.Json.Nodes;

namespace Uploader.Infrastructure.Http;

/// <summary>The read-side GraphQL documents, and how to read what they answer with.</summary>
internal static class Emx2Queries
{
    /// <summary>
    /// Every sequencing row hanging off one biospecimen, walked through EMX2's REFBACK columns.
    /// A delete needs these keys and the uploader does not store them - they are minted from the
    /// run tree, so the catalogue is the only place that knows which ones were published.
    /// </summary>
    public const string SequencingSubtree = """
        query Subtree($key: String) {
          SamplePreparation(filter: { belongsToBiospecimen: { biospecimenIdentifier: { equals: [$key] } } }) {
            sampleprepIdentifier
            sequencing_BelongsToSamplePreparation {
              sequencingIdentifier
              analysis_BelongsToSequencing { analysisIdentifier }
            }
          }
        }
        """;

    public sealed record Subtree(
        IReadOnlyList<string> Preparations,
        IReadOnlyList<string> Runs,
        IReadOnlyList<string> Analyses);

    public static Subtree ReadSequencingSubtree(JsonObject? data)
    {
        List<string> preparations = [];
        List<string> runs = [];
        List<string> analyses = [];

        foreach (var preparation in data?[Emx2Tables.SamplePreparation] as JsonArray ?? [])
        {
            Add(preparations, preparation?[Emx2Tables.PreparationKey]);

            foreach (var run in preparation?["sequencing_BelongsToSamplePreparation"] as JsonArray ?? [])
            {
                Add(runs, run?[Emx2Tables.SequencingKey]);

                foreach (var analysis in run?["analysis_BelongsToSequencing"] as JsonArray ?? [])
                {
                    Add(analyses, analysis?[Emx2Tables.AnalysisKey]);
                }
            }
        }

        return new Subtree(preparations, runs, analyses);
    }

    private static void Add(List<string> keys, JsonNode? node)
    {
        if (node?.GetValue<string>() is { Length: > 0 } key)
        {
            keys.Add(key);
        }
    }
}
