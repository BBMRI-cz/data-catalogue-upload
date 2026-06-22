using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ErrorOr;
using Uploader.Domain;

namespace Uploader.Infrastructure.Http;

/// <summary>Per-entity catalogue gateway. Failures are returned as <see cref="Error"/>s, not thrown.</summary>
internal sealed class HttpCatalogueGateway(IHttpClientFactory httpClientFactory) : Application.Abstractions.ICatalogueGateway
{
    public const string CatalogueClient = "catalogue";

    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public Task<ErrorOr<string>> UpsertPatientAsync(
        string patientId,
        Personal? personal,
        Clinical? clinical,
        CancellationToken cancellationToken) =>
        PostAsync(
            "/patients/upsert",
            new { external_id = patientId, personal, clinical },
            patientId,
            cancellationToken);

    public Task<ErrorOr<string>> UpsertSampleAsync(Sample sample, string patientId, CancellationToken cancellationToken) =>
        PostAsync(
            "/samples/upsert",
            new
            {
                external_id = sample.SampleId,
                patient_id = patientId,
                predictive_number = sample.PredictiveNumber,
                bioptic_number = sample.BiopticNumber,
                material = sample.Material,
            },
            sample.SampleId,
            cancellationToken);

    public Task<ErrorOr<string>> UpsertSequencingAsync(
        IReadOnlyList<SequencingEntry> sequencing,
        string sampleId,
        CancellationToken cancellationToken)
    {
        var fallback = sequencing.Count > 0 ? sequencing[0].PredictiveNumber : sampleId;
        return PostAsync(
            "/sequencing/upsert",
            new { sample_id = sampleId, entries = sequencing },
            fallback,
            cancellationToken);
    }

    public Task<ErrorOr<string>> UpsertWsiAsync(WsiData wsi, string sampleId, CancellationToken cancellationToken) =>
        PostAsync(
            "/wsi/upsert",
            new { sample_id = sampleId, wsi },
            sampleId,
            cancellationToken);

    public Task<ErrorOr<string>> UpsertImagingStudyAsync(
        ImagingStudy study,
        string patientId,
        CancellationToken cancellationToken) =>
        PostAsync(
            "/imaging-studies/upsert",
            new { patient_id = patientId, imaging_study = study },
            study.AccessionNumber,
            cancellationToken);

    public async Task<ErrorOr<Deleted>> DeleteAsync(
        string entityType,
        string entityKey,
        string? remoteId,
        CancellationToken cancellationToken)
    {
        var targetId = string.IsNullOrEmpty(remoteId) ? entityKey : remoteId;
        try
        {
            var client = httpClientFactory.CreateClient(CatalogueClient);
            using var response = await client.DeleteAsync(
                $"/{entityType}/{Uri.EscapeDataString(targetId)}", cancellationToken);
            response.EnsureSuccessStatusCode();
            return Result.Deleted;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return Error.Failure(description: exception.Message);
        }
    }

    private async Task<ErrorOr<string>> PostAsync(
        string path,
        object payload,
        string fallback,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(CatalogueClient);
            var json = JsonSerializer.Serialize(payload, PayloadOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(path, content, cancellationToken);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return ExtractRemoteId(body, fallback);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return Error.Failure(description: exception.Message);
        }
    }

    private static string ExtractRemoteId(string body, string fallback)
    {
        if (string.IsNullOrWhiteSpace(body) || JsonNode.Parse(body) is not JsonObject obj)
        {
            return fallback;
        }

        var node = obj["id"] ?? obj["external_id"];
        if (node is JsonValue value && value.TryGetValue(out string? text) && !string.IsNullOrEmpty(text))
        {
            return text;
        }

        return fallback;
    }
}
