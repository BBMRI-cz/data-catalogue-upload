using BiobankApi.Application.Abstractions.Repositories;

namespace BiobankApi.Application.Features.Ingest;

/// <summary>The result of a <see cref="GetLatestIngestRunQuery"/>: the latest ingest, or null before the first.</summary>
public sealed record GetLatestIngestRunQueryResult(IngestRun? LastRun);
