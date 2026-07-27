using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BiobankApi.Application.Abstractions.Repositories;
using BiobankApi.Domain.Patients;
using BiobankApi.Web.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BiobankApi.IntegrationTests;

public sealed class ApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static HttpClient CreateClient(params PatientAggregate[] patients)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DisableScheduler", "true");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPatientRepository>();
                services.AddScoped<IPatientRepository>(_ => new FakePatientRepository(patients));
            });
        });

        return factory.CreateClient();
    }

    [Fact]
    public async Task HealthReturnsOk()
    {
        var response = await CreateClient().GetAsync("/health");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>(JsonOptions);
        Assert.Equal("ok", body!.Status);
    }

    [Fact]
    public async Task ListPatientsEmpty()
    {
        var response = await CreateClient().GetAsync("/patients");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PatientResponse>>(JsonOptions);
        Assert.Empty(body!);
    }

    [Fact]
    public async Task ListPatientsReturnsRepositoryData()
    {
        var response = await CreateClient(PatientAggregate.Create("P1").Value).GetAsync("/patients");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PatientResponse>>(JsonOptions);
        var patient = Assert.Single(body!);
        Assert.Equal("P1", patient.PatientId);
        Assert.Empty(patient.Samples);
    }
}
