using System.Text.Json;
using BiobankApi.Application;
using BiobankApi.Infrastructure;
using BiobankApi.Infrastructure.Persistence;
using BiobankApi.Web.Endpoints;
using BiobankApi.Web.Scheduling;
using Microsoft.EntityFrameworkCore;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();

// Weekly in-process ingestion via Quartz (OS-independent: identical on Windows and Linux). The cron
// is overridable with BIOBANK_INGEST_CRON for testing; POST /admin/ingest triggers a run on demand.
// Disabled with DisableScheduler=true: integration tests spin up many hosts in one process, and
// Quartz's global static logging provider captures (then over-disposes) a per-host LoggerFactory.
if (!builder.Configuration.GetValue<bool>("DisableScheduler"))
{
    var ingestCron = builder.Configuration["BIOBANK_INGEST_CRON"] ?? "0 0 17 ? * SUN"; // Sundays 17:00 UTC
    builder.Services.AddQuartz(quartz =>
    {
        var jobKey = new JobKey("ingestion");
        quartz.AddJob<IngestionJob>(jobKey);
        quartz.AddTrigger(trigger => trigger.ForJob(jobKey).WithCronSchedule(ingestCron));
    });
    builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
}

var app = builder.Build();

// Apply pending migrations when explicitly asked (set by the container; tests leave it unset).
if (string.Equals(Environment.GetEnvironmentVariable("RUN_MIGRATIONS"), "true", StringComparison.OrdinalIgnoreCase))
{
    using var migrationScope = app.Services.CreateScope();
    await migrationScope.ServiceProvider.GetRequiredService<BiobankDbContext>().Database.MigrateAsync();
}

app.MapOpenApi();
app.MapHealthEndpoints();
app.MapPatientEndpoints();
app.MapAdminEndpoints();

await app.RunAsync();
return 0;

/// <summary>Exposed so the integration tests can host the app via <c>WebApplicationFactory</c>.</summary>
public partial class Program;
