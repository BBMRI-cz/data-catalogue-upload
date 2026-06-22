using System.Text.Json.Nodes;
using Uploader.Application.Json;
using Uploader.Domain;

namespace Uploader.Application.Builders;

/// <summary>Builds the sequencing pipeline (entries -> sample prep -> sequencing -> analysis).</summary>
public sealed class SequencingBuilder
{
    public IReadOnlyList<SequencingEntry> BuildSequencingData(string predictiveNumber, JsonObject payload)
    {
        var entriesPayload = RawJson.AsList(payload["sequencing_entries"]);
        if (entriesPayload.Count == 0)
        {
            entriesPayload = [payload];
        }

        var entries = new List<SequencingEntry>();
        foreach (var node in entriesPayload)
        {
            if (node is not JsonObject entry)
            {
                continue;
            }

            entries.Add(new SequencingEntry
            {
                PredictiveNumber = predictiveNumber,
                SourceId = RawJson.ResolveSourceId(entry, predictiveNumber),
                FixedBlockIdentifier = BuildFixedBlockIdentifier(GetOrSelf(entry, "fixed_block")),
                SamplePreparation = BuildSamplePreparation(GetOrSelf(entry, "sample_preparation")),
            });
        }

        return entries;
    }

    private static JsonObject? GetOrSelf(JsonObject entry, string key) =>
        (entry.ContainsKey(key) ? entry[key] : entry) as JsonObject;

    private static string? BuildFixedBlockIdentifier(JsonObject? payload) =>
        payload is null || payload.Count == 0 ? null : payload.GetString("block_identifier");

    private static SamplePreparation? BuildSamplePreparation(JsonObject? payload)
    {
        if (payload is null || payload.Count == 0)
        {
            return null;
        }

        if (!RawJson.HasAnyKeys(
            payload,
            "sampleprep_identifier",
            "belongs_to_material",
            "input_amount",
            "library_preparation_kit",
            "target_enrichment_kit",
            "sequencing_run",
            "sequencing"))
        {
            return null;
        }

        var directRun = RawJson.FirstDict(payload["sequencing_run"] ?? payload["sequencing"]);

        return new SamplePreparation
        {
            SampleprepIdentifier = payload.GetString("sampleprep_identifier"),
            BelongsToMaterial = payload.GetString("belongs_to_material"),
            InputAmount = payload.GetString("input_amount"),
            LibraryPreparationKit = payload.GetString("library_preparation_kit"),
            PcrFree = payload.GetBool("pcr_free"),
            TargetEnrichmentKit = payload.GetString("target_enrichment_kit"),
            FullSequenceGenes = payload.GetStringList("full_sequence_genes"),
            PartialSequenceGenes = payload.GetStringList("partial_sequence_genes"),
            UmisPresent = payload.GetBool("umis_present"),
            IntendedInsertSize = payload.GetInt("intended_insert_size"),
            IntendedReadLength = payload.GetInt("intended_read_length"),
            Sequencing = directRun is null ? null : BuildSequencingRun(directRun),
        };
    }

    private static Sequencing? BuildSequencingRun(JsonObject? payload)
    {
        if (payload is null || payload.Count == 0)
        {
            return null;
        }

        if (!RawJson.HasAnyKeys(
            payload,
            "sequencing_identifier",
            "belongs_to_sample_preparation",
            "sequencing_date",
            "sequencing_platform",
            "sequencing_instrument_model",
            "sequencing_method",
            "analysis"))
        {
            return null;
        }

        var analysis = RawJson.AsList(payload["analysis"])
            .OfType<JsonObject>()
            .Select(BuildAnalysis)
            .ToList();

        return new Sequencing
        {
            SequencingIdentifier = payload.GetString("sequencing_identifier"),
            BelongsToSamplePreparation = payload.GetString("belongs_to_sample_preparation"),
            SequencingDate = payload.GetString("sequencing_date"),
            SequencingPlatform = payload.GetString("sequencing_platform"),
            SequencingInstrumentModel = payload.GetString("sequencing_instrument_model"),
            SequencingMethod = payload.GetString("sequencing_method"),
            MedianReadDepth = payload.GetInt("median_read_depth"),
            ObservedReadLength = payload.GetInt("observed_read_length"),
            ObservedInsertSize = payload.GetInt("observed_insert_size"),
            PercentageQ30 = payload.GetDouble("percentage_q30"),
            PercentageTr20 = payload.GetDouble("percentage_tr20"),
            OtherQualityMetrics = payload.GetString("other_quality_metrics"),
            Analysis = analysis.Count > 0 ? analysis[0] : null,
        };
    }

    private static Analysis BuildAnalysis(JsonObject payload) => new()
    {
        AnalysisIdentifier = payload.GetString("analysis_identifier"),
        BelongsToSequencing = payload.GetString("belongs_to_sequencing"),
        PhysicalDataLocation = payload.GetString("physical_data_location"),
        AbstractDataLocation = payload.GetString("abstract_data_location"),
        DataFormatsStored = payload.GetStringList("data_formats_stored"),
        AlgorithmsUsed = payload.GetStringList("algorithms_used"),
        ReferenceGenomeUsed = payload.GetString("reference_genome_used"),
        BioinformaticProtocolUsed = payload.GetString("bioinformatic_protocol_used"),
        BioinformaticProtocolDeviation = payload.GetString("bioinformatic_protocol_deviation"),
        ReasonForBioinformaticProtocolDeviation = payload.GetString("reason_for_bioinformatic_protocol_deviation"),
        WgsGuidelineFollowed = payload.GetString("wgs_guideline_followed"),
    };
}
