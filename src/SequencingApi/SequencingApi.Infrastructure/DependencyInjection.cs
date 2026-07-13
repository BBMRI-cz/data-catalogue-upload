using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SequencingApi.Application.Abstractions.DataSource;
using SequencingApi.Infrastructure.Configuration;
using SequencingApi.Infrastructure.DataSource;
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

        // The single data source for this facility; the ingestion handler reads it.
        services.AddSingleton<ISequencingDataSource, StubSequencingDataSource>();

        return services;
    }
}
