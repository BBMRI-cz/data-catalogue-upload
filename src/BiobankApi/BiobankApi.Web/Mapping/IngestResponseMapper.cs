using BiobankApi.Application.Abstractions.Export;
using BiobankApi.Application.Features.Ingest;
using BiobankApi.Web.Endpoints;

namespace BiobankApi.Web.Mapping;

/// <summary>
/// Projects an <see cref="IngestExportsCommandResult"/> onto the <c>POST /admin/ingest</c> response
/// contract.
/// </summary>
internal static class IngestResponseMapper
{
    public static IngestResponse ToResponse(IngestExportsCommandResult ingest) => new(
        ingest.Ingested,
        ingest.Failed,
        [.. ingest.Errors.Select(ToResponse)]);

    private static IngestErrorResponse ToResponse(ExportParseError error) =>
        new(error.Source, error.Reference, error.Reason);
}
