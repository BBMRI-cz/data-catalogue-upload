using Uploader.Application.Abstractions;

namespace Uploader.Application.Features.Sync;

/// <summary>The result of a <see cref="GetLatestSyncRunQuery"/>: the latest run, or null before the first.</summary>
public sealed record GetLatestSyncRunQueryResult(SyncRun? LastRun);
