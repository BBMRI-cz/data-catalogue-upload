using System.Text.Json.Nodes;
using Uploader.Application.Abstractions;

namespace Uploader.Infrastructure.Http;

/// <summary>Reads the four upstream source APIs over HTTP (biobank, radiology, sequencing, WSI).</summary>
internal sealed class HttpSourceDataGateway(IHttpClientFactory httpClientFactory) : ISourceDataGateway
{
    public const string BiobankClient = "source-biobank";
    public const string RadiologyClient = "source-radiology";
    public const string SequencingClient = "source-sequencing";
    public const string WsiClient = "source-wsi";

    public async Task<IReadOnlyList<JsonObject>> FetchPatientsAsync(CancellationToken cancellationToken)
    {
        var node = await GetAsync(BiobankClient, "/patients", cancellationToken);
        return ToObjectList(node);
    }

    public async Task<IReadOnlyList<JsonObject>> FetchRadiologyAsync(
        IReadOnlyList<string> accessionNumbers,
        CancellationToken cancellationToken)
    {
        if (accessionNumbers.Count == 0)
        {
            return [];
        }

        var path = "/radiology?accession_numbers=" + Uri.EscapeDataString(string.Join(",", accessionNumbers));
        var node = await GetAsync(RadiologyClient, path, cancellationToken);
        return ToObjectList(node);
    }

    public async Task<JsonObject?> FetchSequencingAsync(string predictiveNumber, CancellationToken cancellationToken)
    {
        var path = "/sequencing?predictive_number=" + Uri.EscapeDataString(predictiveNumber);
        return await GetAsync(SequencingClient, path, cancellationToken) as JsonObject;
    }

    public async Task<JsonObject?> FetchWsiAsync(string biopticNumber, CancellationToken cancellationToken)
    {
        var path = "/slides?bioptic_number=" + Uri.EscapeDataString(biopticNumber);
        return await GetAsync(WsiClient, path, cancellationToken) as JsonObject;
    }

    private async Task<JsonNode?> GetAsync(string clientName, string path, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(clientName);
        using var response = await client.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(content) ? null : JsonNode.Parse(content);
    }

    private static IReadOnlyList<JsonObject> ToObjectList(JsonNode? node) =>
        node is JsonArray array ? array.OfType<JsonObject>().ToList() : [];
}
