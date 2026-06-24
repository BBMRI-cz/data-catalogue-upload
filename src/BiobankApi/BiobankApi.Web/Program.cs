using System.Text.Json;
using BiobankApi.Application;
using BiobankApi.Application.Features.Ingest;
using BiobankApi.Infrastructure;
using BiobankApi.Infrastructure.Persistence;
using BiobankApi.Web.Endpoints;
using Mediator;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply pending migrations when explicitly asked (set by the container; tests leave it unset).
if (string.Equals(Environment.GetEnvironmentVariable("RUN_MIGRATIONS"), "true", StringComparison.OrdinalIgnoreCase))
{
    using var migrationScope = app.Services.CreateScope();
    await migrationScope.ServiceProvider.GetRequiredService<BiobankDbContext>().Database.MigrateAsync();
}

// Ingest mode: `biobank-api ingest` parses the XML exports, persists them, and exits.
if (args.Contains("ingest"))
{
    using var scope = app.Services.CreateScope();
    var sender = scope.ServiceProvider.GetRequiredService<ISender>();
    var ingestResult = await sender.Send(new IngestExportsCommand());
    return ingestResult.Match(
        result =>
        {
            Console.WriteLine($"Ingested {result.Ingested} patients; {result.Failed} failed.");
            foreach (var failure in result.Errors)
            {
                Console.Error.WriteLine($"  {failure.Source} {failure.Reference}: {failure.Reason}");
            }

            return 0;
        },
        errors =>
        {
            Console.Error.WriteLine(string.Join("; ", errors.Select(error => error.Description)));
            return 1;
        });
}

app.MapOpenApi();
app.MapHealthEndpoints();
app.MapPatientEndpoints();

await app.RunAsync();
return 0;

/// <summary>Exposed so the integration tests can host the app via <c>WebApplicationFactory</c>.</summary>
public partial class Program;
