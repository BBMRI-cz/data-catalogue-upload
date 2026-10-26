using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Uploader.Application;
using Uploader.Host.Endpoints;
using Uploader.Host.Scheduling;
using Uploader.Infrastructure;
using Uploader.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();

builder.Services.AddSingleton<SyncRunner>();

// In-process sync via Quartz, like the source APIs' ingestion (OS-independent: identical on Windows
// and Linux). Mon/Wed/Fri 02:00 UTC, after the Sunday 17:00 UTC source ingests; the cron is
// overridable with UPLOADER_SYNC_CRON, and POST /admin/sync triggers a run on demand.
// Disabled with DisableScheduler=true: integration tests spin up many hosts in one process, and
// Quartz's global static logging provider captures (then over-disposes) a per-host LoggerFactory.
if (!builder.Configuration.GetValue<bool>("DisableScheduler"))
{
    var syncCron = builder.Configuration["UPLOADER_SYNC_CRON"] ?? "0 0 2 ? * MON,WED,FRI";
    builder.Services.AddQuartz(quartz =>
    {
        var jobKey = new JobKey("sync");
        quartz.AddJob<SyncJob>(jobKey);
        quartz.AddTrigger(trigger => trigger
            .ForJob(jobKey)
            .WithCronSchedule(syncCron, cron => cron.InTimeZone(TimeZoneInfo.Utc)));
    });
    builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
}

var app = builder.Build();

// Apply pending migrations when explicitly asked (set by the container; tests leave it unset).
if (string.Equals(Environment.GetEnvironmentVariable("RUN_MIGRATIONS"), "true", StringComparison.OrdinalIgnoreCase))
{
    using var migrationScope = app.Services.CreateScope();
    await migrationScope.ServiceProvider.GetRequiredService<UploaderDbContext>().Database.MigrateAsync();
}

app.MapOpenApi();
app.MapHealthEndpoints();
app.MapAdminEndpoints();

await app.RunAsync();
return 0;

/// <summary>Exposed so the integration tests can host the app via <c>WebApplicationFactory</c>.</summary>
public partial class Program;
