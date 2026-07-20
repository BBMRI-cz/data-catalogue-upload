using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SequencingApi.Application.Abstractions.DataSource;
using SequencingApi.Application.Abstractions.Repositories;
using SequencingApi.Infrastructure.Configuration;
using SequencingApi.Infrastructure.DataSource.Mmci;
using SequencingApi.Infrastructure.Persistence;

namespace SequencingApi.Infrastructure;

/// <summary>Composition of the sequencing_api infrastructure layer (EF Core, export source).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = SequencingOptions.FromConfiguration(configuration);
        services.AddSingleton(options);

        services.AddDbContext<SequencingDbContext>(db => db.UseNpgsql(options.ConnectionString));

        // One repository per aggregate root, plus the cross-aggregate read model.
        services.AddScoped<ISampleRepository, SqlSampleRepository>();
        services.AddScoped<ISequencingRunRepository, SqlSequencingRunRepository>();
        services.AddScoped<IPanelRepository, SqlPanelRepository>();
        services.AddScoped<ISequencingStatsReader, SqlSequencingStatsReader>();

        // The single data source for this facility; the ingestion handler reads it. Another facility
        // would supply its own adapter under DataSource/<Facility>/ and be registered here instead.
        services.AddSingleton<ISequencingDataSource>(_ => new MmciSequencingDataSource(
            options.SequencingDataPath,
            options.SequencingLibrariesPath,
            options.SequencingMappingTablePath));

        return services;
    }
}
