using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Uploader.Application.Behaviors;
using Uploader.Application.Builders;
using Uploader.Domain.Services;

namespace Uploader.Application;

/// <summary>Composition of the uploader application layer (CQRS, behaviors, builders, domain services).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);

        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddValidatorsFromAssembly(
            typeof(DependencyInjection).Assembly,
            includeInternalTypes: true);

        // Anti-corruption builders (raw JSON -> domain).
        services.AddSingleton<ClinicalBuilder>();
        services.AddSingleton<RadiologyBuilder>();
        services.AddSingleton<SequencingBuilder>();
        services.AddSingleton<WsiBuilder>();

        // Domain services.
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IFingerprintCalculator, FingerprintCalculator>();
        services.AddSingleton<ISyncPlanner, FingerprintSyncPlanner>();

        return services;
    }
}
