using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ErrorOr;
using Uploader.Infrastructure.Configuration;

namespace Uploader.Infrastructure.Http;

/// <summary>
/// One GraphQL POST against the catalogue schema, which is the whole of EMX2's API surface:
/// <c>POST /{schema}/api/graphql</c> with an <c>x-molgenis-token</c> header. Both the gateway and
/// the schema reader go through here, so the token and the endpoint are stated once.
/// <para>
/// Failures come back as errors rather than exceptions, matching the source gateways, so one
/// unreachable catalogue costs a patient rather than the run.
/// </para>
/// </summary>
internal sealed class Emx2Client
{
    public const string CatalogueClient = "catalogue";

    /// <summary>
    /// EMX2's GraphQL input fields are camelCase, while its schema metadata and CSV headers are
    /// PascalCase. The payload records are named after the columns, so camelCase is the whole of
    /// the translation. Nulls are omitted rather than sent: <c>save</c> replaces a row, so an
    /// omitted column and an explicit null mean the same thing, and omitting keeps payloads small.
    /// <para>
    /// Internal so the leak test can serialize a payload exactly as the wire would see it, rather
    /// than against a copy of these settings that could drift.
    /// </para>
    /// </summary>
    internal static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new StronglyTypedIdJsonConverterFactory() },
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UploaderOptions _options;

    public Emx2Client(IHttpClientFactory httpClientFactory, UploaderOptions options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public Task<ErrorOr<JsonObject?>> QueryAsync(string document, CancellationToken cancellationToken) =>
        PostAsync(document, new { }, cancellationToken);

    public async Task<ErrorOr<JsonObject?>> PostAsync(
        string document,
        object variables,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(CatalogueClient);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/{_options.CatalogueSchema}/api/graphql")
            {
                Content = JsonContent.Create(new { query = document, variables }, options: PayloadOptions),
            };

            if (!string.IsNullOrWhiteSpace(_options.CatalogueToken))
            {
                request.Headers.Add("x-molgenis-token", _options.CatalogueToken);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            response.EnsureSuccessStatusCode();

            var parsed = JsonNode.Parse(body) as JsonObject;

            // EMX2 reports a rejected query or mutation in "errors" with a 200 status, so the body
            // decides whether this worked, not the status code.
            if (parsed?["errors"] is JsonArray { Count: > 0 } errors)
            {
                var message = errors[0]?["message"]?.GetValue<string>() ?? "unknown GraphQL error";
                return Error.Failure("Catalogue.Rejected", message);
            }

            return parsed?["data"] as JsonObject;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return Error.Unexpected("Catalogue.Unavailable", exception.Message);
        }
        catch (JsonException exception)
        {
            return Error.Failure("Catalogue.Unreadable", exception.Message);
        }
    }
}
