using BiobankApi.Application.Abstractions.Export;

namespace BiobankApi.IntegrationTests;

/// <summary>Export source returning a fixed parse result, so the ingest pipeline can be tested without files.</summary>
internal sealed class FakePatientExportSource : IPatientExportSource
{
    private readonly ExportParseResult _result;

    public FakePatientExportSource(ExportParseResult result) => _result = result;

    public string Name => "fake";

    public ExportParseResult ParsePatients() => _result;
}
