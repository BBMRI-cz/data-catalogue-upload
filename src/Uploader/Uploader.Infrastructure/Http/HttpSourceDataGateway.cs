using System.Text.Json;
using System.Text.Json.Serialization;
using Uploader.Application.Abstractions;
using Uploader.Application.Dtos;

namespace Uploader.Infrastructure.Http;

/// <summary>Reads the four upstream source APIs over HTTP (biobank, radiology, sequencing, WSI).</summary>
internal sealed class HttpSourceDataGateway(IHttpClientFactory httpClientFactory) : ISourceDataGateway
{
    public const string BiobankClient = "source-biobank";
    public const string RadiologyClient = "source-radiology";
    public const string SequencingClient = "source-sequencing";
    public const string WsiClient = "source-wsi";

    private static readonly JsonSerializerOptions SourceOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<IReadOnlyList<PatientDto>> FetchPatientsAsync(CancellationToken cancellationToken) =>
        await GetAsync<List<PatientDto>>(BiobankClient, "/patients", cancellationToken) ?? [];

    public async Task<IReadOnlyList<ImagingStudyDto>> FetchRadiologyAsync(
        IReadOnlyList<string> accessionNumbers,
        CancellationToken cancellationToken)
    {
        if (accessionNumbers.Count == 0)
        {
            return [];
        }

        var path = "/radiology?accession_numbers=" + Uri.EscapeDataString(string.Join(",", accessionNumbers));
        return await GetAsync<List<ImagingStudyDto>>(RadiologyClient, path, cancellationToken) ?? [];
    }

    public Task<SequencingDto?> FetchSequencingAsync(string predictiveNumber, CancellationToken cancellationToken)
    {
        var path = "/sequencing?predictive_number=" + Uri.EscapeDataString(predictiveNumber);
        return GetAsync<SequencingDto>(SequencingClient, path, cancellationToken);
    }

    public Task<WsiDto?> FetchWsiAsync(string biopticNumber, CancellationToken cancellationToken)
    {
        var path = "/slides?bioptic_number=" + Uri.EscapeDataString(biopticNumber);
        return GetAsync<WsiDto>(WsiClient, path, cancellationToken);
    }

    private async Task<T?> GetAsync<T>(string clientName, string path, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(clientName);
        using var response = await client.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(content) ? default : JsonSerializer.Deserialize<T>(content, SourceOptions);
    }
}
