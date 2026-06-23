using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ErrorOr;
using Uploader.Application.Abstractions;
using Uploader.Domain;

namespace Uploader.Infrastructure.Http;

/// <summary>Per-aggregate catalogue gateway. Failures are returned as <see cref="Error"/>s, not thrown.</summary>
internal sealed class HttpCatalogueGateway(IHttpClientFactory httpClientFactory) : ICatalogueGateway
{
    public const string CatalogueClient = "catalogue";

    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new StronglyTypedIdJsonConverterFactory() },
    };

    public Task<ErrorOr<string>> UpsertPatientAsync(PatientAggregate patient, CancellationToken cancellationToken) =>
        PostAsync(
            "/patients/upsert",
            new { external_id = patient.Id.Value, personal = patient.Personal, clinical = patient.Clinical },
            patient.Id.Value,
            cancellationToken);

    public Task<ErrorOr<string>> UpsertSampleAsync(SampleAggregate sample, CancellationToken cancellationToken) =>
        PostAsync(
            "/samples/upsert",
            new
            {
                external_id = sample.Id.Value,
                patient_id = sample.PatientId.Value,
                predictive_number = sample.SequencingId?.Value,
                bioptic_number = sample.WsiId?.Value,
                material = sample.Material,
            },
            sample.Id.Value,
            cancellationToken);

    public Task<ErrorOr<string>> UpsertSequencingAsync(SequencingAggregate sequencing, CancellationToken cancellationToken) =>
        PostAsync(
            "/sequencing/upsert",
            new { external_id = sequencing.Id.Value, sample_id = sequencing.SampleId.Value, entries = sequencing.Entries },
            sequencing.Id.Value,
            cancellationToken);

    public Task<ErrorOr<string>> UpsertWsiAsync(WsiAggregate wsi, CancellationToken cancellationToken) =>
        PostAsync(
            "/wsi/upsert",
            new { external_id = wsi.Id.Value, sample_id = wsi.SampleId.Value, fixed_block = wsi.FixedBlock },
            wsi.Id.Value,
            cancellationToken);

    public Task<ErrorOr<string>> UpsertImagingStudyAsync(ImagingStudyAggregate study, CancellationToken cancellationToken) =>
        PostAsync(
            "/imaging-studies/upsert",
            new { external_id = study.Id.Value, patient_id = study.PatientId.Value, imaging_study = study },
            study.Id.Value,
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
