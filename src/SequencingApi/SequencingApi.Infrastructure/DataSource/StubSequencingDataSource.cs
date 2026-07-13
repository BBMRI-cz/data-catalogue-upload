using ErrorOr;
using SequencingApi.Application.Abstractions.DataSource;

namespace SequencingApi.Infrastructure.DataSource;

/// <summary>
/// Placeholder data source: reports zero records and no errors so the ingestion pipeline runs
/// end-to-end while the service is still a scaffold.
/// </summary>
/// <remarks>
/// ponytail: replace with the real reader (its own <c>*DataSource</c> under this namespace) once the
/// sequencing data format is defined in #30 — mirror BiobankApi's <c>XmlExportParser</c>.
/// </remarks>
internal sealed class StubSequencingDataSource : ISequencingDataSource
{
    public string Name => "stub";

    public ErrorOr<RecordReadResult> ReadRecords() =>
        new RecordReadResult(RecordCount: 0, Errors: []);
}
