using BiobankApi.Application.Abstractions.Export;
using BiobankApi.Application.Abstractions.Repositories;
using BiobankApi.Infrastructure.Configuration;
using BiobankApi.Infrastructure.Persistence;
using BiobankApi.Infrastructure.Xml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BiobankApi.Infrastructure;

/// <summary>Composition of the biobank_api infrastructure layer (EF Core, repository, XML parser).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = BiobankOptions.FromConfiguration(configuration);
        services.AddSingleton(options);

        services.AddDbContext<BiobankDbContext>(db => db.UseNpgsql(options.ConnectionString));

        services.AddScoped<IPatientRepository, SqlPatientRepository>();

        // The single export source for this biobank; the ingestion handler reads it.
        services.AddSingleton<IPatientExportSource>(_ => new XmlExportParser(options.BiobankXmlExportPath));

        return services;
    }
}
