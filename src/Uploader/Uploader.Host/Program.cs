using System.Text.Json;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Uploader.Application;
using Uploader.Application.Features.Sync;
using Uploader.Infrastructure;
using Uploader.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

using var host = builder.Build();

// Ensure the sync-state schema exists (parity with the Python create_all on startup).
using (var migrationScope = host.Services.CreateScope())
{
    var database = migrationScope.ServiceProvider.GetRequiredService<UploaderDbContext>();
    await database.Database.MigrateAsync();
}

using var scope = host.Services.CreateScope();
var sender = scope.ServiceProvider.GetRequiredService<ISender>();
var result = await sender.Send(new RunCatalogueSyncCommand());

var summaryJsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    WriteIndented = true,
};

return result.Match(
    summary =>
    {
        Console.WriteLine(JsonSerializer.Serialize(summary, summaryJsonOptions));
        return summary.Failed == 0 ? 0 : 1;
    },
    errors =>
    {
        Console.Error.WriteLine(string.Join("; ", errors.Select(error => error.Description)));
        return 1;
    });
