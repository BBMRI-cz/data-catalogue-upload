using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Uploader.Application.Abstractions;
using Uploader.Infrastructure.Configuration;
using Uploader.Infrastructure.Http;
using Uploader.Infrastructure.Persistence;

namespace Uploader.Infrastructure;

/// <summary>Composition of the uploader infrastructure layer (EF Core, repositories, HTTP gateways).</summary>
public static class DependencyInjection
{
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(30);

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = UploaderOptions.FromConfiguration(configuration);
        services.AddSingleton(options);
        services.TryAddSingleton(TimeProvider.System);

        services.AddDbContext<UploaderDbContext>(db => db.UseNpgsql(options.ConnectionString));

        services.AddScoped<ISyncStateRepository, SyncStateRepository>();
        services.AddScoped<ISyncRunRepository, SyncRunRepository>();

        AddSourceClient(services, HttpSourceDataGateway.BiobankClient, options.BiobankApiUrl);
        AddSourceClient(services, HttpSourceDataGateway.RadiologyClient, options.RadiologyApiUrl);
        AddSourceClient(services, HttpSourceDataGateway.SequencingClient, options.SequencingApiUrl);
        AddSourceClient(services, HttpSourceDataGateway.WsiClient, options.WsiApiUrl);
        AddSourceClient(services, HttpCatalogueGateway.CatalogueClient, options.CatalogueApiUrl);

        services.AddSingleton<ISourceDataGateway, HttpSourceDataGateway>();
        services.AddSingleton<ICatalogueGateway, HttpCatalogueGateway>();

        return services;
    }

    private static void AddSourceClient(IServiceCollection services, string name, string baseUrl) =>
        services.AddHttpClient(name, client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = HttpTimeout;
        });
}
