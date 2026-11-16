using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TrainingSeeder.Seeding;

/// <summary>
/// One service provider per database, each composed by that service's own <c>AddInfrastructure</c>,
/// so the seeder writes through exactly the repositories and mappings the API reads with. Both
/// services read <c>POSTGRES_*</c>, so each gets its own prefix: <c>BIOBANK_POSTGRES_HOST</c> is the
/// biobank's <c>POSTGRES_HOST</c>, <c>SEQUENCING_POSTGRES_HOST</c> the sequencing one's.
/// </summary>
internal static class SeederServices
{
    public const string BiobankPrefix = "BIOBANK_";
    public const string SequencingPrefix = "SEQUENCING_";

    /// <param name="configure">Changes to the registrations before the provider is built; tests swap in SQLite here.</param>
    public static ServiceProvider Biobank(IConfiguration configuration, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        BiobankApi.Infrastructure.DependencyInjection.AddInfrastructure(services, Prefixed(configuration, BiobankPrefix));
        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <param name="configure">Changes to the registrations before the provider is built; tests swap in SQLite here.</param>
    public static ServiceProvider Sequencing(IConfiguration configuration, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        SequencingApi.Infrastructure.DependencyInjection.AddInfrastructure(services, Prefixed(configuration, SequencingPrefix));
        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static IConfiguration Prefixed(IConfiguration configuration, string prefix) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(configuration.AsEnumerable()
                .Where(setting => setting.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(setting => new KeyValuePair<string, string?>(setting.Key[prefix.Length..], setting.Value)))
            .Build();
}
