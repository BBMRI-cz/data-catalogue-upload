using System.Text.Json;
using System.Text.Json.Serialization;
using ErrorOr;
using Uploader.Application.Abstractions;
using Uploader.Application.Dtos;
using Uploader.Infrastructure.Configuration;

namespace Uploader.Infrastructure.Http;

/// <summary>
/// Reads the upstream source APIs over HTTP (biobank, radiology, sequencing, WSI).
/// <para>
/// A source whose URL is blank is treated as not deployed: no client is registered for it and
/// nothing here contacts it. A source that is configured but unreachable returns an error rather
/// than throwing, so the run survives it.
/// </para>
/// </summary>
internal sealed class HttpSourceDataGateway : ISourceDataGateway
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

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UploaderOptions _options;

    public HttpSourceDataGateway(IHttpClientFactory httpClientFactory, UploaderOptions options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public async Task<ErrorOr<IReadOnlyList<PatientDto>>> FetchPatientsAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured(_options.BiobankApiUrl))
        {
            return None<PatientDto>();
        }

        var fetched = await GetAsync<List<PatientDto>>(BiobankClient, "biobank", "/patients", cancellationToken);
        if (fetched.IsError)
        {
            return fetched.Errors;
        }

        return Some(fetched.Value);
    }

    public async Task<ErrorOr<IReadOnlyList<ImagingStudyDto>>> FetchRadiologyAsync(
        IReadOnlyList<string> accessionNumbers,
        CancellationToken cancellationToken)
    {
        if (accessionNumbers.Count == 0 || !IsConfigured(_options.RadiologyApiUrl))
        {
            return None<ImagingStudyDto>();
        }

        var path = "/radiology?accession_numbers=" + Uri.EscapeDataString(string.Join(",", accessionNumbers));
        var fetched = await GetAsync<List<ImagingStudyDto>>(RadiologyClient, "radiology", path, cancellationToken);
        if (fetched.IsError)
        {
            return fetched.Errors;
        }

        return Some(fetched.Value);
    }

    public Task<ErrorOr<SequencingDto?>> FetchSequencingAsync(
        string predictiveNumber,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured(_options.SequencingApiUrl))
        {
            return Task.FromResult(Nothing<SequencingDto>());
        }

        var path = "/sequencing?predictive_number=" + Uri.EscapeDataString(predictiveNumber);
        return GetAsync<SequencingDto>(SequencingClient, "sequencing", path, cancellationToken);
    }

    public Task<ErrorOr<WsiDto?>> FetchWsiAsync(string biopticNumber, CancellationToken cancellationToken)
    {
        if (!IsConfigured(_options.WsiApiUrl))
        {
            return Task.FromResult(Nothing<WsiDto>());
        }

        var path = "/slides?bioptic_number=" + Uri.EscapeDataString(biopticNumber);
        return GetAsync<WsiDto>(WsiClient, "wsi", path, cancellationToken);
    }

    /// <summary>A blank URL means the service is not deployed for this biobank.</summary>
    internal static bool IsConfigured(string? baseUrl) => !string.IsNullOrWhiteSpace(baseUrl);

    /// <summary>
    /// Empty answers, spelled out. <c>ErrorOrFactory.From</c> cannot tell a bare <c>[]</c> or
    /// <c>null</c> apart from the error-list overload, so the value type is pinned here once
    /// instead of being cast at every call site.
    /// </summary>
    private static ErrorOr<IReadOnlyList<T>> None<T>() =>
        ErrorOrFactory.From<IReadOnlyList<T>>(Array.Empty<T>());

    private static ErrorOr<IReadOnlyList<T>> Some<T>(List<T>? value) =>
        ErrorOrFactory.From(value is null ? (IReadOnlyList<T>)Array.Empty<T>() : value);

    private static ErrorOr<T?> Nothing<T>()
        where T : class => ErrorOrFactory.From<T?>((T?)null);

    private async Task<ErrorOr<T?>> GetAsync<T>(
        string clientName,
        string sourceName,
        string path,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            var client = _httpClientFactory.CreateClient(clientName);
            using var response = await client.GetAsync(path, cancellationToken);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var value = string.IsNullOrWhiteSpace(content)
                ? default
                : JsonSerializer.Deserialize<T>(content, SourceOptions);
            return ErrorOrFactory.From(value);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return SourceErrors.Unavailable(sourceName, exception.Message);
        }
        catch (JsonException exception)
        {
            return SourceErrors.Invalid(sourceName, exception.Message);
        }
    }
}
