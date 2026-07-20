using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BiobankApi.Application.Abstractions.Export;
using BiobankApi.Application.Abstractions.Repositories;
using BiobankApi.Domain.Patients;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BiobankApi.IntegrationTests;

public sealed class AdminEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact]
    public async Task IngestRunsPipelineAndReturnsSummary()
    {
        var patients = new List<PatientAggregate>
        {
            PatientAggregate.Create("P1").Value,
            PatientAggregate.Create("P2").Value,
        };
        var errors = new List<ExportParseError> { new("fake", "bad.xml", "broken record") };
        var source = new FakePatientExportSource(new ExportParseResult(patients, errors));

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DisableScheduler", "true");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPatientExportSource>();
                services.AddSingleton<IPatientExportSource>(source);
                services.RemoveAll<IPatientRepository>();
                services.AddScoped<IPatientRepository>(_ => new FakePatientRepository([]));
            });
        });

        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/admin/ingest", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<IngestSummary>(JsonOptions);
        Assert.Equal(2, summary!.Ingested);
        Assert.Equal(1, summary.Failed);
    }

    private sealed record IngestSummary(int Ingested, int Failed);
}
